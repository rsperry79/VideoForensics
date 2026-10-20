using Microsoft.Extensions.Logging;

using Moq;

using System.Diagnostics;
using System.Reflection;

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
        /// ffmpeg exits 0 for JPEG input but writes no comment bytes, so tagging must report failure
        /// (and not start ffmpeg) rather than claim the snapshot was tagged.
        /// </summary>
        [Fact]
        public async Task TagEventIdAsync_JpegMedia_ReturnsFalseAndLeavesFileUntouched()
        {
            string tempFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".jpg");
            byte[] original = new byte[] { 0xFF, 0xD8, 0xFF, 0xD9 };
            File.WriteAllBytes(tempFile, original);
            var logger = new Mock<ILogger>();
            try
            {
                // Missing binary on purpose: if the JPEG check ran ffmpeg, the failure would show up as an error, not the warning.
                var tagger = new FfmpegMediaMetadataTagger(logger.Object, ffmpegPath: "nonexistent-ffmpeg-binary-xyz");
                bool result = await tagger.TagEventIdAsync(tempFile, Guid.NewGuid(), CancellationToken.None);

                Assert.False(result);
                Assert.Equal(original, File.ReadAllBytes(tempFile));
                Assert.False(File.Exists(tempFile + ".tmp"));
                logger.Verify(
                    l => l.Log(LogLevel.Warning, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(), It.IsAny<Exception?>(), It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                    Times.Once);
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
