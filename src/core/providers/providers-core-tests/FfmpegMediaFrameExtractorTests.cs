using Microsoft.Extensions.Logging.Abstractions;

using Xunit;

namespace VideoForensics.Providers.Core.Tests
{
    public class FfmpegMediaFrameExtractorTests : IDisposable
    {
        private readonly List<string> _tempScripts = new();

        [Fact]
        public async Task ExtractFrameAsync_NegativeOffset_ReturnsFailureWithoutRunningProcess()
        {
            var extractor = new FfmpegMediaFrameExtractor(NullLogger<FfmpegMediaFrameExtractor>.Instance, "nonexistent-ffmpeg-binary-xyz");

            FrameExtractionResult result = await extractor.ExtractFrameAsync("/videos/test.mp4", -1, CancellationToken.None);

            Assert.False(result.Success);
            Assert.NotNull(result.ErrorMessage);
        }

        [Fact]
        public async Task ExtractFrameAsync_FfmpegBinaryMissing_ReturnsFailureResult()
        {
            var extractor = new FfmpegMediaFrameExtractor(NullLogger<FfmpegMediaFrameExtractor>.Instance, "nonexistent-ffmpeg-binary-xyz-test");

            FrameExtractionResult result = await extractor.ExtractFrameAsync("/videos/test.mp4", 1000, CancellationToken.None);

            Assert.False(result.Success);
            Assert.NotNull(result.ErrorMessage);
            Assert.Null(result.PngBytes);
        }

