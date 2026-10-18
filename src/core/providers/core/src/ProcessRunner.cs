using System.Diagnostics;

namespace VideoForensics.Core.Providers.Core
{
    /// <summary>Output and exit code of a finished process.</summary>
    public sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);

    /// <summary>
    /// Runs an external tool with a time limit. If the time runs out or the caller cancels, the whole process tree is killed,
    /// so a hung ffmpeg cannot outlive the download that started it.
    /// </summary>
    public static class ProcessRunner
    {
        /// <summary>Time limit for one ffmpeg or ffprobe analysis of a downloaded clip.</summary>
        public static readonly TimeSpan DefaultAnalysisTimeout = TimeSpan.FromSeconds(120);

        /// <summary>Runs the process and returns its output.</summary>
        /// <exception cref="TimeoutException">The process was still running after <paramref name="timeout"/>; it has been killed.</exception>
        /// <exception cref="OperationCanceledException">The caller cancelled; the process has been killed.</exception>
        public static async Task<ProcessResult> RunAsync(ProcessStartInfo startInfo, TimeSpan timeout, CancellationToken ct)
        {
            using var limit = CancellationTokenSource.CreateLinkedTokenSource(ct);
            limit.CancelAfter(timeout);

            using Process process = Process.Start(startInfo) ?? throw new InvalidOperationException($"Could not start {startInfo.FileName}.");
            using CancellationTokenRegistration kill = limit.Token.Register(() => KillTree(process));

            Task<string> stdout = process.StandardOutput.ReadToEndAsync(CancellationToken.None);
            Task<string> stderr = process.StandardError.ReadToEndAsync(CancellationToken.None);
            try
            {
                await process.WaitForExitAsync(CancellationToken.None).WaitAsync(limit.Token);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                throw new TimeoutException($"{startInfo.FileName} did not finish within {timeout.TotalSeconds:0} s and was stopped.");
            }

            // Why: the kill can make the process exit before WaitAsync observes the cancellation, so the wait
            // may return normally even though the limit fired. Treat that as a timeout (or caller cancel), not success.
            if (limit.IsCancellationRequested)
            {
                ct.ThrowIfCancellationRequested();
                throw new TimeoutException($"{startInfo.FileName} did not finish within {timeout.TotalSeconds:0} s and was stopped.");
            }

            return new ProcessResult(process.ExitCode, await stdout, await stderr);
        }

        private static void KillTree(Process process)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch (InvalidOperationException)
            {
                // The process exited between the check and the kill.
            }
        }
    }
}
