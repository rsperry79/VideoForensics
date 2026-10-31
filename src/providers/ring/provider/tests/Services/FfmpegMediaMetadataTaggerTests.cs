using Microsoft.Extensions.Logging;

using Moq;

using System.Diagnostics;
using System.Reflection;

using VideoForensics.Core.Providers.Core;
using VideoForensics.Providers.Ring.Services;

using Xunit;

namespace VideoForensics.Providers.Ring.Tests
{
    public class FfmpegMediaMetadataTaggerTests
    {
        private readonly Mock<ILogger> _mockLogger = new();

        [Fact]
        public async Task TagEventIdAsync_WhenFfmpegBinaryMissing_ReturnsFalse()
        {
            string tempFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".mp4");
            File.WriteAllText(tempFile, "dummy");
            try
            {
                var tagger = new FfmpegMediaMetadataTagger(_mockLogger.Object, ffmpegPath: "nonexistent-ffmpeg-binary-xyz");
                bool result = await tagger.TagEventIdAsync(tempFile, Guid.NewGuid(), CancellationToken.None);

                Assert.False(result);
            }
            finally
            {
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }
            }
        }

        [Fact]
        public async Task ReadEventIdAsync_WhenFfprobeBinaryMissing_ReturnsNull()
        {
            string tempFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".mp4");
            File.WriteAllText(tempFile, "dummy");
            try
            {
                var tagger = new FfmpegMediaMetadataTagger(_mockLogger.Object, ffprobePath: "nonexistent-ffprobe-binary-xyz");
                Guid? result = await tagger.ReadEventIdAsync(tempFile, CancellationToken.None);

                Assert.Null(result);
            }
            finally
            {
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }
            }
        }

        [Fact]
        public async Task FfmpegMediaMetadataTagger_TagFails_LogsError()
        {
            string tempFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".mp4");
            File.WriteAllText(tempFile, "dummy");
            var logger = new Mock<ILogger>();
            try
            {
                var tagger = new FfmpegMediaMetadataTagger(logger.Object, ffmpegPath: "nonexistent-ffmpeg-binary-xyz");
                bool result = await tagger.TagEventIdAsync(tempFile, Guid.NewGuid(), CancellationToken.None);

                Assert.False(result);
                logger.Verify(
                    l => l.Log(LogLevel.Error, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(), It.IsAny<Exception?>(), It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                    Times.Once);
                logger.Verify(
                    l => l.Log(LogLevel.Warning, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(), It.IsAny<Exception?>(), It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                    Times.Never);
            }
            finally
            {
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }
            }
        }

        /// <summary>
        /// ffmpeg picks the output muxer from the file extension, so the temp file must keep the media's
        /// real extension; "clip.mp4.tmp" fails with "Unable to choose an output format" (exit -22).
        /// </summary>
        [Fact]
        public void BuildTempPath_KeepsMediaExtension_ForFormatInference()
        {
            _ = new FfmpegMediaMetadataTagger(_mockLogger.Object, ffmpegPath: "ffmpeg");

            MethodInfo? method = typeof(FfmpegMediaMetadataTagger).GetMethod(
                "BuildTempPath",
                BindingFlags.NonPublic | BindingFlags.Static,
                null,
                new[] { typeof(string) },
                null);

            Assert.NotNull(method);

            string tmp = (string)method.Invoke(null, new object[] { Path.Combine("dir", "clip.mp4") })!;

            Assert.Equal(Path.Combine("dir", "clip.tmp.mp4"), tmp);
        }

        /// <summary>
        /// ffmpeg writes no comment into JPEG output, so JPEG tagging writes the COM segment directly and reads it back
        /// without shelling out. Both binaries are missing on purpose: any ffmpeg/ffprobe call would fail the round trip.
        /// </summary>
        [Fact]
        public async Task TagEventIdAsync_JpegMedia_WritesCommentThatReadEventIdAsyncReturns()
        {
            string tempFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".jpg");
            File.WriteAllBytes(tempFile, new byte[] { 0xFF, 0xD8, 0xFF, 0xD9 });
            var eventId = Guid.NewGuid();
            try
            {
                var tagger = new FfmpegMediaMetadataTagger(
                    _mockLogger.Object,
                    ffmpegPath: "nonexistent-ffmpeg-binary-xyz",
                    ffprobePath: "nonexistent-ffprobe-binary-xyz");

                bool tagged = await tagger.TagEventIdAsync(tempFile, eventId, CancellationToken.None);
                Guid? read = await tagger.ReadEventIdAsync(tempFile, CancellationToken.None);

                Assert.True(tagged);
                Assert.Equal(eventId, read);
            }
            finally
            {
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }
            }
        }

        /// <summary>
        /// Real-binary round trip: tags an MP4 with ffmpeg and reads the comment back with ffprobe. Skips when
        /// ffmpeg is not installed, so it never passes vacuously.
        /// </summary>
        [Fact]
        public async Task TagEventIdAsync_Mp4_RoundTripsWithRealFfmpegAndFfprobe()
        {
            // Same lookup the app uses (configured bin dir, bundled binary, registry PATH), so the test finds the
            // binaries wherever the app would, not only where the bare name happens to resolve.
            string ffmpeg = FfmpegPathResolver.Resolve(null, "ffmpeg");
            string ffprobe = FfmpegPathResolver.Resolve(null, "ffprobe");
            if (!ToolAvailable(ffmpeg) || !ToolAvailable(ffprobe))
            {
                Assert.Skip("ffmpeg and ffprobe were not found by FfmpegPathResolver for this round trip.");
            }

            string tempFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".mp4");
            var eventId = Guid.NewGuid();
            try
            {
                using (Process generate = Process.Start(new ProcessStartInfo(ffmpeg)
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    ArgumentList = { "-v", "error", "-y", "-f", "lavfi", "-i", "testsrc=d=1:s=64x64", "-c:v", "mpeg4", tempFile },
                })!)
                {
                    await generate.WaitForExitAsync(CancellationToken.None);
                    Assert.Equal(0, generate.ExitCode);
                }

                var tagger = new FfmpegMediaMetadataTagger(_mockLogger.Object, ffmpegPath: ffmpeg, ffprobePath: ffprobe);

                bool tagged = await tagger.TagEventIdAsync(tempFile, eventId, CancellationToken.None);
                Guid? read = await tagger.ReadEventIdAsync(tempFile, CancellationToken.None);

                Assert.True(tagged);
                Assert.Equal(eventId, read);
            }
            finally
            {
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }
            }
        }

        private static bool ToolAvailable(string name)
        {
            try
            {
                using var probe = Process.Start(new ProcessStartInfo(name, "-version")
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                });
                if (probe is null)
                {
                    return false;
                }

                _ = probe.StandardOutput.ReadToEnd();
                probe.WaitForExit();
                return probe.ExitCode == 0;
            }
            catch (System.ComponentModel.Win32Exception)
            {
                return false;
            }
        }


    }
}
