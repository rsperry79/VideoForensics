using System.Diagnostics;

using Microsoft.Extensions.Logging.Abstractions;

using Xunit;

namespace VideoForensics.Core.Providers.Core.Tests
{
    /// <summary>
    /// Frame extraction runs through FFMpegCore against the real ffmpeg, so these tests generate a short video and skip
    /// when ffmpeg is not installed. Timeout and output-cap behaviour are checked the same way.
    /// </summary>
    public class FfmpegMediaFrameExtractorTests : IDisposable
    {
        private readonly string _workDir = Path.Combine(Path.GetTempPath(), "frame-extractor-" + Guid.NewGuid().ToString("N"));

        public FfmpegMediaFrameExtractorTests()
        {
            _ = Directory.CreateDirectory(_workDir);
        }

        public void Dispose()
        {
            if (Directory.Exists(_workDir))
            {
                Directory.Delete(_workDir, recursive: true);
            }
        }

        private static bool FfmpegAvailable()
        {
            try
            {
                using var process = Process.Start(new ProcessStartInfo("ffmpeg", "-version") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false });
                if (process is null)
                {
                    return false;
                }

                _ = process.StandardOutput.ReadToEnd();
                _ = process.StandardError.ReadToEnd();
                process.WaitForExit();
                return process.ExitCode == 0;
            }
            catch (System.ComponentModel.Win32Exception)
            {
                return false;
            }
        }

        private string MakeTestVideo()
        {
            string path = Path.Combine(_workDir, "clip.mp4");
            using var process = Process.Start(new ProcessStartInfo("ffmpeg", $"-f lavfi -i testsrc=size=320x240:rate=5 -t 3 -y \"{path}\"")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            });
            Assert.NotNull(process);
            _ = process!.StandardError.ReadToEnd();
            process.WaitForExit();
            Assert.Equal(0, process.ExitCode);
            return path;
        }

        [Fact]
        public async Task ExtractFrameAsync_NegativeOffset_ReturnsFailureWithoutRunningFfmpeg()
        {
            var extractor = new FfmpegMediaFrameExtractor(NullLogger<FfmpegMediaFrameExtractor>.Instance, "nonexistent-ffmpeg-binary-xyz");

            FrameExtractionResult result = await extractor.ExtractFrameAsync("any.mp4", -1, CancellationToken.None);

            Assert.False(result.Success);
            Assert.Contains("frameOffsetMs must be >= 0", result.ErrorMessage);
        }

        [Fact]
        public async Task ExtractFrameAsync_MissingBinary_ReturnsFailureResult()
        {
            var extractor = new FfmpegMediaFrameExtractor(NullLogger<FfmpegMediaFrameExtractor>.Instance, Path.Combine(_workDir, "no-such-ffmpeg.exe"));

            FrameExtractionResult result = await extractor.ExtractFrameAsync("any.mp4", 0, CancellationToken.None);

            Assert.False(result.Success);
            Assert.False(string.IsNullOrWhiteSpace(result.ErrorMessage));
        }

        [Fact]
        public async Task ExtractFrameAsync_ValidOffset_ReturnsPngBytes()
        {
            if (!FfmpegAvailable())
            {
                return;
            }

            string video = MakeTestVideo();
            var extractor = new FfmpegMediaFrameExtractor(NullLogger<FfmpegMediaFrameExtractor>.Instance, "ffmpeg");

            FrameExtractionResult result = await extractor.ExtractFrameAsync(video, 1000, CancellationToken.None);

            Assert.True(result.Success, result.ErrorMessage);
            Assert.NotNull(result.PngBytes);
            Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47 }, result.PngBytes!.Take(4).ToArray());
        }

        [Fact]
        public async Task ExtractFrameAsync_OutputOverCap_ReturnsFailureWithTheCapReason()
        {
            if (!FfmpegAvailable())
            {
                return;
            }

            string video = MakeTestVideo();
            var extractor = new FfmpegMediaFrameExtractor(NullLogger<FfmpegMediaFrameExtractor>.Instance, "ffmpeg", maxOutputBytes: 100);

            FrameExtractionResult result = await extractor.ExtractFrameAsync(video, 1000, CancellationToken.None);

            Assert.False(result.Success);
            Assert.Contains("maximum allowed size", result.ErrorMessage);
        }

        [Fact]
        public async Task ExtractFrameAsync_ExceedsTimeout_ReturnsFailureWithTheTimeoutReason()
        {
            if (!FfmpegAvailable())
            {
                return;
            }

            string video = MakeTestVideo();
            var extractor = new FfmpegMediaFrameExtractor(NullLogger<FfmpegMediaFrameExtractor>.Instance, "ffmpeg", timeout: TimeSpan.FromMilliseconds(1));

            FrameExtractionResult result = await extractor.ExtractFrameAsync(video, 1000, CancellationToken.None);

            Assert.False(result.Success);
            Assert.Contains("timed out", result.ErrorMessage);
        }
    }
}
