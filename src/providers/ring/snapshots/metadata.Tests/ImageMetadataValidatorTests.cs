using System.IO.Abstractions;

using Xunit;

namespace VideoForensics.Providers.Ring.Snapshots.Metadata.Tests
{
    public class ImageMetadataValidatorTests : IDisposable
    {
        private readonly IMetadataValidator _validator = null!;
        private readonly IFileSystem _fileSystem = null!;
        private readonly string _testFilePath = null!;

        public ImageMetadataValidatorTests()
        {
            _fileSystem = new System.IO.Abstractions.FileSystem();
            _validator = new ImageMetadataValidator(_fileSystem);
            _testFilePath = Path.Combine(Path.GetTempPath(), $"test-image-{Guid.NewGuid()}.jpg");
        }

        public void Dispose()
        {
            if (_fileSystem.File.Exists(_testFilePath))
            {
                _fileSystem.File.Delete(_testFilePath);
            }
        }

        #region Format Detection Tests

        [Fact]
        public void DetectFormat_WithJpegFile_ReturnsJpeg()
        {
            byte[] jpegHeader = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 };
            _fileSystem.File.WriteAllBytes(_testFilePath, jpegHeader);

            string? format = _validator.DetectFormat(_testFilePath);

            Assert.Equal("JPEG", format);
        }

        [Fact]
        public void DetectFormat_WithPngFile_ReturnsPng()
        {
            byte[] pngHeader = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
            _fileSystem.File.WriteAllBytes(_testFilePath, pngHeader);

            string? format = _validator.DetectFormat(_testFilePath);

            Assert.Equal("PNG", format);
        }

        [Fact]
        public void DetectFormat_WithWebPFile_ReturnsWebP()
        {
            byte[] webpHeader = new byte[] { 0x52, 0x49, 0x46, 0x46, 0x00, 0x00, 0x00, 0x00, 0x57, 0x45, 0x42, 0x50 };
            _fileSystem.File.WriteAllBytes(_testFilePath, webpHeader);

            string? format = _validator.DetectFormat(_testFilePath);

            Assert.Equal("WebP", format);
        }

        [Fact]
        public void DetectFormat_WithGifFile_ReturnsGif()
        {
            byte[] gifHeader = new byte[] { 0x47, 0x49, 0x46, 0x38 };
            _fileSystem.File.WriteAllBytes(_testFilePath, gifHeader);

            string? format = _validator.DetectFormat(_testFilePath);

            Assert.Equal("GIF", format);
        }

        [Fact]
        public void DetectFormat_WithUnknownFormat_ReturnsNull()
        {
            byte[] unknownHeader = new byte[] { 0x00, 0x00, 0x00, 0x00 };
            _fileSystem.File.WriteAllBytes(_testFilePath, unknownHeader);

            string? format = _validator.DetectFormat(_testFilePath);

            Assert.Null(format);
        }

        [Fact]
        public void DetectFormat_WithNonExistentFile_ReturnsNull()
        {
            string? format = _validator.DetectFormat("/nonexistent/file.jpg");

            Assert.Null(format);
        }

        #endregion

        #region Validation Tests

        [Fact]
        public void Validate_WithValidJpegFile_ReturnsTrue()
        {
            byte[] jpegHeader = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 };
            _fileSystem.File.WriteAllBytes(_testFilePath, jpegHeader);

            bool isValid = _validator.Validate(_testFilePath);

