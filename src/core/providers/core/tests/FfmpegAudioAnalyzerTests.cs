using System.Diagnostics;

using Xunit;

namespace VideoForensics.Core.Providers.Core.Tests
{
    /// <summary>The audio analyzer runs its analysis filters through FFMpegCore against the real ffmpeg; it skips when ffmpeg is absent.</summary>
    public class FfmpegAudioAnalyzerTests : IDisposable
    {
        private readonly string _workDir = Path.Combine(Path.GetTempPath(), "audio-analyzer-" + Guid.NewGuid().ToString("N"));

        public FfmpegAudioAnalyzerTests()
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
            var analyzer = new FfmpegAudioAnalyzer(ffmpegPath: "ffmpeg");

            AudioClipStats? stats = await analyzer.AnalyzeAsync(Path.Combine(_workDir, "missing.mp4"), 2.0, CancellationToken.None);

            Assert.Null(stats);
        }

        [Fact]
        public async Task AnalyzeAsync_ToneClip_ReturnsStats()
        {
            if (!FfmpegAvailable())
            {
                return;
            }

            string clip = Path.Combine(_workDir, "tone.mp4");
            using (var make = Process.Start(new ProcessStartInfo("ffmpeg", $"-f lavfi -i sine=frequency=440:duration=2 -f lavfi -i color=c=black:s=64x48:d=2 -shortest -y \"{clip}\"") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false }))
            {
                Assert.NotNull(make);
                _ = make!.StandardError.ReadToEnd();
                make.WaitForExit();
                Assert.Equal(0, make.ExitCode);
            }

            var analyzer = new FfmpegAudioAnalyzer(ffmpegPath: "ffmpeg");
            AudioClipStats? stats = await analyzer.AnalyzeAsync(clip, 2.0, CancellationToken.None);

            Assert.NotNull(stats);
        }
    }
}
