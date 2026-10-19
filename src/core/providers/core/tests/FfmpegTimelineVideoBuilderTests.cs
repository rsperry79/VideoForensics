using System.ComponentModel;
using System.Diagnostics;

using Microsoft.Extensions.Logging.Abstractions;

using VideoForensics.Core.Providers.Common.Contracts;

using Xunit;

namespace VideoForensics.Core.Providers.Core.Tests
{
    public class FfmpegTimelineVideoBuilderTests : IDisposable
    {
        private static readonly DateTimeOffset T0 = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        private readonly string _workDir = Path.Combine(Path.GetTempPath(), "timeline-tests-" + Guid.NewGuid().ToString("N"));

        public FfmpegTimelineVideoBuilderTests()
        {
            Directory.CreateDirectory(_workDir);
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(_workDir, recursive: true);
            }
            catch (IOException)
            {
                // Best-effort cleanup of the test directory.
            }
        }

        [Fact]
        public async Task BuildAsync_EmptyStillList_ThrowsArgumentException()
        {
            var builder = new FfmpegTimelineVideoBuilder(NullLogger<FfmpegTimelineVideoBuilder>.Instance);
            var request = new TimelineVideoRequest(Array.Empty<TimelineStill>(), Path.Combine(_workDir, "out.mp4"));

            await Assert.ThrowsAsync<ArgumentException>(() => builder.BuildAsync(request, CancellationToken.None));
        }

        [Fact]
        public async Task BuildAsync_MissingImageFile_ThrowsArgumentException()
        {
            var builder = new FfmpegTimelineVideoBuilder(NullLogger<FfmpegTimelineVideoBuilder>.Instance);
            var stills = new[] { new TimelineStill(Path.Combine(_workDir, "missing.png"), T0, TimeSpan.FromSeconds(1)) };
            var request = new TimelineVideoRequest(stills, Path.Combine(_workDir, "out.mp4"));

            await Assert.ThrowsAsync<ArgumentException>(() => builder.BuildAsync(request, CancellationToken.None));
        }

        [Fact]
        public async Task BuildAsync_ZeroHold_ThrowsArgumentException()
        {
            var builder = new FfmpegTimelineVideoBuilder(NullLogger<FfmpegTimelineVideoBuilder>.Instance);
            string image = WriteDummyFile("still.png");
            var stills = new[] { new TimelineStill(image, T0, TimeSpan.Zero) };
            var request = new TimelineVideoRequest(stills, Path.Combine(_workDir, "out.mp4"));

            await Assert.ThrowsAsync<ArgumentException>(() => builder.BuildAsync(request, CancellationToken.None));
        }

        [Fact]
        public async Task BuildAsync_ValidSyntheticStills_WritesMp4WithExpectedFrameCount()
        {
            if (!ToolAvailable("ffmpeg") || !ToolAvailable("ffprobe"))
            {
                return; // Real ffmpeg is required to make stills and probe the output.
            }

            string a = Path.Combine(_workDir, "a.png");
            string b = Path.Combine(_workDir, "b.png");
            string c = Path.Combine(_workDir, "c.png");
            CreatePng(a, 320, 240);
            CreatePng(b, 640, 480);
            CreatePng(c, 200, 300);

            // Holds 2s, 1.4s (rounds to 1s), 3s: expected frames = 2 + 1 + 3 = 6.
            var stills = new[]
            {
                new TimelineStill(a, T0, TimeSpan.FromSeconds(2)),
                new TimelineStill(b, T0.AddSeconds(2), TimeSpan.FromSeconds(1.4)),
                new TimelineStill(c, T0.AddSeconds(3.4), TimeSpan.FromSeconds(3)),
            };
            string output = Path.Combine(_workDir, "timeline.mp4");
            var builder = new FfmpegTimelineVideoBuilder(NullLogger<FfmpegTimelineVideoBuilder>.Instance);

            TimelineVideoResult result = await builder.BuildAsync(new TimelineVideoRequest(stills, output), CancellationToken.None);

            Assert.True(result.Success, result.ErrorMessage);
            Assert.Equal(output, result.OutputPath);
            Assert.Equal(6, result.FrameCount);
            Assert.True(File.Exists(output));
            Assert.Equal("6", ProbeStreamField(output, "nb_read_frames"));
            Assert.Equal("640", ProbeStreamField(output, "width"));
            Assert.Equal("480", ProbeStreamField(output, "height"));
            Assert.Equal("yuv420p", ProbeStreamField(output, "pix_fmt"));
        }

