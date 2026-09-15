using Microsoft.Extensions.Logging;

using Moq;

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
    }
}
