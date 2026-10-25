using System.Diagnostics;

using Xunit;

namespace VideoForensics.Core.Providers.Core.Tests
{
    /// <summary>The colour analyzer runs its signalstats chain through FFMpegCore against the real ffmpeg; it skips when ffmpeg is absent.</summary>
    public class FfmpegClipColorAnalyzerTests : IDisposable
    {
        private readonly string _workDir = Path.Combine(Path.GetTempPath(), "clip-color-" + Guid.NewGuid().ToString("N"));

        public FfmpegClipColorAnalyzerTests()
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

        [Fact]
        public async Task AnalyzeAsync_MissingFile_ReturnsNull()
        {
            var analyzer = new FfmpegClipColorAnalyzer(ffmpegPath: "ffmpeg");

            ClipColorStats? stats = await analyzer.AnalyzeAsync(Path.Combine(_workDir, "missing.mp4"), CancellationToken.None);

            Assert.Null(stats);
        }

        [Fact]
        public async Task AnalyzeAsync_ColourClip_ReturnsStats()
        {
            if (!FfmpegAvailable())
            {
                return;
            }

            string clip = Path.Combine(_workDir, "colour.mp4");
            using (var make = Process.Start(new ProcessStartInfo("ffmpeg", $"-f lavfi -i color=c=red:s=160x120:d=2:r=5 -y \"{clip}\"") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false }))
            {
                Assert.NotNull(make);
                _ = make!.StandardError.ReadToEnd();
                make.WaitForExit();
                Assert.Equal(0, make.ExitCode);
            }

            var analyzer = new FfmpegClipColorAnalyzer(ffmpegPath: "ffmpeg");
            ClipColorStats? stats = await analyzer.AnalyzeAsync(clip, CancellationToken.None);

            Assert.NotNull(stats);
        }
    }
}
