using System.Text;

using FFMpegCore;

namespace VideoForensics.Core.Providers.Core
{
    /// <summary>
    /// Runs an FFMpegCore processor and captures what it prints. The analysis filters write their results to stdout or stderr,
    /// so these are collected line by line. Returns success only for exit code 0; a timeout or a failed start is a plain failure.
    /// Cancellation by the caller is rethrown.
    /// </summary>
    public static class FfmpegCapture
    {
        /// <summary>The captured run: whether ffmpeg exited cleanly, and every stdout and stderr line it printed.</summary>
        public sealed record CaptureResult(bool Success, string Stdout, string Stderr);

        public static async Task<CaptureResult> RunAsync(FFMpegArgumentProcessor processor, TimeSpan timeout, CancellationToken ct)
        {
            var stdout = new StringBuilder();
            var stderr = new StringBuilder();
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(timeout);

            try
            {
                bool success = await processor
                    .NotifyOnOutput(line => stdout.AppendLine(line))
                    .NotifyOnError(line => stderr.AppendLine(line))
                    .CancellableThrough(timeoutCts.Token)
                    .ProcessAsynchronously(throwOnError: false);
                return new CaptureResult(success, stdout.ToString(), stderr.ToString());
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception)
            {
                return new CaptureResult(false, stdout.ToString(), stderr.ToString());
            }
        }
    }
}
