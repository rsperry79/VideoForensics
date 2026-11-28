using Microsoft.Extensions.Logging;

using System.Security.Cryptography;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Core.Helpers.Platform;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Providers.Ring.Services;

namespace VideoForensics.Client.Core.Services
{
    /// <summary>
    /// Stores a JPEG grabbed from a live camera as a <see cref="MediaKinds.Still"/> media item, in the camera's
    /// <c>stills</c> folder beside its snapshots. The file name carries a unique id, so two stills never share a
    /// path and a failed insert can delete only its own file.
    /// </summary>
    /// <remarks>
    /// Why the still is not a MediaStillCapture: that table needs a source media item, and a live frame has none.
    /// The operator is recorded in the action log, because a media row has no operator field.
    /// Why the kind is Still: snapshot queries, roll-up and the snapshot dedup hash read only snapshot rows, so a
    /// live grab does not change how provider snapshots are skipped.
    /// </remarks>
    public sealed class LiveStillStore : ILiveStillStore
    {
        /// <summary>Largest JPEG accepted, matching the review-page still limit.</summary>
        internal const long MaxJpegBytes = 20 * 1024 * 1024;

        private const string ActionName = "CaptureLiveStill";

        private static readonly byte[] JpegSignature = [0xFF, 0xD8, 0xFF];

        private readonly ILogger<LiveStillStore> _logger;
        private readonly IMediaItemWriteRepository _mediaItems;
        private readonly IMediaStorageProvider _storage;
        private readonly IMediaPathResolver _paths;
        private readonly IActionLogRepository _actionLog;

        public LiveStillStore(
            ILogger<LiveStillStore> logger,
            IMediaItemWriteRepository mediaItems,
            IMediaStorageProvider storage,
            IMediaPathResolver paths,
            IActionLogRepository actionLog)
        {
            _logger = logger;
            _mediaItems = mediaItems;
            _storage = storage;
            _paths = paths;
            _actionLog = actionLog;
        }

