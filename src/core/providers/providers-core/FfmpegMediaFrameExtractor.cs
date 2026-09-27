using System.Diagnostics;

using Microsoft.Extensions.Logging;

namespace VideoForensics.Providers.Core
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

    /// <summary>Shells out to ffmpeg to extract a single frame, piping PNG bytes back over stdout (no temp file needed).</summary>
    public class FfmpegMediaFrameExtractor : IMediaFrameExtractor
    {
        /// <summary>Default wall-clock budget for a single extraction before the ffmpeg process is killed.</summary>
        public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);

        /// <summary>Default cap on ffmpeg's stdout, so a pathological/hostile input file can't exhaust memory.</summary>
        public const long DefaultMaxOutputBytes = 50 * 1024 * 1024; // 50 MB

        private readonly string _ffmpegPath;
        private readonly ILogger<FfmpegMediaFrameExtractor> _logger;
        private readonly TimeSpan _timeout;
        private readonly long _maxOutputBytes;

        public FfmpegMediaFrameExtractor(
            ILogger<FfmpegMediaFrameExtractor> logger,
            string? ffmpegPath = null,
            TimeSpan? timeout = null,
            long? maxOutputBytes = null)
        {
            _logger = logger;
            _ffmpegPath = FfmpegPathResolver.Resolve(ffmpegPath, "ffmpeg");
            _timeout = timeout ?? DefaultTimeout;
            _maxOutputBytes = maxOutputBytes ?? DefaultMaxOutputBytes;
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
            CancellationToken linkedToken = linkedCts.Token;

            Process? process = null;
            try
            {
                var processInfo = new ProcessStartInfo
                {
                    FileName = _ffmpegPath,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                // -ss before -i seeks quickly (input seeking); -vframes 1 extracts exactly one
                // frame; image2pipe + png over pipe:1 avoids ever writing a temp file to disk.
                processInfo.ArgumentList.Add("-ss");
                processInfo.ArgumentList.Add(FormatTimestamp(frameOffsetMs));
                processInfo.ArgumentList.Add("-i");
                processInfo.ArgumentList.Add(videoFilePath);
                processInfo.ArgumentList.Add("-vframes");
                processInfo.ArgumentList.Add("1");
                processInfo.ArgumentList.Add("-f");
                processInfo.ArgumentList.Add("image2pipe");
                processInfo.ArgumentList.Add("-vcodec");
                processInfo.ArgumentList.Add("png");
                processInfo.ArgumentList.Add("pipe:1");

                process = Process.Start(processInfo);
                if (process == null)
                {
                    return new FrameExtractionResult { Success = false, ErrorMessage = "Failed to start ffmpeg process." };
                }

                using var stdout = new MemoryStream();
                Task readStdoutTask = ReadBoundedAsync(process.StandardOutput.BaseStream, stdout, _maxOutputBytes, linkedToken);
                Task<string> readStderrTask = process.StandardError.ReadToEndAsync(linkedToken);

                try
                {
                    await Task.WhenAll(readStdoutTask, readStderrTask);
                    await process.WaitForExitAsync(linkedToken);
                }
                catch (OperationCanceledException) when (linkedToken.IsCancellationRequested)
                {
                    KillProcessSafely(process, videoFilePath);

                    bool wasCallerCancellation = ct.IsCancellationRequested;
                    string reason = wasCallerCancellation
                        ? "ffmpeg extraction was cancelled."
                        : $"ffmpeg extraction timed out after {_timeout.TotalSeconds:0.#}s.";
                    _logger.LogWarning(
                        "ffmpeg frame extraction for {Path} at {OffsetMs}ms did not complete in time: {Reason}",
                        videoFilePath, frameOffsetMs, reason);
                    return new FrameExtractionResult { Success = false, ErrorMessage = reason };
                }
                catch (OutputTooLargeException ex)
                {
                    KillProcessSafely(process, videoFilePath);
                    _logger.LogWarning(
                        "ffmpeg frame extraction for {Path} at {OffsetMs}ms exceeded the maximum output size of {MaxBytes} bytes",
                        videoFilePath, frameOffsetMs, _maxOutputBytes);
                    return new FrameExtractionResult { Success = false, ErrorMessage = ex.Message };
                }

                if (process.ExitCode != 0 || stdout.Length == 0)
                {
                    string stderr = readStderrTask.IsCompletedSuccessfully ? readStderrTask.Result : string.Empty;
                    _logger.LogWarning(
                        "ffmpeg frame extraction failed for {Path} at {OffsetMs}ms, exit code {Code}: {Stderr}",
                        videoFilePath, frameOffsetMs, process.ExitCode, stderr);
                    return new FrameExtractionResult
                    {
                        Success = false,
                        ErrorMessage = $"ffmpeg exited with code {process.ExitCode}."
                    };
                }

                return new FrameExtractionResult { Success = true, PngBytes = stdout.ToArray() };
            }
            catch (Exception ex)
            {
                if (process is not null)
                {
                    KillProcessSafely(process, videoFilePath);
                }

                _logger.LogWarning(ex, "Exception extracting frame from {Path} at {OffsetMs}ms", videoFilePath, frameOffsetMs);
                return new FrameExtractionResult { Success = false, ErrorMessage = ex.Message };
            }
            finally
            {
                process?.Dispose();
            }
        }

        /// <summary>
        /// Copies <paramref name="source"/> into <paramref name="destination"/> in bounded chunks,
        /// throwing <see cref="OutputTooLargeException"/> as soon as <paramref name="maxBytes"/> would
        /// be exceeded, so a pathological/hostile process can never grow the buffer without limit.
        /// </summary>
        private static async Task ReadBoundedAsync(Stream source, MemoryStream destination, long maxBytes, CancellationToken ct)
        {
            byte[] buffer = new byte[81920];
            long total = 0;
            int bytesRead;
            while ((bytesRead = await source.ReadAsync(buffer, 0, buffer.Length, ct)) > 0)
            {
                total += bytesRead;
                if (total > maxBytes)
                {
                    throw new OutputTooLargeException($"ffmpeg output exceeded the maximum allowed size of {maxBytes} bytes.");
                }

                await destination.WriteAsync(buffer, 0, bytesRead, ct);
            }
        }

        /// <summary>Kills the process (and its whole tree) if it's still running, swallowing and logging any failure to do so.</summary>
        private void KillProcessSafely(Process process, string videoFilePath)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to kill ffmpeg process for {Path} after timeout/cancellation/output overflow", videoFilePath);
            }
        }

        private static string FormatTimestamp(long timestampMs)
        {
            long seconds = timestampMs / 1000;
            long milliseconds = timestampMs % 1000;
            long hours = seconds / 3600;
            long minutes = seconds % 3600 / 60;
            long secs = seconds % 60;

            return $"{hours:D2}:{minutes:D2}:{secs:D2}.{milliseconds:D3}";
        }
    }
}
