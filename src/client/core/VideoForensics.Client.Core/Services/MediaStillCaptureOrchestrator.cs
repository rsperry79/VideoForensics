using Microsoft.Extensions.Logging;

using System.Security.Cryptography;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Providers.Core;

namespace VideoForensics.Client.Core.Services
{
    /// <summary>
    /// Orchestrates the Evidence "grab still with hash" capture: prefers deterministic server-side
    /// frame extraction from the untouched source file, falls back to a client-captured PNG only
    /// when extraction is unavailable, persists the still as a new, independently-hashed MediaItem
    /// with its capture provenance, records a chain-of-custody ActionLog entry, and optionally pins
    /// the result to a case. The original source media file is never modified.
    /// </summary>
    internal class MediaStillCaptureOrchestrator : IMediaStillCaptureService
    {
        /// <summary>Maximum decoded size accepted for a client-captured still (protects against a hostile/oversized upload).</summary>
        private const long MaxClientStillBytes = 20 * 1024 * 1024; // 20 MB

        /// <summary>
        /// Maximum accepted length of the base64 request payload itself, checked before any
        /// decoding is attempted. Chosen just above the base64 length that exactly encodes
        /// <see cref="MaxClientStillBytes"/> (ceil(20 MiB / 3) * 4 = 27,962,028 chars), so any
        /// string this long or shorter can still fail the post-decode size check below, while an
        /// even longer string is rejected immediately without ever calling Convert.FromBase64String.
        /// </summary>
        private const int MaxClientPngBase64Length = 28_000_000;

        /// <summary>PNG file signature (first 8 bytes of any valid PNG).</summary>
        private static readonly byte[] PngSignature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

        private readonly ILogger<MediaStillCaptureOrchestrator> _logger;
        private readonly IMediaItemRepository _mediaItemRepository;
        private readonly IMediaStillRepository _mediaStillRepository;
        private readonly IMediaStorageProvider _storageProvider;
        private readonly IMediaFrameExtractor _frameExtractor;
        private readonly IActionLogRepository _actionLogRepository;
        private readonly ICaseRepository _caseRepository;

        public MediaStillCaptureOrchestrator(
            ILogger<MediaStillCaptureOrchestrator> logger,
            IMediaItemRepository mediaItemRepository,
            IMediaStillRepository mediaStillRepository,
            IMediaStorageProvider storageProvider,
            IMediaFrameExtractor frameExtractor,
            IActionLogRepository actionLogRepository,
            ICaseRepository caseRepository)
        {
            _logger = logger;
            _mediaItemRepository = mediaItemRepository;
            _mediaStillRepository = mediaStillRepository;
            _storageProvider = storageProvider;
            _frameExtractor = frameExtractor;
            _actionLogRepository = actionLogRepository;
            _caseRepository = caseRepository;
        }