            Assert.True(isValid);
        }

        [Fact]
        public void Validate_WithValidPngFile_ReturnsTrue()
        {
            string pngPath = Path.Combine(Path.GetTempPath(), $"test-{Guid.NewGuid()}.png");
            byte[] pngHeader = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
            _fileSystem.File.WriteAllBytes(pngPath, pngHeader);

            try
            {
                bool isValid = _validator.Validate(pngPath);
                Assert.True(isValid);
            }
            finally
            {
                if (_fileSystem.File.Exists(pngPath))
                {
                    _fileSystem.File.Delete(pngPath);
                }
            }
        }

        [Fact]
        public void Validate_WithNonExistentFile_ReturnsFalse()
        {
            bool isValid = _validator.Validate("/nonexistent/file.jpg");

            Assert.False(isValid);
        }

        [Fact]
        public void Validate_WithInvalidExtension_ReturnsFalse()
        {
            string invalidPath = Path.Combine(Path.GetTempPath(), $"test-{Guid.NewGuid()}.txt");
            File.WriteAllBytes(invalidPath, new byte[] { 0xFF, 0xD8, 0xFF });

            try
            {
                bool isValid = _validator.Validate(invalidPath);

                Assert.False(isValid);
            }
            finally
            {
                if (File.Exists(invalidPath))
                {
                    File.Delete(invalidPath);
                }
            }
        }

        [Fact]
        public void Validate_WithNullPath_ReturnsFalse()
        {
            bool isValid = _validator.Validate(null!);

            Assert.False(isValid);
        }

        [Fact]
        public void Validate_WithEmptyPath_ReturnsFalse()
        {
            bool isValid = _validator.Validate("");

            Assert.False(isValid);
        }

        #endregion

        #region Corruption Detection Tests

        [Fact]
        public void IsCorrupted_WithValidJpeg_ReturnsFalse()
        {
            byte[] jpegHeader = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 };
            _fileSystem.File.WriteAllBytes(_testFilePath, jpegHeader);

            bool isCorrupted = _validator.IsCorrupted(_testFilePath);

            Assert.False(isCorrupted);
        }

        [Fact]
        public void IsCorrupted_WithValidPng_ReturnsFalse()
        {
            string pngPath = Path.Combine(Path.GetTempPath(), $"test-{Guid.NewGuid()}.png");
            byte[] pngHeader = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
            _fileSystem.File.WriteAllBytes(pngPath, pngHeader);

            try
            {
                bool isCorrupted = _validator.IsCorrupted(pngPath);
                Assert.False(isCorrupted);
            }
            finally
            {
                if (_fileSystem.File.Exists(pngPath))
                {
                    _fileSystem.File.Delete(pngPath);
                }
            }
        }

        [Fact]
        public void IsCorrupted_WithInvalidHeader_ReturnsTrue()
        {
            byte[] invalidHeader = new byte[] { 0x00, 0x00, 0x00, 0x00 };
            _fileSystem.File.WriteAllBytes(_testFilePath, invalidHeader);

            bool isCorrupted = _validator.IsCorrupted(_testFilePath);

            Assert.True(isCorrupted);
        }

        [Fact]
        public void IsCorrupted_WithEmptyFile_ReturnsTrue()
        {
            _fileSystem.File.WriteAllBytes(_testFilePath, new byte[] { });

            bool isCorrupted = _validator.IsCorrupted(_testFilePath);

            Assert.True(isCorrupted);
        }

        [Fact]
        public void IsCorrupted_WithNonExistentFile_ReturnsTrue()
        {
            bool isCorrupted = _validator.IsCorrupted("/nonexistent/file.jpg");

            Assert.True(isCorrupted);
        }

        #endregion

        #region Async Operations

        [Fact]
        public async Task ValidateAsync_WithValidFile_ReturnsTrue()
        {
            byte[] jpegHeader = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 };
            _fileSystem.File.WriteAllBytes(_testFilePath, jpegHeader);

            bool isValid = await _validator.ValidateAsync(_testFilePath);

            Assert.True(isValid);
        }

        [Fact]
        public async Task ValidateAsync_WithInvalidFile_ReturnsFalse()
        {
            bool isValid = await _validator.ValidateAsync("/nonexistent/file.jpg");

            Assert.False(isValid);
        }

        #endregion

        #region Supported Formats

        [Fact]
        public void Validate_SupportJpg()
        {
            string jpgPath = Path.Combine(Path.GetTempPath(), $"test-{Guid.NewGuid()}.jpg");
            File.WriteAllBytes(jpgPath, new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 });

            try
            {
                bool isValid = _validator.Validate(jpgPath);
                Assert.True(isValid);
            }
            finally
            {
                if (File.Exists(jpgPath))
                {
                    File.Delete(jpgPath);
                }
            }
        }

        [Fact]
        public void Validate_SupportJpeg()
        {
            string jpegPath = Path.Combine(Path.GetTempPath(), $"test-{Guid.NewGuid()}.jpeg");
            File.WriteAllBytes(jpegPath, new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 });

            try
            {
                bool isValid = _validator.Validate(jpegPath);
                Assert.True(isValid);
            }
            finally
            {
                if (File.Exists(jpegPath))
                {
                    File.Delete(jpegPath);
                }
            }
        }

        [Fact]
        public void Validate_SupportWebp()
        {
            string webpPath = Path.Combine(Path.GetTempPath(), $"test-{Guid.NewGuid()}.webp");
            File.WriteAllBytes(webpPath, new byte[] { 0x52, 0x49, 0x46, 0x46, 0x00, 0x00, 0x00, 0x00, 0x57, 0x45, 0x42, 0x50 });

            try
            {
                bool isValid = _validator.Validate(webpPath);
                Assert.True(isValid);
            }
            finally
            {
                if (File.Exists(webpPath))
                {
                    File.Delete(webpPath);
                }
            }
        }

        [Fact]
        public void Validate_SupportPng()
        {
            string pngPath = Path.Combine(Path.GetTempPath(), $"test-{Guid.NewGuid()}.png");
            File.WriteAllBytes(pngPath, new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });

            try
            {
                bool isValid = _validator.Validate(pngPath);
                Assert.True(isValid);
            }
            finally
            {
                if (File.Exists(pngPath))
                {
                    File.Delete(pngPath);
                }
            }
        }

        #endregion
    }
}