        [Fact]
        public async Task BuildAsync_FfmpegMissing_ReturnsFailureWithoutThrowing()
        {
            var builder = new FfmpegTimelineVideoBuilder(
                NullLogger<FfmpegTimelineVideoBuilder>.Instance,
                "nonexistent-ffmpeg-binary-xyz-timeline");
            string image = WriteDummyFile("still.png");
            var request = new TimelineVideoRequest(
                new[] { new TimelineStill(image, T0, TimeSpan.FromSeconds(1)) },
                Path.Combine(_workDir, "out.mp4"));

            TimelineVideoResult result = await builder.BuildAsync(request, CancellationToken.None);

            Assert.False(result.Success);
            Assert.NotNull(result.ErrorMessage);
        }

        [Fact]
        public async Task BuildAsync_Cancelled_ThrowsOperationCanceledException()
        {
            var builder = new FfmpegTimelineVideoBuilder(NullLogger<FfmpegTimelineVideoBuilder>.Instance);
            string image = WriteDummyFile("still.png");
            var request = new TimelineVideoRequest(
                new[] { new TimelineStill(image, T0, TimeSpan.FromSeconds(1)) },
                Path.Combine(_workDir, "out.mp4"));
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(request, cts.Token));
        }

        private string WriteDummyFile(string name)
        {
            string path = Path.Combine(_workDir, name);
            File.WriteAllBytes(path, new byte[] { 0x89, 0x50, 0x4E, 0x47 });
            return path;
        }

        private void CreatePng(string path, int width, int height)
        {
            RunTool("ffmpeg", "-f", "lavfi", "-i", $"color=c=red:s={width}x{height}", "-frames:v", "1", "-y", path);
        }

        private static string ProbeStreamField(string path, string entry)
        {
            return RunTool(
                "ffprobe",
                "-v", "error",
                "-count_frames",
                "-select_streams", "v:0",
                "-show_entries", $"stream={entry}",
                "-of", "csv=p=0",
                path).Trim();
        }

        private static bool ToolAvailable(string baseName)
        {
            try
            {
                using Process process = StartTool(baseName, "-version");
                process.StandardOutput.ReadToEnd();
                process.StandardError.ReadToEnd();
                process.WaitForExit();
                return process.ExitCode == 0;
            }
            catch (Win32Exception)
            {
                return false;
            }
        }

        private static string RunTool(string baseName, params string[] args)
        {
            using Process process = StartTool(baseName, args);
            string stdout = process.StandardOutput.ReadToEnd();
            string stderr = process.StandardError.ReadToEnd();
            process.WaitForExit();
            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException($"{baseName} exited {process.ExitCode}: {stderr}");
            }

            return stdout;
        }

        private static Process StartTool(string baseName, params string[] args)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = FfmpegPathResolver.Resolve(null, baseName),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            foreach (string arg in args)
            {
                startInfo.ArgumentList.Add(arg);
            }

            return Process.Start(startInfo) ?? throw new InvalidOperationException($"Could not start {baseName}.");
        }

        [Fact]
        public async Task BuildAsync_OutputPathIsAnInput_ThrowsAndLeavesTheInputUntouched()
        {
            // ffmpeg runs with -y, so an output that names an input would overwrite that input. It must be refused up front.
            string still = WriteDummyFile("evidence.png");
            byte[] before = File.ReadAllBytes(still);
            var builder = new FfmpegTimelineVideoBuilder(NullLogger<FfmpegTimelineVideoBuilder>.Instance);
            var request = new TimelineVideoRequest(new[] { new TimelineStill(still, T0, TimeSpan.FromSeconds(1)) }, still);

            _ = await Assert.ThrowsAsync<ArgumentException>(() => builder.BuildAsync(request, CancellationToken.None));

            Assert.Equal(before, File.ReadAllBytes(still));
        }

        [Fact]
        public void ProcessTimeout_LongerOutput_GetsMoreBudgetThanTheBase()
        {
            var baseline = TimeSpan.FromSeconds(120);

            Assert.Equal(baseline, FfmpegTimelineVideoBuilder.ProcessTimeout(baseline, 0));
            Assert.True(FfmpegTimelineVideoBuilder.ProcessTimeout(baseline, 600) > TimeSpan.FromSeconds(600));
        }
    }
}