        /// <inheritdoc />
        public async Task<MediaStillCaptureResult> CaptureStillAsync(
            Guid sourceMediaItemId,
            long frameOffsetMs,
            string? clientPngBase64,
            Guid? caseId,
            string? pinReason,
            string capturedBy,
            CancellationToken ct)
        {
            if (frameOffsetMs < 0)
            {
                return MediaStillCaptureResult.Fail(MediaStillCaptureError.InvalidInput, "frameOffsetMs must be >= 0.");
            }

            if (caseId.HasValue && string.IsNullOrWhiteSpace(pinReason))
            {
                return MediaStillCaptureResult.Fail(MediaStillCaptureError.InvalidInput, "A reason is required to pin the still to a case.");
            }

            MediaItem? source = await _mediaItemRepository.GetAsync(sourceMediaItemId, ct);
            if (source is null)
            {
                return MediaStillCaptureResult.Fail(MediaStillCaptureError.MediaNotFound, $"Media item {sourceMediaItemId} not found.");
            }

            if (!await _storageProvider.ExistsAsync(source.FilePath, ct))
            {
                return MediaStillCaptureResult.Fail(MediaStillCaptureError.MediaNotFound, "Source media file does not exist on disk.");
            }

            // Recompute the source file's hash independently at the moment of capture (forensic
            // requirement) rather than trusting the already-stored Sha256Hash column, which could
            // in principle be stale relative to the file on disk.
            string sourceHashAtCapture = await ComputeSha256Async(source.FilePath, ct);

            FrameExtractionResult extraction = await _frameExtractor.ExtractFrameAsync(source.FilePath, frameOffsetMs, ct);

            byte[]? stillBytes;
            MediaStillCaptureMethod method;

            if (extraction.Success && extraction.PngBytes is { Length: > 0 } extractedBytes)
            {
                stillBytes = extractedBytes;
                method = MediaStillCaptureMethod.Server;
            }
            else if (!string.IsNullOrWhiteSpace(clientPngBase64))
            {
                // Reject an implausibly long payload before ever attempting to decode it - decoding
                // allocates a buffer roughly 3/4 of the input's length, so an unbounded string is
                // itself a memory-exhaustion vector independent of the post-decode size check below.
                if (clientPngBase64.Length > MaxClientPngBase64Length)
                {
                    return MediaStillCaptureResult.Fail(
                        MediaStillCaptureError.InvalidInput,
                        $"pngBase64 is too long (max {MaxClientPngBase64Length} base64 characters).");
                }

                byte[] decoded;
                try
                {
                    decoded = Convert.FromBase64String(clientPngBase64);
                }
                catch (FormatException)
                {
                    return MediaStillCaptureResult.Fail(MediaStillCaptureError.InvalidInput, "pngBase64 is not valid base64.");
                }

                if (decoded.Length > MaxClientStillBytes)
                {
                    return MediaStillCaptureResult.Fail(
                        MediaStillCaptureError.InvalidInput,
                        $"Decoded pngBase64 exceeds the maximum allowed size of {MaxClientStillBytes} bytes.");
                }

                if (!StartsWithPngSignature(decoded))
                {
                    return MediaStillCaptureResult.Fail(MediaStillCaptureError.InvalidInput, "pngBase64 does not start with a valid PNG signature.");
                }

                stillBytes = decoded;
                method = MediaStillCaptureMethod.Client;
            }
            else
            {
                _logger.LogWarning(
                    "Still capture failed for source {SourceMediaItemId} at {FrameOffsetMs}ms: {Error}",
                    sourceMediaItemId, frameOffsetMs, extraction.ErrorMessage);
                return MediaStillCaptureResult.Fail(
                    MediaStillCaptureError.ExtractionFailed,
                    extraction.ErrorMessage ?? "Frame extraction failed and no client-captured still was provided.");
            }

            if (stillBytes.Length == 0)
            {
                return MediaStillCaptureResult.Fail(MediaStillCaptureError.InvalidInput, "Captured still has no content.");
            }

            string stillHash = Convert.ToHexString(SHA256.HashData(stillBytes)).ToLowerInvariant();
            var stillId = Guid.NewGuid();
            string stillFileName = $"{source.Id:N}_still_{frameOffsetMs}ms_{stillId:N}.png";
            string? sourceDirectory = Path.GetDirectoryName(source.FilePath);
            string stillFilePath = string.IsNullOrEmpty(sourceDirectory)
                ? stillFileName
                : Path.Combine(sourceDirectory, "stills", stillFileName);

            using (var memoryStream = new MemoryStream(stillBytes))
            {
                await _storageProvider.SaveAsync(stillFilePath, memoryStream, ct);
            }

            var nowUtc = DateTime.UtcNow;
            var stillMediaItem = new MediaItem
            {
                Id = stillId,
                DeviceId = source.DeviceId,
                FileName = stillFileName,
                FilePath = stillFilePath,
                MediaFormat = "image/png",
                FileSizeBytes = stillBytes.Length,
                RecordedAtUtc = source.RecordedAtUtc.AddMilliseconds(frameOffsetMs),
                DownloadedAtUtc = nowUtc,
                Sha256Hash = stillHash,
                IntegrityVerified = true,
                LastVerifiedAtUtc = nowUtc
            };

            var capture = new MediaStillCapture
            {
                MediaItemId = stillId,
                SourceMediaItemId = source.Id,
                FrameOffsetMs = frameOffsetMs,
                SourceSha256AtCapture = sourceHashAtCapture,
                CapturedByOperator = capturedBy,
                CaptureMethod = method,
                CreatedAtUtc = nowUtc
            };

            _ = await _mediaStillRepository.CreateAsync(stillMediaItem, capture, ct);

            _ = await _actionLogRepository.AppendAsync(
                capturedBy,
                ActorType.Human,
                "CaptureStill",
                nameof(MediaItem),
                stillId,
                $"Source: {source.Id}, FrameOffsetMs: {frameOffsetMs}, Method: {method}, SourceSha256AtCapture: {sourceHashAtCapture}, StillSha256: {stillHash}",
                ct);

            bool pinnedToCase = false;
            if (caseId.HasValue)
            {
                try
                {
                    _ = await _caseRepository.AddItemAsync(caseId.Value, CaseItemKind.Media, stillId, pinReason!, capturedBy, ct);
                    pinnedToCase = true;
                }
                catch (Exception ex)
                {
                    // The still itself was already captured and persisted; a failed pin shouldn't
                    // undo that. Surface it via logging so an operator can retry pinning separately.
                    _logger.LogWarning(ex, "Still {StillId} captured but failed to pin to case {CaseId}", stillId, caseId);
                }
            }

            var info = new MediaStillInfo
            {
                Id = stillId,
                SourceMediaItemId = source.Id,
                FrameOffsetMs = frameOffsetMs,
                Sha256Hash = stillHash,
                SourceSha256AtCapture = sourceHashAtCapture,
                CapturedByOperator = capturedBy,
                CaptureMethod = method.ToString(),
                CreatedAtUtc = capture.CreatedAtUtc,
                PinnedToCase = pinnedToCase
            };

            _logger.LogInformation(
                "Captured still {StillId} from source {SourceMediaItemId} at {FrameOffsetMs}ms via {Method} by {CapturedBy}",
                stillId, source.Id, frameOffsetMs, method, capturedBy);

            return MediaStillCaptureResult.Ok(info);
        }

        private async Task<string> ComputeSha256Async(string filePath, CancellationToken ct)
        {
            using Stream stream = await _storageProvider.OpenReadStreamAsync(filePath, ct);
            byte[] hash = await SHA256.HashDataAsync(stream, ct);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }

        /// <summary>True if <paramref name="bytes"/> begins with the 8-byte PNG file signature.</summary>
        private static bool StartsWithPngSignature(byte[] bytes)
        {
            if (bytes.Length < PngSignature.Length)
            {
                return false;
            }

            for (int i = 0; i < PngSignature.Length; i++)
            {
                if (bytes[i] != PngSignature[i])
                {
                    return false;
                }
            }

            return true;
        }
    }
}
