using System.Diagnostics;
using VideoForensics.Core.Providers.Core;
using Xunit;

namespace VideoForensics.Core.Providers.Core.Tests
{
    public class ProcessRunnerTests
    {
        [Fact]
        public async Task RunAsync_ProcessExceedsTimeout_ThrowsTimeoutAndStopsProcess()
        {
            var psi = new ProcessStartInfo { FileName = "ping", RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
            psi.ArgumentList.Add("-n");
            psi.ArgumentList.Add("60");
            psi.ArgumentList.Add("127.0.0.1");

            var started = Stopwatch.StartNew();
            _ = await Assert.ThrowsAsync<TimeoutException>(() => ProcessRunner.RunAsync(psi, TimeSpan.FromMilliseconds(300), CancellationToken.None));

            // A 60-ping run takes about a minute; returning quickly shows the process was killed, not waited on.
            Assert.True(started.Elapsed < TimeSpan.FromSeconds(20));
        }

        [Fact]
        public async Task RunAsync_ShortProcess_ReturnsExitCodeAndOutput()
        {
            var psi = new ProcessStartInfo { FileName = "cmd", RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
            psi.ArgumentList.Add("/c");
            psi.ArgumentList.Add("echo hello");

            ProcessResult result = await ProcessRunner.RunAsync(psi, TimeSpan.FromSeconds(30), CancellationToken.None);

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("hello", result.StandardOutput);
        }
    }
}
