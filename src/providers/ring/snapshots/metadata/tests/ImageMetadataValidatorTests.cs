using System.IO.Abstractions;

namespace VideoForensics.Providers.Ring.Snapshots.Metadata.Tests
{
    [TestClass]
    public class ImageMetadataValidatorTests
    {
        private IMetadataValidator _validator = null!;
        private IFileSystem _fileSystem = null!;
        private string _testFilePath = null!;

        [TestInitialize]
        public void Setup()
        {
            _fileSystem = new System.IO.Abstractions.FileSystem();
            _validator = new ImageMetadataValidator(_fileSystem);
            _testFilePath = Path.Combine(Path.GetTempPath(), $"test-image-{Guid.NewGuid()}.jpg");
        }

        [TestCleanup]
        public void Cleanup()
        {
            if (_fileSystem.File.Exists(_testFilePath))
            {
                _fileSystem.File.Delete(_testFilePath);
            }
        }

        #region Format Detection Tests

        [TestMethod]
        public void DetectFormat_WithJpegFile_ReturnsJpeg()
        {
            var jpegHeader = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 };
            _fileSystem.File.WriteAllBytes(_testFilePath, jpegHeader);

            var format = _validator.DetectFormat(_testFilePath);

            Assert.AreEqual("JPEG", format);
        }

        [TestMethod]
        public void DetectFormat_WithPngFile_ReturnsPng()
        {
            var pngHeader = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
            _fileSystem.File.WriteAllBytes(_testFilePath, pngHeader);

            var format = _validator.DetectFormat(_testFilePath);

            Assert.AreEqual("PNG", format);
        }

        [TestMethod]
        public void DetectFormat_WithWebPFile_ReturnsWebP()
        {
            var webpHeader = new byte[] { 0x52, 0x49, 0x46, 0x46, 0x00, 0x00, 0x00, 0x00, 0x57, 0x45, 0x42, 0x50 };
            _fileSystem.File.WriteAllBytes(_testFilePath, webpHeader);

            var format = _validator.DetectFormat(_testFilePath);

            Assert.AreEqual("WebP", format);
        }

        [TestMethod]
        public void DetectFormat_WithGifFile_ReturnsGif()
        {
            var gifHeader = new byte[] { 0x47, 0x49, 0x46, 0x38 };
            _fileSystem.File.WriteAllBytes(_testFilePath, gifHeader);

            var format = _validator.DetectFormat(_testFilePath);

            Assert.AreEqual("GIF", format);
        }

        [TestMethod]
        public void DetectFormat_WithUnknownFormat_ReturnsNull()
        {
            var unknownHeader = new byte[] { 0x00, 0x00, 0x00, 0x00 };
            _fileSystem.File.WriteAllBytes(_testFilePath, unknownHeader);

            var format = _validator.DetectFormat(_testFilePath);

            Assert.IsNull(format);
        }

        [TestMethod]
        public void DetectFormat_WithNonExistentFile_ReturnsNull()
        {
            var format = _validator.DetectFormat("/nonexistent/file.jpg");

            Assert.IsNull(format);
        }

        #endregion

        #region Validation Tests

        [TestMethod]
        public void Validate_WithValidJpegFile_ReturnsTrue()
        {
            var jpegHeader = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 };
            _fileSystem.File.WriteAllBytes(_testFilePath, jpegHeader);

            var isValid = _validator.Validate(_testFilePath);

