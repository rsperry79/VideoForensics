using System.ComponentModel;
using System.Diagnostics;

using Microsoft.Extensions.Logging.Abstractions;

using VideoForensics.Core.Providers.Common.Contracts;

using Xunit;

namespace VideoForensics.Core.Providers.Core.Tests
{
    public class FfmpegTimelineVideoBuilderClipTests : IDisposable
    {
        private static readonly DateTimeOffset T0 = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        private readonly string _workDir = Path.Combine(Path.GetTempPath(), "timeline-clip-tests-" + Guid.NewGuid().ToString("N"));

        public FfmpegTimelineVideoBuilderClipTests()
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
        public async Task BuildAsync_ClipCoversStillWindow_ReplacesStillFrames()
        {
            if (!ToolAvailable("ffmpeg") || !ToolAvailable("ffprobe"))
            {
                return; // Real ffmpeg is required to make stills and clips and probe the output.
            }

            string red = Path.Combine(_workDir, "red.png");
            string blue = Path.Combine(_workDir, "blue.png");
            string clip = Path.Combine(_workDir, "clip.mp4");
            CreatePng(red, "red", 320, 240);
            CreatePng(blue, "blue", 320, 240);
            CreateVideo(clip, 320, 240, seconds: 2);

            // Still 1: [T0, T0+3). Still 2: [T0+3, T0+6), with a 2 s clip covering [T0+3, T0+5).
            // Expected: 3 s red + 2 s clip + 1 s blue tail = 6 frames.
            var stills = new[]
            {
                new TimelineStill(red, T0, TimeSpan.FromSeconds(3)),
                new TimelineStill(blue, T0.AddSeconds(3), TimeSpan.FromSeconds(3)),
            };
            var clips = new[] { new TimelineClip(clip, T0.AddSeconds(3), TimeSpan.FromSeconds(2)) };
            string output = Path.Combine(_workDir, "timeline.mp4");
            var builder = new FfmpegTimelineVideoBuilder(NullLogger<FfmpegTimelineVideoBuilder>.Instance);

            TimelineVideoResult result = await builder.BuildAsync(new TimelineVideoRequest(stills, output, clips), CancellationToken.None);

            Assert.True(result.Success, result.ErrorMessage);
            Assert.Equal(output, result.OutputPath);
            Assert.Equal(6, result.FrameCount);
            Assert.Equal("6", ProbeStreamField(output, "nb_read_frames"));
            Assert.Equal("320", ProbeStreamField(output, "width"));
            Assert.Equal("240", ProbeStreamField(output, "height"));

            // Frame counts alone cannot tell a clip from a still, so check content: frames 0-2 are red,
            // frames 3-4 are the clip (not blue), and frame 5 is the blue tail.
            List<(byte R, byte G, byte B)> frames = ReadFrameAverages(output);
            Assert.Equal(6, frames.Count);
            Assert.True(IsRed(frames[0]), $"frame 0 should be the red still, was {frames[0]}");
            Assert.False(IsBlue(frames[3]), $"frame 3 should be the clip, not the blue still, was {frames[3]}");
            Assert.False(IsBlue(frames[4]), $"frame 4 should be the clip, not the blue still, was {frames[4]}");
            Assert.True(IsBlue(frames[5]), $"frame 5 should be the blue tail, was {frames[5]}");
        }

        [Fact]
        public async Task BuildAsync_ClipInsideStill_SplitsStill()
        {
            if (!ToolAvailable("ffmpeg") || !ToolAvailable("ffprobe"))
            {
                return;
            }

            string still = Path.Combine(_workDir, "still.png");
            string clip = Path.Combine(_workDir, "clip.mp4");
            CreatePng(still, "red", 320, 240);
            CreateVideo(clip, 320, 240, seconds: 2);

            // One 5 s still [T0, T0+5) with a 2 s clip at [T0+1, T0+3): 1 s still + 2 s clip + 2 s still = 5 frames.
            var stills = new[] { new TimelineStill(still, T0, TimeSpan.FromSeconds(5)) };
            var clips = new[] { new TimelineClip(clip, T0.AddSeconds(1), TimeSpan.FromSeconds(2)) };
            string output = Path.Combine(_workDir, "timeline.mp4");
            var builder = new FfmpegTimelineVideoBuilder(NullLogger<FfmpegTimelineVideoBuilder>.Instance);

            TimelineVideoResult result = await builder.BuildAsync(new TimelineVideoRequest(stills, output, clips), CancellationToken.None);

            Assert.True(result.Success, result.ErrorMessage);
            Assert.Equal(5, result.FrameCount);
            Assert.Equal("5", ProbeStreamField(output, "nb_read_frames"));

            // Frames 0 and 3-4 are the red still; frames 1-2 must be the clip, not red.
            List<(byte R, byte G, byte B)> frames = ReadFrameAverages(output);
            Assert.Equal(5, frames.Count);
            Assert.True(IsRed(frames[0]), $"frame 0 should be the red still, was {frames[0]}");
            Assert.False(IsRed(frames[1]), $"frame 1 should be the clip, not the still, was {frames[1]}");
            Assert.False(IsRed(frames[2]), $"frame 2 should be the clip, not the still, was {frames[2]}");
            Assert.True(IsRed(frames[3]), $"frame 3 should be the still tail, was {frames[3]}");
        }

