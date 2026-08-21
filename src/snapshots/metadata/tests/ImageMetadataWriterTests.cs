using System.IO.Abstractions;
using Ring.Api.Snapshots.Metadata.Models;

namespace Ring.Api.Snapshots.Metadata.Tests
{
    [TestClass]
    public class ImageMetadataWriterTests
    {
        private IMetadataWriter _writer = null!;
        private IMetadataValidator _validator = null!;
        private string _testFilePath = null!;
        private IFileSystem _fileSystem = null!;

        [TestInitialize]
        public void Setup()
        {
            _fileSystem = new System.IO.Abstractions.FileSystem();
            _validator = new ImageMetadataValidator(_fileSystem);
            _writer = new ImageMetadataWriter(_fileSystem, _validator);
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

        #region WriteMetadata Tests

        [TestMethod]
        public void WriteMetadata_WithValidJpegAndMetadata_ReturnsSuccessResult()
        {
            var jpegHeader = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 };
            _fileSystem.File.WriteAllBytes(_testFilePath, jpegHeader);

            var metadata = new SnapshotMetadata
            {
                EventDateTime = DateTime.Now,
                DeviceName = "Test Camera",
                PersonDetected = true
            };

            var result = _writer.WriteMetadata(_testFilePath, metadata);

            Assert.IsNotNull(result);
            Assert.AreEqual(MetadataStatus.Valid, result.Status);
            Assert.IsTrue(result.IsValid);
            Assert.IsFalse(result.WasCorrected);
        }

        [TestMethod]
        public void WriteMetadata_WithNullFilePath_ThrowsArgumentException()
        {
            var metadata = new SnapshotMetadata { DeviceName = "Test" };

            try
            {
                _writer.WriteMetadata(null!, metadata);
                Assert.Fail("Expected ArgumentException to be thrown");
            }
            catch (ArgumentException)
            {
            }
        }

        [TestMethod]
        public void WriteMetadata_WithEmptyFilePath_ThrowsArgumentException()
        {
            var metadata = new SnapshotMetadata { DeviceName = "Test" };

            try
            {
                _writer.WriteMetadata("", metadata);
                Assert.Fail("Expected ArgumentException to be thrown");
            }
            catch (ArgumentException)
            {
            }
        }

        [TestMethod]
        public void WriteMetadata_WithNullMetadata_ThrowsArgumentNullException()
        {
            var jpegHeader = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 };
            _fileSystem.File.WriteAllBytes(_testFilePath, jpegHeader);

            try
            {
                _writer.WriteMetadata(_testFilePath, null!);
                Assert.Fail("Expected ArgumentNullException to be thrown");
            }
            catch (ArgumentNullException)
            {
            }
        }

        [TestMethod]
        public void WriteMetadata_WithNonExistentFile_ReturnsFailed()
        {
            var nonExistentPath = Path.Combine(Path.GetTempPath(), $"nonexistent-{Guid.NewGuid()}.jpg");
            var metadata = new SnapshotMetadata { DeviceName = "Test" };

            var result = _writer.WriteMetadata(nonExistentPath, metadata);

            Assert.AreEqual(MetadataStatus.Failed, result.Status);
            Assert.IsFalse(result.IsValid);
            Assert.IsNotNull(result.ErrorMessage);
            StringAssert.Contains(result.ErrorMessage, "not found");
        }

        [TestMethod]
        public void WriteMetadata_WithInvalidFileExtension_ReturnsFailed()
        {
            var invalidPath = Path.Combine(Path.GetTempPath(), $"test-{Guid.NewGuid()}.txt");
            File.WriteAllBytes(invalidPath, new byte[] { 0xFF, 0xD8 });

            try
            {
                var metadata = new SnapshotMetadata { DeviceName = "Test" };
                var result = _writer.WriteMetadata(invalidPath, metadata);

                Assert.AreEqual(MetadataStatus.Failed, result.Status);
                Assert.IsFalse(result.IsValid);
            }
            finally
            {
                if (File.Exists(invalidPath))
                    File.Delete(invalidPath);
            }
        }

        [TestMethod]
        public void WriteMetadata_ResultHasValidProperties()
        {
            var jpegHeader = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 };
            _fileSystem.File.WriteAllBytes(_testFilePath, jpegHeader);

            var metadata = new SnapshotMetadata { PersonDetected = true };
            var result = _writer.WriteMetadata(_testFilePath, metadata);