            Assert.IsTrue(isValid);
        }

        [TestMethod]
        public void Validate_WithValidPngFile_ReturnsTrue()
        {
            var pngPath = Path.Combine(Path.GetTempPath(), $"test-{Guid.NewGuid()}.png");
            var pngHeader = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
            _fileSystem.File.WriteAllBytes(pngPath, pngHeader);

            try
            {
                var isValid = _validator.Validate(pngPath);
                Assert.IsTrue(isValid);
            }
            finally
            {
                if (_fileSystem.File.Exists(pngPath))
                    _fileSystem.File.Delete(pngPath);
            }
        }

        [TestMethod]
        public void Validate_WithNonExistentFile_ReturnsFalse()
        {
            var isValid = _validator.Validate("/nonexistent/file.jpg");

            Assert.IsFalse(isValid);
        }

        [TestMethod]
        public void Validate_WithInvalidExtension_ReturnsFalse()
        {
            var invalidPath = Path.Combine(Path.GetTempPath(), $"test-{Guid.NewGuid()}.txt");
            File.WriteAllBytes(invalidPath, new byte[] { 0xFF, 0xD8, 0xFF });

            try
            {
                var isValid = _validator.Validate(invalidPath);

                Assert.IsFalse(isValid);
            }
            finally
            {
                if (File.Exists(invalidPath))
                    File.Delete(invalidPath);
            }
        }

        [TestMethod]
        public void Validate_WithNullPath_ReturnsFalse()
        {
            var isValid = _validator.Validate(null!);

            Assert.IsFalse(isValid);
        }

        [TestMethod]
        public void Validate_WithEmptyPath_ReturnsFalse()
        {
            var isValid = _validator.Validate("");

            Assert.IsFalse(isValid);
        }

        #endregion

        #region Corruption Detection Tests

        [TestMethod]
        public void IsCorrupted_WithValidJpeg_ReturnsFalse()
        {
            var jpegHeader = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 };
            _fileSystem.File.WriteAllBytes(_testFilePath, jpegHeader);

            var isCorrupted = _validator.IsCorrupted(_testFilePath);

            Assert.IsFalse(isCorrupted);
        }

        [TestMethod]
        public void IsCorrupted_WithValidPng_ReturnsFalse()
        {
            var pngPath = Path.Combine(Path.GetTempPath(), $"test-{Guid.NewGuid()}.png");
            var pngHeader = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
            _fileSystem.File.WriteAllBytes(pngPath, pngHeader);

            try
            {
                var isCorrupted = _validator.IsCorrupted(pngPath);
                Assert.IsFalse(isCorrupted);
            }
            finally
            {
                if (_fileSystem.File.Exists(pngPath))
                    _fileSystem.File.Delete(pngPath);
            }
        }

        [TestMethod]
        public void IsCorrupted_WithInvalidHeader_ReturnsTrue()
        {
            var invalidHeader = new byte[] { 0x00, 0x00, 0x00, 0x00 };
            _fileSystem.File.WriteAllBytes(_testFilePath, invalidHeader);

            var isCorrupted = _validator.IsCorrupted(_testFilePath);

            Assert.IsTrue(isCorrupted);
        }

        [TestMethod]
        public void IsCorrupted_WithEmptyFile_ReturnsTrue()
        {
            _fileSystem.File.WriteAllBytes(_testFilePath, new byte[] { });

            var isCorrupted = _validator.IsCorrupted(_testFilePath);

            Assert.IsTrue(isCorrupted);
        }

        [TestMethod]
        public void IsCorrupted_WithNonExistentFile_ReturnsTrue()
        {
            var isCorrupted = _validator.IsCorrupted("/nonexistent/file.jpg");

            Assert.IsTrue(isCorrupted);
        }

        #endregion

        #region Async Operations

        [TestMethod]
        public async Task ValidateAsync_WithValidFile_ReturnsTrue()
        {
            var jpegHeader = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 };
            _fileSystem.File.WriteAllBytes(_testFilePath, jpegHeader);

            var isValid = await _validator.ValidateAsync(_testFilePath);

            Assert.IsTrue(isValid);
        }

        [TestMethod]
        public async Task ValidateAsync_WithInvalidFile_ReturnsFalse()
        {
            var isValid = await _validator.ValidateAsync("/nonexistent/file.jpg");

            Assert.IsFalse(isValid);
        }

        #endregion

        #region Supported Formats

        [TestMethod]
        public void Validate_SupportJpg()
        {
            var jpgPath = Path.Combine(Path.GetTempPath(), $"test-{Guid.NewGuid()}.jpg");
            File.WriteAllBytes(jpgPath, new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 });

            try
            {
                var isValid = _validator.Validate(jpgPath);
                Assert.IsTrue(isValid);
            }
            finally
            {
                if (File.Exists(jpgPath))
                    File.Delete(jpgPath);
            }
        }

        [TestMethod]
        public void Validate_SupportJpeg()
        {
            var jpegPath = Path.Combine(Path.GetTempPath(), $"test-{Guid.NewGuid()}.jpeg");
            File.WriteAllBytes(jpegPath, new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 });

            try
            {
                var isValid = _validator.Validate(jpegPath);
                Assert.IsTrue(isValid);
            }
            finally
            {
                if (File.Exists(jpegPath))
                    File.Delete(jpegPath);
            }
        }

        [TestMethod]
        public void Validate_SupportWebp()
        {
            var webpPath = Path.Combine(Path.GetTempPath(), $"test-{Guid.NewGuid()}.webp");
            File.WriteAllBytes(webpPath, new byte[] { 0x52, 0x49, 0x46, 0x46, 0x00, 0x00, 0x00, 0x00, 0x57, 0x45, 0x42, 0x50 });

            try
            {
                var isValid = _validator.Validate(webpPath);
                Assert.IsTrue(isValid);
            }
            finally
            {
                if (File.Exists(webpPath))
                    File.Delete(webpPath);
            }
        }

        [TestMethod]
        public void Validate_SupportPng()
        {
            var pngPath = Path.Combine(Path.GetTempPath(), $"test-{Guid.NewGuid()}.png");
            File.WriteAllBytes(pngPath, new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });

            try
            {
                var isValid = _validator.Validate(pngPath);
                Assert.IsTrue(isValid);
            }
            finally
            {
                if (File.Exists(pngPath))
                    File.Delete(pngPath);
            }
        }

        #endregion
    }
}