        [Fact]
        public async Task ExtractFrameAsync_ProcessExitsZeroWithOutput_ReturnsSuccessWithBytes()
        {
            if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
            {
                return; // Fake shell-script ffmpeg stand-in only works on POSIX shells.
            }

            string scriptPath = CreateFakeFfmpegScript(exitCode: 0, stdoutBytes: new byte[] { 0x89, 0x50, 0x4E, 0x47, 1, 2, 3 });
            var extractor = new FfmpegMediaFrameExtractor(NullLogger<FfmpegMediaFrameExtractor>.Instance, scriptPath);

            FrameExtractionResult result = await extractor.ExtractFrameAsync("/videos/test.mp4", 2500, CancellationToken.None);

            Assert.True(result.Success);
            Assert.NotNull(result.PngBytes);
            Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47, 1, 2, 3 }, result.PngBytes);
        }

        [Fact]
        public async Task ExtractFrameAsync_ProcessExitsNonZero_ReturnsFailureResult()
        {
            if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
            {
                return;
            }

            string scriptPath = CreateFakeFfmpegScript(exitCode: 1, stdoutBytes: Array.Empty<byte>());
            var extractor = new FfmpegMediaFrameExtractor(NullLogger<FfmpegMediaFrameExtractor>.Instance, scriptPath);

            FrameExtractionResult result = await extractor.ExtractFrameAsync("/videos/test.mp4", 500, CancellationToken.None);

            Assert.False(result.Success);
            Assert.NotNull(result.ErrorMessage);
        }

        [Fact]
        public async Task ExtractFrameAsync_ProcessExceedsTimeout_KillsProcessAndReturnsFailureQuickly()
        {
            if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
            {
                return;
            }

            // The fake "ffmpeg" sleeps far longer than the configured timeout; a healthy
            // implementation kills it and returns well before the sleep would otherwise finish.
            string pidFile = Path.Combine(Path.GetTempPath(), $"fake-ffmpeg-pid-{Guid.NewGuid():N}.txt");
            _tempScripts.Add(pidFile);
            string scriptPath = CreateSleepingFfmpegScript(sleepSeconds: 30, pidFile: pidFile);
            var extractor = new FfmpegMediaFrameExtractor(NullLogger<FfmpegMediaFrameExtractor>.Instance, scriptPath, timeout: TimeSpan.FromMilliseconds(300));

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            FrameExtractionResult result = await extractor.ExtractFrameAsync("/videos/test.mp4", 1000, CancellationToken.None);
            stopwatch.Stop();

            Assert.False(result.Success);
            Assert.Contains("timed out", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
            Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(10), $"Expected the timeout to short-circuit the 30s sleep; took {stopwatch.Elapsed}.");

            // The spawned process must actually be gone (not orphaned holding the file open).
            await AssertProcessIsGoneAsync(pidFile);
        }

        [Fact]
        public async Task ExtractFrameAsync_CallerCancels_KillsProcessAndReturnsFailureQuickly()
        {
            if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
            {
                return;
            }

            string pidFile = Path.Combine(Path.GetTempPath(), $"fake-ffmpeg-pid-{Guid.NewGuid():N}.txt");
            _tempScripts.Add(pidFile);
            string scriptPath = CreateSleepingFfmpegScript(sleepSeconds: 30, pidFile: pidFile);
            // A long timeout, so the caller's own cancellation - not the timeout - is what fires.
            var extractor = new FfmpegMediaFrameExtractor(NullLogger<FfmpegMediaFrameExtractor>.Instance, scriptPath, timeout: TimeSpan.FromMinutes(5));

            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            FrameExtractionResult result = await extractor.ExtractFrameAsync("/videos/test.mp4", 1000, cts.Token);
            stopwatch.Stop();

            Assert.False(result.Success);
            Assert.Contains("cancel", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
            Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(10), $"Expected cancellation to short-circuit the 30s sleep; took {stopwatch.Elapsed}.");

            await AssertProcessIsGoneAsync(pidFile);
        }

        [Fact]
        public async Task ExtractFrameAsync_OutputExceedsMaxSize_KillsProcessAndReturnsFailure()
        {
            if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
            {
                return;
            }

            // The fake "ffmpeg" streams far more than the tiny configured cap.
            string scriptPath = CreateHugeOutputFfmpegScript(totalBytes: 1_000_000);
            var extractor = new FfmpegMediaFrameExtractor(
                NullLogger<FfmpegMediaFrameExtractor>.Instance, scriptPath, timeout: TimeSpan.FromSeconds(30), maxOutputBytes: 1024);

            FrameExtractionResult result = await extractor.ExtractFrameAsync("/videos/test.mp4", 1000, CancellationToken.None);

            Assert.False(result.Success);
            Assert.Contains("exceeded", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
            Assert.Null(result.PngBytes);
        }

        [Fact]
        public async Task ExtractFrameAsync_OutputWithinCap_StillSucceeds()
        {
            if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
            {
                return;
            }

            string scriptPath = CreateFakeFfmpegScript(exitCode: 0, stdoutBytes: new byte[] { 0x89, 0x50, 0x4E, 0x47, 1, 2, 3 });
            var extractor = new FfmpegMediaFrameExtractor(
                NullLogger<FfmpegMediaFrameExtractor>.Instance, scriptPath, timeout: TimeSpan.FromSeconds(30), maxOutputBytes: 1024);

            FrameExtractionResult result = await extractor.ExtractFrameAsync("/videos/test.mp4", 1000, CancellationToken.None);

            Assert.True(result.Success);
            Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47, 1, 2, 3 }, result.PngBytes);
        }

        /// <summary>Writes the shell's own PID to <paramref name="pidFile"/>, then sleeps - lets tests verify the spawned process was actually killed, not merely that our call returned.</summary>
        private string CreateSleepingFfmpegScript(int sleepSeconds, string pidFile)
        {
            string scriptPath = Path.Combine(Path.GetTempPath(), $"fake-ffmpeg-sleep-{Guid.NewGuid():N}.sh");
            string script = $"#!/bin/sh\necho $$ > '{pidFile}'\nsleep {sleepSeconds}\nexit 0\n";
            WriteExecutableScript(scriptPath, script);
            _tempScripts.Add(scriptPath);
            return scriptPath;
        }

        private string CreateHugeOutputFfmpegScript(int totalBytes)
        {
            string scriptPath = Path.Combine(Path.GetTempPath(), $"fake-ffmpeg-huge-{Guid.NewGuid():N}.sh");
            string script = $"#!/bin/sh\nhead -c {totalBytes} /dev/zero\nexit 0\n";
            WriteExecutableScript(scriptPath, script);
            _tempScripts.Add(scriptPath);
            return scriptPath;
        }

        /// <summary>Polls briefly for the PID recorded by CreateSleepingFfmpegScript to no longer exist as a running process.</summary>
        private static async Task AssertProcessIsGoneAsync(string pidFile)
        {
            for (int attempt = 0; attempt < 20; attempt++)
            {
                if (!File.Exists(pidFile))
                {
                    await Task.Delay(50);
                    continue;
                }

                string pidText = (await File.ReadAllTextAsync(pidFile)).Trim();
                if (int.TryParse(pidText, out int pid))
                {
                    try
                    {
                        using var proc = System.Diagnostics.Process.GetProcessById(pid);
                        if (proc.HasExited)
                        {
                            return;
                        }
                    }
                    catch (ArgumentException)
                    {
                        // Process no longer exists - it was killed.
                        return;
                    }
                }

                await Task.Delay(100);
            }

            Assert.Fail("Expected the spawned fake-ffmpeg process to have been killed, but it is still running.");
        }

        private string CreateFakeFfmpegScript(int exitCode, byte[] stdoutBytes)
        {
            string scriptPath = Path.Combine(Path.GetTempPath(), $"fake-ffmpeg-{Guid.NewGuid():N}.sh");
            string base64 = Convert.ToBase64String(stdoutBytes);
            string script = $"#!/bin/sh\necho '{base64}' | base64 -d\nexit {exitCode}\n";
            WriteExecutableScript(scriptPath, script);
            _tempScripts.Add(scriptPath);
            return scriptPath;
        }

        private static void WriteExecutableScript(string scriptPath, string script)
        {
            File.WriteAllText(scriptPath, script);
            if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
            {
                File.SetUnixFileMode(scriptPath,
                    UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                    UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                    UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
            }
        }

        public void Dispose()
        {
            foreach (string path in _tempScripts)
            {
                try { File.Delete(path); } catch { }
            }
        }
    }
}