            Assert.IsTrue(result.DurationMs >= 0);
            Assert.IsTrue(result.ProcessedAt <= DateTime.UtcNow);
            Assert.IsFalse(result.WasCorrected);
        }

        #endregion

        #region WriteMetadataAsync Tests

        [TestMethod]
        public async Task WriteMetadataAsync_WithValidFile_ReturnsSuccessResult()
        {
            var jpegHeader = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 };
            _fileSystem.File.WriteAllBytes(_testFilePath, jpegHeader);

            var metadata = new SnapshotMetadata { DeviceName = "Test Camera" };

            var result = await _writer.WriteMetadataAsync(_testFilePath, metadata);

            Assert.AreEqual(MetadataStatus.Valid, result.Status);
            Assert.IsTrue(result.IsValid);
        }

        [TestMethod]
        public async Task WriteMetadataAsync_WithNonExistentFile_ReturnsFailed()
        {
            var nonExistentPath = Path.Combine(Path.GetTempPath(), $"nonexistent-{Guid.NewGuid()}.jpg");
            var metadata = new SnapshotMetadata { DeviceName = "Test" };

            var result = await _writer.WriteMetadataAsync(nonExistentPath, metadata);

            Assert.AreEqual(MetadataStatus.Failed, result.Status);
        }

        #endregion

        #region ValidateImage Tests

        [TestMethod]
        public void ValidateImage_WithValidFile_ReturnsValid()
        {
            var jpegHeader = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 };
            _fileSystem.File.WriteAllBytes(_testFilePath, jpegHeader);

            var result = _writer.ValidateImage(_testFilePath);

            Assert.AreEqual(MetadataStatus.Valid, result.Status);
            Assert.IsTrue(result.IsValid);
            Assert.IsFalse(result.WasWritten);
            Assert.IsFalse(result.WasCorrected);
        }

        [TestMethod]
        public void ValidateImage_WithNonExistentFile_ReturnsFailed()
        {
            var nonExistentPath = Path.Combine(Path.GetTempPath(), $"nonexistent-{Guid.NewGuid()}.jpg");

            var result = _writer.ValidateImage(nonExistentPath);

            Assert.AreEqual(MetadataStatus.Failed, result.Status);
            Assert.IsFalse(result.IsValid);
        }

        [TestMethod]
        public void ValidateImage_WithInvalidFileFormat_ReturnsCorrupt()
        {
            var invalidPath = Path.Combine(Path.GetTempPath(), $"test-{Guid.NewGuid()}.jpg");
            File.WriteAllBytes(invalidPath, new byte[] { 0x47, 0x49, 0x46 }); // GIF header, not JPEG

            try
            {
                var result = _writer.ValidateImage(invalidPath);

                Assert.AreEqual(MetadataStatus.Corrupt, result.Status);
                Assert.IsFalse(result.IsValid);
            }
            finally
            {
                if (File.Exists(invalidPath))
                    File.Delete(invalidPath);
            }
        }

        [TestMethod]
        public void ValidateImage_WithNullPath_ThrowsArgumentException()
        {
            try
            {
                _writer.ValidateImage(null!);
                Assert.Fail("Expected ArgumentException to be thrown");
            }
            catch (ArgumentException)
            {
            }
        }

        #endregion

        #region ValidateImageAsync Tests

        [TestMethod]
        public async Task ValidateImageAsync_WithValidFile_ReturnsValid()
        {
            var jpegHeader = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 };
            _fileSystem.File.WriteAllBytes(_testFilePath, jpegHeader);

            var result = await _writer.ValidateImageAsync(_testFilePath);

            Assert.AreEqual(MetadataStatus.Valid, result.Status);
            Assert.IsTrue(result.IsValid);
        }

        [TestMethod]
        public async Task ValidateImageAsync_WithNonExistentFile_ReturnsFailed()
        {
            var nonExistentPath = Path.Combine(Path.GetTempPath(), $"nonexistent-{Guid.NewGuid()}.jpg");

            var result = await _writer.ValidateImageAsync(nonExistentPath);

            Assert.AreEqual(MetadataStatus.Failed, result.Status);
        }

        #endregion

        #region PhotoPrism Tags Tests

        [TestMethod]
        public void WriteMetadata_WithPersonDetected_IncludesPhotoPrismTags()
        {
            var jpegHeader = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 };
            _fileSystem.File.WriteAllBytes(_testFilePath, jpegHeader);

            var metadata = new SnapshotMetadata
            {
                PersonDetected = true,
                EventType = "person"
            };

            var result = _writer.WriteMetadata(_testFilePath, metadata);

            Assert.IsNotNull(result.PhotoPrismTags);
            Assert.IsTrue(result.PhotoPrismTags.Contains("person"));
        }

        [TestMethod]
        public void WriteMetadata_WithMotionDetected_IncludesMotionTag()
        {
            var jpegHeader = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 };
            _fileSystem.File.WriteAllBytes(_testFilePath, jpegHeader);

            var metadata = new SnapshotMetadata
            {
                MotionDetected = true,
                EventType = "motion"
            };

            var result = _writer.WriteMetadata(_testFilePath, metadata);

            Assert.IsNotNull(result.PhotoPrismTags);
            Assert.IsTrue(result.PhotoPrismTags.Contains("motion"));
        }

        [TestMethod]
        public void WriteMetadata_WithoutEvents_NoPhotoPrismTags()
        {
            var jpegHeader = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 };
            _fileSystem.File.WriteAllBytes(_testFilePath, jpegHeader);

            var metadata = new SnapshotMetadata { DeviceName = "Inactive Camera" };

            var result = _writer.WriteMetadata(_testFilePath, metadata);

            var hasRelevantTags = result.PhotoPrismTags == null || result.PhotoPrismTags.Count == 0;
            Assert.IsTrue(hasRelevantTags);
        }

        #endregion

        #region Supported Formats Tests

        [TestMethod]
        public void WriteMetadata_SupportsJpeg()
        {
            var jpegPath = Path.Combine(Path.GetTempPath(), $"test-{Guid.NewGuid()}.jpeg");
            File.WriteAllBytes(jpegPath, new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 });

            try
            {
                var metadata = new SnapshotMetadata { DeviceName = "Test" };
                var result = _writer.WriteMetadata(jpegPath, metadata);
                Assert.AreEqual(MetadataStatus.Valid, result.Status);
            }
            finally
            {
                if (_fileSystem.File.Exists(jpegPath))
                    _fileSystem.File.Delete(jpegPath);
            }
        }

        [TestMethod]
        public void WriteMetadata_SupportsPng()
        {
            var pngPath = Path.Combine(Path.GetTempPath(), $"test-{Guid.NewGuid()}.png");
            File.WriteAllBytes(pngPath, new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });

            try
            {
                var metadata = new SnapshotMetadata { DeviceName = "Test" };
                var result = _writer.WriteMetadata(pngPath, metadata);
                Assert.AreEqual(MetadataStatus.Valid, result.Status);
            }
            finally
            {
                if (_fileSystem.File.Exists(pngPath))
                    _fileSystem.File.Delete(pngPath);
            }
        }

        [TestMethod]
        public void WriteMetadata_SupportsWebp()
        {
            var webpPath = Path.Combine(Path.GetTempPath(), $"test-{Guid.NewGuid()}.webp");
            File.WriteAllBytes(webpPath, new byte[] { 0x52, 0x49, 0x46, 0x46, 0x00, 0x00, 0x00, 0x00, 0x57, 0x45, 0x42, 0x50 });

            try
            {
                var metadata = new SnapshotMetadata { DeviceName = "Test" };
                var result = _writer.WriteMetadata(webpPath, metadata);
                Assert.AreEqual(MetadataStatus.Valid, result.Status);
            }
            finally
            {
                if (_fileSystem.File.Exists(webpPath))
                    _fileSystem.File.Delete(webpPath);
            }
        }

        #endregion

        #region Result Timing Tests

        [TestMethod]
        public void WriteMetadata_ResultDurationIsReasonable()
        {
            var jpegHeader = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 };
            _fileSystem.File.WriteAllBytes(_testFilePath, jpegHeader);

            var metadata = new SnapshotMetadata { DeviceName = "Test" };
            var result = _writer.WriteMetadata(_testFilePath, metadata);

            Assert.IsTrue(result.DurationMs < 1000, "Operation should complete in less than 1 second");
        }

        [TestMethod]
        public void WriteMetadata_ProcessedAtIsRecent()
        {
            var jpegHeader = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 };
            _fileSystem.File.WriteAllBytes(_testFilePath, jpegHeader);

            var metadata = new SnapshotMetadata { DeviceName = "Test" };
            var beforeTime = DateTime.UtcNow;
            var result = _writer.WriteMetadata(_testFilePath, metadata);
            var afterTime = DateTime.UtcNow;

            Assert.IsTrue(result.ProcessedAt >= beforeTime, "ProcessedAt should be after operation start");
            Assert.IsTrue(result.ProcessedAt <= afterTime.AddSeconds(1), "ProcessedAt should be close to operation end");
        }

        #endregion

        #region Image Property Extraction

        [TestMethod]
        public void WriteMetadata_ExtractsImageFormat()
        {
            var jpegHeader = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 };
            _fileSystem.File.WriteAllBytes(_testFilePath, jpegHeader);

            var metadata = new SnapshotMetadata { DeviceName = "Test" };
            var result = _writer.WriteMetadata(_testFilePath, metadata);

            Assert.IsNotNull(metadata.ImageFormat);
            Assert.AreEqual("JPEG", metadata.ImageFormat);
        }

        [TestMethod]
        public void WriteMetadata_ExtractsImageFileSize()
        {
            var jpegHeader = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x00, 0x00, 0x00 };
            _fileSystem.File.WriteAllBytes(_testFilePath, jpegHeader);

            var metadata = new SnapshotMetadata { DeviceName = "Test" };
            var result = _writer.WriteMetadata(_testFilePath, metadata);

            Assert.IsTrue(metadata.ImageFileSize >= 0);
        }

        [TestMethod]
        public void WriteMetadata_EstimatesImageQuality()
        {
            var jpegHeader = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 };
            _fileSystem.File.WriteAllBytes(_testFilePath, jpegHeader);

            var metadata = new SnapshotMetadata { DeviceName = "Test" };
            var result = _writer.WriteMetadata(_testFilePath, metadata);

            Assert.IsTrue(metadata.ImageQualityScore >= 0);
            Assert.IsTrue(metadata.ImageQualityScore <= 100);
        }

        #endregion
    }
}
