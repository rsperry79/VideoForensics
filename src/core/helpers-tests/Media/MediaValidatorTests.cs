using System;
using System.IO;
using Xunit;
using VideoForensics.Providers.Common.Helpers.Contracts;
using VideoForensics.Providers.Common.Helpers.Media;

#nullable enable

namespace VideoForensics.Providers.Common.Helpers.Tests.Media
{
    public class MediaValidatorTests : IDisposable
    {
        private readonly IMediaValidator _validator = new MediaValidator();
        private readonly string _tempDir = Path.Combine(Path.GetTempPath(), $"MediaValidatorTests_{Guid.NewGuid()}");

        public MediaValidatorTests()
        {
            Directory.CreateDirectory(_tempDir);
        }

        [Fact]
        public void ValidateMediaExists_WithNonexistentFile_ReturnsFalse()
        {
            var result = _validator.ValidateMediaExists("/nonexistent/file.mp4", null);
            Assert.False(result);
        }

        [Fact]
        public void ValidateMediaExists_WithNullPath_ReturnsFalse()
        {
            var result = _validator.ValidateMediaExists(null!, null);
            Assert.False(result);
        }

        [Fact]
        public void ValidateMediaExists_WithEmptyPath_ReturnsFalse()
        {
            var result = _validator.ValidateMediaExists("", null);
            Assert.False(result);
        }

        [Fact]
        public void ValidateMediaExists_WithExistingFileAndNoExpectedSize_ReturnsTrue()
        {
            var filePath = Path.Combine(_tempDir, "test.mp4");
            File.WriteAllText(filePath, "content");

            var result = _validator.ValidateMediaExists(filePath, null);
            Assert.True(result);
        }

        [Fact]
        public void ValidateMediaExists_WithExistingFileAndMatchingSize_ReturnsTrue()
        {
            var filePath = Path.Combine(_tempDir, "test.mp4");
            var content = "content";
            File.WriteAllText(filePath, content);
            var expectedSize = new FileInfo(filePath).Length;

            var result = _validator.ValidateMediaExists(filePath, expectedSize);
            Assert.True(result);
        }

        [Fact]
        public void ValidateMediaExists_WithExistingFileAndMismatchedSize_ReturnsFalse()
        {
            var filePath = Path.Combine(_tempDir, "test.mp4");
            File.WriteAllText(filePath, "content");
            var wrongSize = 999L;

            var result = _validator.ValidateMediaExists(filePath, wrongSize);
            Assert.False(result);
        }

        [Fact]
        public void ValidateMediaExists_WithEmptyExistingFile_ReturnsFalse()
        {
            var filePath = Path.Combine(_tempDir, "empty.mp4");
            File.WriteAllText(filePath, "");

            var result = _validator.ValidateMediaExists(filePath, null);
            Assert.False(result);
        }

        [Fact]
        public void ValidateMediaExists_WithEmptyFileAndZeroExpectedSize_ReturnsTrue()
        {
            var filePath = Path.Combine(_tempDir, "empty.mp4");
            File.WriteAllText(filePath, "");

            var result = _validator.ValidateMediaExists(filePath, 0);
            Assert.True(result);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_tempDir))
                {
                    Directory.Delete(_tempDir, true);
                }
            }
            catch
            {
                // Ignore cleanup errors in tests
            }
        }
    }
}
