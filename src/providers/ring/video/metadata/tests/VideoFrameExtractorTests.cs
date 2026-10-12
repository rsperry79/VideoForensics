using System.IO.Abstractions.TestingHelpers;

namespace VideoForensics.Providers.Ring.Video.Metadata.Tests
{
    public class VideoFrameExtractorTests
    {
        [Fact]
        public void ExtractFramesAtTimestamps_WhenFfmpegFailsWithNoHardwareAccel_ReturnsFailedExtractedFrameNotEmpty()
        {
            // Arrange: Create a mock filesystem with a video file
            var mockFileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
            {
                { "/videos/test.mp4", new MockFileData(new byte[100]) }
            });

            var extractor = new VideoFrameExtractor(mockFileSystem, "nonexistent-ffmpeg-binary-xyz-fallback-test");

            // Act: Extract frames at a timestamp
            var timestamps = new List<long> { 1000L };
            var result = extractor.ExtractFramesAtTimestamps("/videos/test.mp4", timestamps, "/output");

            // Assert: The result should contain exactly 1 element (the frame with failure info)
            Assert.NotEmpty(result);
            Assert.Single(result);
            Assert.False(result[0].ExtractionSuccessful);
            Assert.NotNull(result[0].ExtractionError);
            Assert.NotEmpty(result[0].ExtractionError);
        }
    }
}
