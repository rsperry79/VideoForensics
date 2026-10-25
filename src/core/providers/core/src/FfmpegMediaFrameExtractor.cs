using FFMpegCore;
using FFMpegCore.Pipes;
using Microsoft.Extensions.Logging;

using System.Diagnostics;

namespace VideoForensics.Core.Providers.Core
{
    /// <summary>Result of a single frame extraction attempt.</summary>
    public class FrameExtractionResult
    {
        /// <summary>True if a frame was successfully extracted.</summary>
        public bool Success { get; set; }

        /// <summary>The extracted frame's PNG-encoded bytes, if successful.</summary>
        public byte[]? PngBytes { get; set; }

        /// <summary>Error message if extraction failed.</summary>
        public string? ErrorMessage { get; set; }
    }

    /// <summary>
    /// Extracts a single still frame from a local video file at an arbitrary millisecond offset.
    /// Provider-agnostic (unlike VideoForensics.Providers.Ring's VideoFrameExtractor, which extracts
    /// frames at detection timestamps for Ring-specific evidence tagging): this is the plain
    /// "give me the frame at this offset" primitive used by the Evidence "grab still with hash"
    /// feature for any stored video file, from any provider.
    /// </summary>
    public interface IMediaFrameExtractor
    {
        /// <summary>Extracts the frame at <paramref name="frameOffsetMs"/> milliseconds into the video as PNG bytes.</summary>
        Task<FrameExtractionResult> ExtractFrameAsync(string videoFilePath, long frameOffsetMs, CancellationToken ct);
    }

    /// <summary>
    /// Thrown internally when ffmpeg's stdout exceeds the configured maximum size, so the caller
    /// can distinguish "output too large" from an ordinary I/O failure and kill the process instead
    /// of letting it keep writing into an unbounded buffer.
    /// </summary>
    internal sealed class OutputTooLargeException : Exception
    {
        public OutputTooLargeException(string message) : base(message)
        {
        }
    }

    /// <summary>
    /// Extracts a single frame through FFMpegCore, writing one PNG into a bounded in-memory buffer. The buffer fails as soon
    /// as the cap is passed, so an oversized output stops the ffmpeg process rather than filling memory. Timeout and caller
    /// cancellation both stop the process through FFMpegCore's cancellation.
    /// </summary>
    public class FfmpegMediaFrameExtractor : IMediaFrameExtractor
    {
        /// <summary>Default wall-clock budget for a single extraction before the ffmpeg process is killed.</summary>
        public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);

        /// <summary>Default cap on the extracted PNG, so a pathological/hostile input file can't exhaust memory.</summary>
        public const long DefaultMaxOutputBytes = 50 * 1024 * 1024; // 50 MB

        private readonly ILogger<FfmpegMediaFrameExtractor> _logger;
        private readonly TimeSpan _timeout;
        private readonly long _maxOutputBytes;

        /// <summary>
        /// Creates the extractor. <paramref name="ffmpegPath"/> is resolved like the other ffmpeg tools; its folder is handed to
        /// FFMpegCore so the same binary runs.
        /// </summary>
        public FfmpegMediaFrameExtractor(
            ILogger<FfmpegMediaFrameExtractor> logger,
            string? ffmpegPath = null,
            TimeSpan? timeout = null,
            long? maxOutputBytes = null)
        {
            _logger = logger;
            _timeout = timeout ?? DefaultTimeout;
            _maxOutputBytes = maxOutputBytes ?? DefaultMaxOutputBytes;

            string resolved = FfmpegPathResolver.Resolve(ffmpegPath, "ffmpeg");
            string? folder = Path.GetDirectoryName(resolved);
            if (!string.IsNullOrEmpty(folder))
            {
                GlobalFFOptions.Configure(options => options.BinaryFolder = folder);
            }
        }

