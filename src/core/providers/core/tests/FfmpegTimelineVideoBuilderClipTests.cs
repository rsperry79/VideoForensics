using Microsoft.Extensions.Logging.Abstractions;

using System.ComponentModel;
using System.Diagnostics;

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
            _ = Directory.CreateDirectory(_workDir);
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
            TimelineStill[] stills = new[]
            {
                new TimelineStill(red, T0, TimeSpan.FromSeconds(3)),
                new TimelineStill(blue, T0.AddSeconds(3), TimeSpan.FromSeconds(3)),
            };
            TimelineClip[] clips = new[] { new TimelineClip(clip, T0.AddSeconds(3), TimeSpan.FromSeconds(2)) };
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
            TimelineStill[] stills = new[] { new TimelineStill(still, T0, TimeSpan.FromSeconds(5)) };
            TimelineClip[] clips = new[] { new TimelineClip(clip, T0.AddSeconds(1), TimeSpan.FromSeconds(2)) };
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
        public async Task BuildAsync_ClipShorterThanOneSecondWindow_StillGetsOneFrame()
        {
            if (!ToolAvailable("ffmpeg") || !ToolAvailable("ffprobe"))
            {
                return;
            }

            string red = Path.Combine(_workDir, "red.png");
            string clip = Path.Combine(_workDir, "clip.mp4");
            CreatePng(red, "red", 320, 240);
            CreateVideo(clip, 320, 240, seconds: 2);

            // Still [T0, T0+3) red. A 0.4 s clip window at T0+1 rounds to 0 s, but clip windows use the same
            // rounding as stills (minimum 1 s), so it contributes 1 frame. The cut-off still remainder
            // [T0+1.4, T0+3) is 1.6 s raw and rounds to 2 s. Expected: 1 red + 1 clip + 2 red = 4 frames.
            TimelineStill[] stills = new[] { new TimelineStill(red, T0, TimeSpan.FromSeconds(3)) };
            TimelineClip[] clips = new[] { new TimelineClip(clip, T0.AddSeconds(1), TimeSpan.FromSeconds(0.4)) };
            string output = Path.Combine(_workDir, "timeline.mp4");
            var builder = new FfmpegTimelineVideoBuilder(NullLogger<FfmpegTimelineVideoBuilder>.Instance);

            TimelineVideoResult result = await builder.BuildAsync(new TimelineVideoRequest(stills, output, clips), CancellationToken.None);

            Assert.True(result.Success, result.ErrorMessage);
            Assert.Equal(4, result.FrameCount);
            Assert.Equal("4", ProbeStreamField(output, "nb_read_frames"));

            List<(byte R, byte G, byte B)> frames = ReadFrameAverages(output);
            Assert.Equal(4, frames.Count);
            Assert.True(IsRed(frames[0]), $"frame 0 should be the red still, was {frames[0]}");
            Assert.False(IsRed(frames[1]), $"frame 1 should be the 1-frame clip, not the still, was {frames[1]}");
            Assert.True(IsRed(frames[2]), $"frame 2 should be the still remainder, was {frames[2]}");
            Assert.True(IsRed(frames[3]), $"frame 3 should be the still remainder, was {frames[3]}");
        }

        [Fact]
        public async Task BuildAsync_ClipDeclaredLongerThanRealVideo_FrameCountMatchesWrittenFrames()
        {
            if (!ToolAvailable("ffmpeg") || !ToolAvailable("ffprobe"))
            {
                return;
            }

            string still = Path.Combine(_workDir, "still.png");
            string clip = Path.Combine(_workDir, "clip.mp4");
            CreatePng(still, "red", 320, 240);
            CreateVideo(clip, 320, 240, seconds: 2);

            // A 2 s real clip declared with Duration 5 s, over a 5 s still starting at the same time.
            // Usable clip seconds = min(declared 5, real 2) = 2, so the clip contributes 2 frames. The declared
            // window covers the whole still, so the still has no remainder. Real video ends at 2 s and the rest of
            // the declared window is a gap, which stays unfilled. Expected: 2 frames, equal to the frames written.
            TimelineStill[] stills = new[] { new TimelineStill(still, T0, TimeSpan.FromSeconds(5)) };
            TimelineClip[] clips = new[] { new TimelineClip(clip, T0, TimeSpan.FromSeconds(5)) };
            string output = Path.Combine(_workDir, "timeline.mp4");
            var builder = new FfmpegTimelineVideoBuilder(NullLogger<FfmpegTimelineVideoBuilder>.Instance);

            TimelineVideoResult result = await builder.BuildAsync(new TimelineVideoRequest(stills, output, clips), CancellationToken.None);

            Assert.True(result.Success, result.ErrorMessage);
            Assert.Equal(2, result.FrameCount);
            Assert.Equal(result.FrameCount.ToString(System.Globalization.CultureInfo.InvariantCulture), ProbeStreamField(output, "nb_read_frames"));
        }

        [Fact]
        public async Task BuildAsync_TrimmedClipOffsetPastEnd_AddsNoClipSegment()
        {
            if (!ToolAvailable("ffmpeg") || !ToolAvailable("ffprobe"))
            {
                return;
            }

            string still = Path.Combine(_workDir, "still.png");
            string clip = Path.Combine(_workDir, "clip.mp4");
            CreatePng(still, "red", 320, 240);
            CreateVideo(clip, 320, 240, seconds: 2);

            // A single clip always starts at its StartedAt (offset 0), so an offset only arises when an earlier clip
            // ends after this clip starts: the later clip is trimmed to start at that earlier end.
            // Clip A: [T0-3, T0+1), real 2 s, declared 4 s. Usable = min(declared 4, real 2) = 2 frames.
            // Clip B: StartedAt T0-2, Duration 7 s, so its declared window is [T0+1, T0+5) with 4 s.
            //   Start is trimmed to A's end (T0+1), so offset = (T0+1) - (T0-2) = 3 s, past B's real end of 2 s.
            //   Real seconds left = 2 - 3 = -1, so B's usable seconds are 0 and B adds no segment.
            // Still: [T0, T0+6) red, 6 s. A covers [T0, T0+1) and B's window covers [T0+1, T0+5), so only the tail
            //   [T0+5, T0+6) remains: 1 s, 1 frame.
            // Clip A is usable for 2 s at its window start; the rest of its declared window has no real video, so it is a gap that is
            // filled with black. Expected: 2 (A) + 6 black gap (T0-1 to T0+5) + 1 (still tail) = 9 frames, in real time.
            TimelineStill[] stills = new[] { new TimelineStill(still, T0, TimeSpan.FromSeconds(6)) };
            TimelineClip[] clips = new[]
            {
                new TimelineClip(clip, T0.AddSeconds(-3), TimeSpan.FromSeconds(4)),
                new TimelineClip(clip, T0.AddSeconds(-2), TimeSpan.FromSeconds(7)),
            };
            string output = Path.Combine(_workDir, "timeline.mp4");
            var builder = new FfmpegTimelineVideoBuilder(NullLogger<FfmpegTimelineVideoBuilder>.Instance);

            TimelineVideoResult result = await builder.BuildAsync(new TimelineVideoRequest(stills, output, clips), CancellationToken.None);

            Assert.True(result.Success, result.ErrorMessage);
            Assert.Equal(9, result.FrameCount);
            Assert.Equal("9", ProbeStreamField(output, "nb_read_frames"));

            List<(byte R, byte G, byte B)> frames = ReadFrameAverages(output);
            Assert.Equal(9, frames.Count);
            Assert.False(IsRed(frames[0]), $"frame 0 should be clip A, not the still, was {frames[0]}");
            Assert.False(IsRed(frames[1]), $"frame 1 should be clip A, not the still, was {frames[1]}");
            for (int gap = 2; gap <= 7; gap++)
            {
                Assert.True(IsGray(frames[gap]), $"frame {gap} should be gray gap fill, was {frames[gap]}");
            }

            Assert.True(IsRed(frames[8]), $"frame 8 should be the still tail, was {frames[8]}");
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

            TimelineStill[] stills = new[] { new TimelineStill(still, T0, TimeSpan.FromSeconds(3)) };
            TimelineClip[] clips = new[] { new TimelineClip(notVideo, T0.AddSeconds(1), TimeSpan.FromSeconds(1)) };
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
            TimelineStill[] stills = new[] { new TimelineStill(still, T0, TimeSpan.FromSeconds(1)) };
            TimelineClip[] clips = new[] { new TimelineClip(Path.Combine(_workDir, "missing.mp4"), T0, TimeSpan.FromSeconds(1)) };
            var request = new TimelineVideoRequest(stills, Path.Combine(_workDir, "out.mp4"), clips);

            _ = await Assert.ThrowsAsync<ArgumentException>(() => builder.BuildAsync(request, CancellationToken.None));
        }

        [Fact]
        public async Task BuildAsync_ClipZeroDuration_ThrowsArgumentException()
        {
            var builder = new FfmpegTimelineVideoBuilder(NullLogger<FfmpegTimelineVideoBuilder>.Instance);
            string still = WriteDummyFile("still.png");
            string clipFile = WriteDummyFile("clip.mp4");
            TimelineStill[] stills = new[] { new TimelineStill(still, T0, TimeSpan.FromSeconds(1)) };
            TimelineClip[] clips = new[] { new TimelineClip(clipFile, T0, TimeSpan.Zero) };
            var request = new TimelineVideoRequest(stills, Path.Combine(_workDir, "out.mp4"), clips);

            _ = await Assert.ThrowsAsync<ArgumentException>(() => builder.BuildAsync(request, CancellationToken.None));
        }

        private string WriteDummyFile(string name)
        {
            string path = Path.Combine(_workDir, name);
            File.WriteAllBytes(path, new byte[] { 0x89, 0x50, 0x4E, 0x47 });
            return path;
        }

        private void CreatePng(string path, string color, int width, int height)
        {
            _ = RunTool("ffmpeg", "-f", "lavfi", "-i", $"color=c={color}:s={width}x{height}", "-frames:v", "1", "-y", path);
        }

        private void CreateVideo(string path, int width, int height, int seconds)
        {
            _ = RunTool(
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
        private static bool IsRed((byte R, byte G, byte B) c)
        {
            return c.R > 200 && c.G < 60 && c.B < 60;
        }

        // Missing time is drawn as mid gray (#808080), not black, so a gap reads as "no image" rather than a dark frame.
        private static bool IsGray((byte R, byte G, byte B) c)
        {
            return c.R is > 112 and < 144 && c.G is > 112 and < 144 && c.B is > 112 and < 144;
        }

        private static bool IsBlue((byte R, byte G, byte B) c)
        {
            return c.B > 200 && c.R < 60 && c.G < 60;
        }

        private static byte[] RunToolBinary(string baseName, params string[] args)
        {
            using Process process = StartTool(baseName, args);
            // Read stderr asynchronously: ffmpeg writes progress there, and a full pipe would deadlock a blocking stdout read.
            Task<string> stderrTask = process.StandardError.ReadToEndAsync();
            using var buffer = new MemoryStream();
            process.StandardOutput.BaseStream.CopyTo(buffer);
            string stderr = stderrTask.GetAwaiter().GetResult();
            process.WaitForExit();
            return process.ExitCode != 0
                ? throw new InvalidOperationException($"{baseName} exited {process.ExitCode}: {stderr}")
                : buffer.ToArray();
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
                _ = process.StandardOutput.ReadToEnd();
                _ = process.StandardError.ReadToEnd();
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
            return process.ExitCode != 0 ? throw new InvalidOperationException($"{baseName} exited {process.ExitCode}: {stderr}") : stdout;
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
        public async Task BuildAsync_UnusedAudioOnlyClip_DoesNotFailTheBuild()
        {
            if (!ToolAvailable("ffmpeg") || !ToolAvailable("ffprobe"))
            {
                return;
            }

            // Same layout as the offset-past-end test, but clip B has no video stream. B contributes no segment,
            // so no dimension probe may run on it: a clip that adds nothing must not fail the build.
            string still = Path.Combine(_workDir, "still.png");
            string clip = Path.Combine(_workDir, "clip.mp4");
            string sound = Path.Combine(_workDir, "sound.m4a");
            CreatePng(still, "red", 320, 240);
            CreateVideo(clip, 320, 240, seconds: 2);
            _ = RunTool("ffmpeg", "-f", "lavfi", "-i", "anullsrc=r=44100:cl=mono", "-t", "2", "-c:a", "aac", "-y", sound);

            TimelineStill[] stills = new[] { new TimelineStill(still, T0, TimeSpan.FromSeconds(6)) };
            TimelineClip[] clips = new[]
            {
                new TimelineClip(clip, T0.AddSeconds(-3), TimeSpan.FromSeconds(4)),
                new TimelineClip(sound, T0.AddSeconds(-2), TimeSpan.FromSeconds(7)),
            };
            var builder = new FfmpegTimelineVideoBuilder(NullLogger<FfmpegTimelineVideoBuilder>.Instance);

            TimelineVideoResult result = await builder.BuildAsync(
                new TimelineVideoRequest(stills, Path.Combine(_workDir, "out.mp4"), clips), CancellationToken.None);

            Assert.True(result.Success, result.ErrorMessage);
        }
    }
}