        /// <inheritdoc />
        public async Task<LiveStillResult> StoreAsync(LiveStillRequest request, CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(request);

            LiveStillResult? invalid = Validate(request);
            if (invalid is not null)
            {
                return invalid;
            }

            // Truncating to the millisecond makes (device, instant) a stable identity for one frame.
            DateTime instant = DateTime.SpecifyKind(
                new DateTime(request.CapturedAtUtc.Ticks - (request.CapturedAtUtc.Ticks % TimeSpan.TicksPerMillisecond), DateTimeKind.Unspecified),
                DateTimeKind.Utc);
            string hash = Convert.ToHexString(SHA256.HashData(request.Jpeg)).ToLowerInvariant();

            MediaItem? existing = await FindStillAtInstantAsync(request.DeviceId, instant, ct);
            if (existing is not null)
            {
                return existing.Sha256Hash == hash
                    ? LiveStillResult.Ok(existing.Id)
                    : LiveStillResult.Fail(LiveStillError.Conflict, "Another frame from this camera is already stored at this instant.");
            }

            var stillId = Guid.NewGuid();
            string fileName = StillFileName(request.DeviceName, instant, stillId);
            string folder = Path.Combine(MediaFolderLayout.GetDeviceFolder(request.MediaRoot, request.ProviderName, request.LocationName, request.DeviceName), "stills");
            string storedPath = await _paths.ToStoredAsync(Path.Combine(folder, fileName), ct);

            try
            {
                using var content = new MemoryStream(request.Jpeg);
                await _storage.SaveAsync(storedPath, content, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError("Could not save a live still for {DeviceName}: {Message} ({ErrorType})", request.DeviceName, ex.Message, ex.GetType().Name);
                return LiveStillResult.Fail(LiveStillError.StorageFailed, "The still could not be saved.");
            }

            DateTime nowUtc = DateTime.UtcNow;
            var row = new MediaItem
            {
                Id = stillId,
                DeviceId = request.DeviceId,
                FileName = fileName,
                FilePath = storedPath,
                MediaFormat = "jpg",
                MediaKind = MediaKinds.Still,
                FileSizeBytes = request.Jpeg.Length,
                RecordedAtUtc = instant,
                DownloadedAtUtc = nowUtc,
                Sha256Hash = hash,
                IntegrityVerified = true,
                LastVerifiedAtUtc = nowUtc
            };

            try
            {
                await _mediaItems.AddAsync(row, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // A concurrent request for the same instant may have won the insert. Its file is at its own path, so
                // this request's file is the only one to delete, and only when no row for the instant exists.
                MediaItem? winner = await FindStillAtInstantAsync(request.DeviceId, instant, ct);
                if (winner is not null)
                {
                    return winner.Sha256Hash == hash
                        ? LiveStillResult.Ok(winner.Id)
                        : LiveStillResult.Fail(LiveStillError.Conflict, "Another frame from this camera is already stored at this instant.");
                }

                _logger.LogError("Could not record a live still for {DeviceName}: {Message} ({ErrorType})", request.DeviceName, ex.Message, ex.GetType().Name);
                await TryDeleteAsync(storedPath, ct);
                return LiveStillResult.Fail(LiveStillError.StorageFailed, "The still could not be recorded.");
            }

            _ = await _actionLog.AppendAsync(
                request.OperatorName,
                ActorType.Human,
                ActionName,
                nameof(MediaItem),
                stillId,
                $"Device: {request.DeviceId:D}, RecordedAtUtc: {instant:O}, Sha256: {hash}",
                ct);

            _logger.LogInformation("Stored a live still for {DeviceName} as {StillId}", request.DeviceName, stillId);
            return LiveStillResult.Ok(stillId);
        }

        /// <summary>Validates the request at the boundary. Returns null when it is usable.</summary>
        private static LiveStillResult? Validate(LiveStillRequest request)
        {
            if (request.Jpeg is null || request.Jpeg.Length == 0)
            {
                return LiveStillResult.Fail(LiveStillError.InvalidInput, "The still is empty.");
            }

            if (request.Jpeg.Length > MaxJpegBytes)
            {
                return LiveStillResult.Fail(LiveStillError.InvalidInput, $"The still exceeds the maximum size of {MaxJpegBytes} bytes.");
            }

            if (!StartsWith(request.Jpeg, JpegSignature))
            {
                return LiveStillResult.Fail(LiveStillError.InvalidInput, "The still is not a JPEG.");
            }

            if (string.IsNullOrWhiteSpace(request.OperatorName))
            {
                return LiveStillResult.Fail(LiveStillError.InvalidInput, "An operator is required.");
            }

            return null;
        }

        /// <summary>The still already stored for this camera at this instant, or null.</summary>
        private async Task<MediaItem?> FindStillAtInstantAsync(Guid deviceId, DateTime instant, CancellationToken ct)
        {
            IReadOnlyList<MediaItem> rows = await _mediaItems.GetByDeviceAndDateRangeAsync(deviceId, instant, instant, ct);
            return rows.FirstOrDefault(r => r.MediaKind == MediaKinds.Still && r.RecordedAtUtc == instant);
        }

        private static string StillFileName(string deviceName, DateTime instant, Guid stillId)
        {
            string formatted = MediaFileNamer.FormatMediaFileName(deviceName, instant, "still", "jpg");
            string stem = Path.GetFileNameWithoutExtension(formatted);
            return $"{stem}_{stillId:N}.jpg";
        }

        private async Task TryDeleteAsync(string storedPath, CancellationToken ct)
        {
            try
            {
                await _storage.DeleteAsync(storedPath, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning("Could not remove an unrecorded live still: {Message} ({ErrorType})", ex.Message, ex.GetType().Name);
            }
        }

        private static bool StartsWith(byte[] bytes, byte[] signature)
        {
            if (bytes.Length < signature.Length)
            {
                return false;
            }

            for (int i = 0; i < signature.Length; i++)
            {
                if (bytes[i] != signature[i])
                {
                    return false;
                }
            }

            return true;
        }
    }
}
