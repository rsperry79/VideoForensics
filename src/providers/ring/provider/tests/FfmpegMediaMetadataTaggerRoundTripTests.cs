using System.Diagnostics;

using Microsoft.Extensions.Logging.Abstractions;

using VideoForensics.Providers.Ring.Services;

using Xunit;

namespace VideoForensics.Providers.Ring.Tests
{
    /// <summary>Tags a real MP4 through FFMpegCore and reads the ID back; skips when ffmpeg is not installed.</summary>
    public class FfmpegMediaMetadataTaggerRoundTripTests : IDisposable
    {
        private readonly string _workDir = Path.Combine(Path.GetTempPath(), "tagger-roundtrip-" + Guid.NewGuid().ToString("N"));

        public FfmpegMediaMetadataTaggerRoundTripTests()
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
        public async Task TagThenRead_ReturnsTheSameEventId()
        {
            if (!FfmpegAvailable())
            {
                return;
            }

            string clip = Path.Combine(_workDir, "clip.mp4");
            using (var make = Process.Start(new ProcessStartInfo("ffmpeg", $"-f lavfi -i testsrc=size=160x120:rate=5 -t 1 -y \"{clip}\"") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false }))
            {
                Assert.NotNull(make);
                _ = make!.StandardError.ReadToEnd();
                make.WaitForExit();
                Assert.Equal(0, make.ExitCode);
            }

            var eventId = Guid.NewGuid();
            var tagger = new FfmpegMediaMetadataTagger(NullLogger.Instance, ffmpegPath: "ffmpeg", ffprobePath: "ffprobe");

            bool tagged = await tagger.TagEventIdAsync(clip, eventId, CancellationToken.None);
            Guid? read = await tagger.ReadEventIdAsync(clip, CancellationToken.None);

            Assert.True(tagged);
            Assert.Equal(eventId, read);
        }
    }
}
