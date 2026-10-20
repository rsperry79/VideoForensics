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
            var tagger = new FfmpegMediaMetadataTagger(_mockLogger.Object, ffmpegPath: "ffmpeg");

            var method = typeof(FfmpegMediaMetadataTagger).GetMethod(
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
            Guid eventId = Guid.NewGuid();
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
            Guid eventId = Guid.NewGuid();
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
                using Process? probe = Process.Start(new ProcessStartInfo(name, "-version")
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                });
                if (probe is null)
                {
                    return false;
                }

                probe.StandardOutput.ReadToEnd();
                probe.WaitForExit();
                return probe.ExitCode == 0;
            }
            catch (System.ComponentModel.Win32Exception)
            {
                return false;
            }
        }

        [Fact]
        public void BuildTaggingProcessInfo_UsesArgumentList_NotArgumentsString()
        {
            var tagger = new FfmpegMediaMetadataTagger(_mockLogger.Object, ffmpegPath: "ffmpeg");
            string mediaFilePath = "/path/to/media.mp4";
            Guid eventId = Guid.NewGuid();
            string tmpPath = "/path/to/tmp.mp4";

            // Use reflection to call the internal BuildTaggingProcessInfo method
            var method = typeof(FfmpegMediaMetadataTagger).GetMethod(
                "BuildTaggingProcessInfo",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance,
                null,
                new[] { typeof(string), typeof(Guid), typeof(string) },
                null);

            Assert.NotNull(method);

            var psi = (ProcessStartInfo)method.Invoke(tagger, new object[] { mediaFilePath, eventId, tmpPath })!;

            // Verify ArgumentList is used instead of Arguments string
            Assert.NotNull(psi.ArgumentList);
            Assert.NotEmpty(psi.ArgumentList);
            // Arguments should be empty/null when using ArgumentList
            Assert.True(string.IsNullOrEmpty(psi.Arguments));

            // Verify expected arguments are in the list
            Assert.Contains("-y", psi.ArgumentList);
            Assert.Contains("-i", psi.ArgumentList);
            Assert.Contains(mediaFilePath, psi.ArgumentList);
            Assert.Contains("-codec", psi.ArgumentList);
            Assert.Contains("copy", psi.ArgumentList);
            Assert.Contains(tmpPath, psi.ArgumentList);
        }

        [Fact]
        public void BuildReadingProcessInfo_UsesArgumentList_NotArgumentsString()
        {
            var tagger = new FfmpegMediaMetadataTagger(_mockLogger.Object, ffprobePath: "ffprobe");
            string mediaFilePath = "/path/to/media.mp4";

            // Use reflection to call the internal BuildReadingProcessInfo method
            var method = typeof(FfmpegMediaMetadataTagger).GetMethod(
                "BuildReadingProcessInfo",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance,
                null,
                new[] { typeof(string) },
                null);

            Assert.NotNull(method);

            var psi = (ProcessStartInfo)method.Invoke(tagger, new object[] { mediaFilePath })!;

            // Verify ArgumentList is used instead of Arguments string
            Assert.NotNull(psi.ArgumentList);
            Assert.NotEmpty(psi.ArgumentList);
            // Arguments should be empty/null when using ArgumentList
            Assert.True(string.IsNullOrEmpty(psi.Arguments));

            // Verify expected arguments are in the list
            Assert.Contains("-v", psi.ArgumentList);
            Assert.Contains("quiet", psi.ArgumentList);
            Assert.Contains("-show_entries", psi.ArgumentList);
            Assert.Contains("format_tags=comment", psi.ArgumentList);
            Assert.Contains("-of", psi.ArgumentList);
            Assert.Contains("default=noprint_wrappers=1:nokey=1", psi.ArgumentList);
            Assert.Contains(mediaFilePath, psi.ArgumentList);
        }
    }
}