        /// <inheritdoc />
        public async Task<FrameExtractionResult> ExtractFrameAsync(string videoFilePath, long frameOffsetMs, CancellationToken ct)
        {
            if (frameOffsetMs < 0)
            {
                return new FrameExtractionResult { Success = false, ErrorMessage = "frameOffsetMs must be >= 0." };
            }

            using var timeoutCts = new CancellationTokenSource(_timeout);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);
            var output = new CappedMemoryStream(_maxOutputBytes);

            try
            {
                _ = await FFMpegArguments
                    .FromFileInput(videoFilePath, verifyExists: true, options => options.Seek(TimeSpan.FromMilliseconds(frameOffsetMs)))
                    .OutputToPipe(new StreamPipeSink(output), options => options
                        .WithFrameOutputCount(1)
                        .ForceFormat("image2pipe")
                        .WithVideoCodec("png"))
                    .CancellableThrough(linkedCts.Token)
                    .ProcessAsynchronously(throwOnError: true);
            }
            catch (Exception ex) when (ex is OperationCanceledException || linkedCts.IsCancellationRequested)
            {
                string reason = ct.IsCancellationRequested
                    ? "ffmpeg extraction was cancelled."
                    : $"ffmpeg extraction timed out after {_timeout.TotalSeconds:0.#}s.";
                _logger.LogWarning(
                    "ffmpeg frame extraction for {Path} at {OffsetMs}ms did not complete in time: {Reason}",
                    videoFilePath, frameOffsetMs, reason);
                return new FrameExtractionResult { Success = false, ErrorMessage = reason };
            }
            catch (Exception ex)
            {
                if (output.Exceeded)
                {
                    _logger.LogWarning(
                        "ffmpeg frame extraction for {Path} at {OffsetMs}ms exceeded the maximum output size of {MaxBytes} bytes",
                        videoFilePath, frameOffsetMs, _maxOutputBytes);
                    return new FrameExtractionResult
                    {
                        Success = false,
                        ErrorMessage = $"ffmpeg output exceeded the maximum allowed size of {_maxOutputBytes} bytes."
                    };
                }

                _logger.LogWarning(ex, "ffmpeg frame extraction failed for {Path} at {OffsetMs}ms", videoFilePath, frameOffsetMs);
                return new FrameExtractionResult { Success = false, ErrorMessage = $"ffmpeg extraction failed: {ex.Message}" };
            }

            if (output.Length == 0)
            {
                _logger.LogWarning("ffmpeg frame extraction for {Path} at {OffsetMs}ms produced no output", videoFilePath, frameOffsetMs);
                return new FrameExtractionResult { Success = false, ErrorMessage = "ffmpeg produced no frame at this offset." };
            }

            return new FrameExtractionResult { Success = true, PngBytes = output.ToArray() };
        }

        /// <summary>A memory stream that refuses to grow past its cap, and records that it did.</summary>
        private sealed class CappedMemoryStream : MemoryStream
        {
            private readonly long _maxBytes;

            public CappedMemoryStream(long maxBytes)
            {
                _maxBytes = maxBytes;
            }

            /// <summary>True once a write was refused for passing the cap.</summary>
            public bool Exceeded { get; private set; }

            public override void Write(byte[] buffer, int offset, int count)
            {
                Check(count);
                base.Write(buffer, offset, count);
            }

            public override void Write(ReadOnlySpan<byte> buffer)
            {
                Check(buffer.Length);
                base.Write(buffer);
            }

            public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
                Check(count);
                return base.WriteAsync(buffer, offset, count, cancellationToken);
            }

            public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
            {
                Check(buffer.Length);
                return base.WriteAsync(buffer, cancellationToken);
            }

            private void Check(int incoming)
            {
                if (Length + incoming > _maxBytes)
                {
                    Exceeded = true;
                    throw new OutputTooLargeException($"ffmpeg output exceeded the maximum allowed size of {_maxBytes} bytes.");
                }
            }
        }
    }
}