        [Fact]
        public async Task BuildAsync_ClipWithoutVideoStream_ReturnsFailureWithoutThrowing()
        {
            if (!ToolAvailable("ffmpeg") || !ToolAvailable("ffprobe"))
            {
                return;
            }

            string still = Path.Combine(_workDir, "still.png");
            CreatePng(still, "red", 320, 240);
            string notVideo = Path.Combine(_workDir, "notes.txt");
            File.WriteAllText(notVideo, "this is not a video file");

            var stills = new[] { new TimelineStill(still, T0, TimeSpan.FromSeconds(3)) };
            var clips = new[] { new TimelineClip(notVideo, T0.AddSeconds(1), TimeSpan.FromSeconds(1)) };
            var builder = new FfmpegTimelineVideoBuilder(NullLogger<FfmpegTimelineVideoBuilder>.Instance);

            TimelineVideoResult result = await builder.BuildAsync(
                new TimelineVideoRequest(stills, Path.Combine(_workDir, "out.mp4"), clips), CancellationToken.None);

            Assert.False(result.Success);
            Assert.NotNull(result.ErrorMessage);
        }

        [Fact]
        public async Task BuildAsync_ClipMissingFile_ThrowsArgumentException()
        {
            var builder = new FfmpegTimelineVideoBuilder(NullLogger<FfmpegTimelineVideoBuilder>.Instance);
            string still = WriteDummyFile("still.png");
            var stills = new[] { new TimelineStill(still, T0, TimeSpan.FromSeconds(1)) };
            var clips = new[] { new TimelineClip(Path.Combine(_workDir, "missing.mp4"), T0, TimeSpan.FromSeconds(1)) };
            var request = new TimelineVideoRequest(stills, Path.Combine(_workDir, "out.mp4"), clips);

            await Assert.ThrowsAsync<ArgumentException>(() => builder.BuildAsync(request, CancellationToken.None));
        }

        [Fact]
        public async Task BuildAsync_ClipZeroDuration_ThrowsArgumentException()
        {
            var builder = new FfmpegTimelineVideoBuilder(NullLogger<FfmpegTimelineVideoBuilder>.Instance);
            string still = WriteDummyFile("still.png");
            string clipFile = WriteDummyFile("clip.mp4");
            var stills = new[] { new TimelineStill(still, T0, TimeSpan.FromSeconds(1)) };
            var clips = new[] { new TimelineClip(clipFile, T0, TimeSpan.Zero) };
            var request = new TimelineVideoRequest(stills, Path.Combine(_workDir, "out.mp4"), clips);

            await Assert.ThrowsAsync<ArgumentException>(() => builder.BuildAsync(request, CancellationToken.None));
        }

        private string WriteDummyFile(string name)
        {
            string path = Path.Combine(_workDir, name);
            File.WriteAllBytes(path, new byte[] { 0x89, 0x50, 0x4E, 0x47 });
            return path;
        }

        private void CreatePng(string path, string color, int width, int height)
        {
            RunTool("ffmpeg", "-f", "lavfi", "-i", $"color=c={color}:s={width}x{height}", "-frames:v", "1", "-y", path);
        }

        private void CreateVideo(string path, int width, int height, int seconds)
        {
            RunTool(
                "ffmpeg",
                "-f", "lavfi", "-i", $"testsrc=size={width}x{height}:rate=2",
                "-t", seconds.ToString(System.Globalization.CultureInfo.InvariantCulture),
                "-y", path);
        }

        /// <summary>Decodes every frame to a single averaged RGB pixel, so each frame's colour can be checked.</summary>
        private static List<(byte R, byte G, byte B)> ReadFrameAverages(string path)
        {
            byte[] raw = RunToolBinary(
                "ffmpeg",
                "-v", "error",
                "-i", path,
                "-vf", "scale=1:1,format=rgb24",
                "-f", "rawvideo",
                "-");
            var frames = new List<(byte R, byte G, byte B)>();
            for (int i = 0; i + 2 < raw.Length; i += 3)
            {
                frames.Add((raw[i], raw[i + 1], raw[i + 2]));
            }

            return frames;
        }

        // Thresholds are loose on purpose: the stills are solid colours that survive H.264 with small drift.
        private static bool IsRed((byte R, byte G, byte B) c) => c.R > 200 && c.G < 60 && c.B < 60;

        private static bool IsBlue((byte R, byte G, byte B) c) => c.B > 200 && c.R < 60 && c.G < 60;

        private static byte[] RunToolBinary(string baseName, params string[] args)
        {
            using Process process = StartTool(baseName, args);
            // Read stderr asynchronously: ffmpeg writes progress there, and a full pipe would deadlock a blocking stdout read.
            Task<string> stderrTask = process.StandardError.ReadToEndAsync();
            using var buffer = new MemoryStream();
            process.StandardOutput.BaseStream.CopyTo(buffer);
            string stderr = stderrTask.GetAwaiter().GetResult();
            process.WaitForExit();
            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException($"{baseName} exited {process.ExitCode}: {stderr}");
            }

            return buffer.ToArray();
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
            // Read stderr asynchronously so a full stderr pipe cannot deadlock the stdout read.
            Task<string> stderrTask = process.StandardError.ReadToEndAsync();
            string stdout = process.StandardOutput.ReadToEnd();
            string stderr = stderrTask.GetAwaiter().GetResult();
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
    }
}
