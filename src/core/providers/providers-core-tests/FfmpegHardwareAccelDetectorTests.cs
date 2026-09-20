using System.Diagnostics;
using Xunit;

namespace VideoForensics.Providers.Core.Tests
{
    public class FfmpegHardwareAccelDetectorTests
    {
        [Fact]
        public async Task DetectAsync_WithNonexistentFfmpegPath_ReturnsNull()
        {
            // Arrange
            var nonexistentPath = "nonexistent-ffmpeg-binary-xyz-test-12345";

            // Act
            var result = await FfmpegHardwareAccelDetector.DetectAsync(nonexistentPath);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void Detect_WithNonexistentFfmpegPath_ReturnsNull()
        {
            // Arrange
            var nonexistentPath = "nonexistent-ffmpeg-binary-xyz-test-12346";

            // Act
            var result = FfmpegHardwareAccelDetector.Detect(nonexistentPath);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void Detect_CachesResultAcrossCalls()
        {
            // Arrange
            var nonexistentPath = "nonexistent-ffmpeg-binary-xyz-test-12347";
            var stopwatch = Stopwatch.StartNew();

            // Act - First call (expensive, probes all candidates)
            var result1 = FfmpegHardwareAccelDetector.Detect(nonexistentPath);
            stopwatch.Stop();
            var firstCallTime = stopwatch.ElapsedMilliseconds;

            stopwatch.Restart();
            // Second call (cached, should be instant)
            var result2 = FfmpegHardwareAccelDetector.Detect(nonexistentPath);
            stopwatch.Stop();
            var secondCallTime = stopwatch.ElapsedMilliseconds;

            // Assert
            Assert.Null(result1);
            Assert.Null(result2);
            // Second call should be significantly faster (well under 500ms vs. the first call)
            Assert.True(secondCallTime < 500, $"Second call took {secondCallTime}ms, expected < 500ms (cache should short-circuit)");
        }

        [Fact]
        public void BuildDecodeArgs_WithNullMethod_ReturnsEmptyArray()
        {
            // Act
            var result = FfmpegHardwareAccelDetector.BuildDecodeArgs(null);

            // Assert
            Assert.Empty(result);
        }

        [Fact]
        public void BuildDecodeArgs_WithEmptyMethod_ReturnsEmptyArray()
        {
            // Act
            var result = FfmpegHardwareAccelDetector.BuildDecodeArgs("");

            // Assert
            Assert.Empty(result);
        }

        [Fact]
        public void BuildDecodeArgs_WithMethodName_ReturnsHwaccelFlagPair()
        {
            // Act
            var result = FfmpegHardwareAccelDetector.BuildDecodeArgs("cuda");

            // Assert
            Assert.Equal(new[] { "-hwaccel", "cuda" }, result);
        }

        [Fact]
        public void BuildDecodeArgs_WithDifferentMethodName_ReturnsCorrectPair()
        {
            // Act
            var result = FfmpegHardwareAccelDetector.BuildDecodeArgs("d3d11va");

            // Assert
            Assert.Equal(new[] { "-hwaccel", "d3d11va" }, result);
        }

        [Fact]
        public void BuildDecodeArgs_WithVaapiMethod_ReturnsCorrectPair()
        {
            // Act
            var result = FfmpegHardwareAccelDetector.BuildDecodeArgs("vaapi");

            // Assert
            Assert.Equal(new[] { "-hwaccel", "vaapi" }, result);
        }
    }
}
