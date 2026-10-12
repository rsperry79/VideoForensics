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
