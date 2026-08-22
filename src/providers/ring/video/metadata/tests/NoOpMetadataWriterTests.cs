using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;

namespace VideoForensics.Providers.Ring.Video.Metadata.Tests
{
    [TestClass]
    public class NoOpMetadataWriterTests
    {
        private IMetadataWriter _writer = null!;
        private string _testFilePath = null!;
        private IFileSystem _fileSystem = null!;

        [TestInitialize]
        public void Setup()
        {
            // Use real file system for tests (MockFileSystem has limitations with streams)
            _fileSystem = new System.IO.Abstractions.FileSystem();
            _writer = new NoOpMetadataWriter(_fileSystem);
            _testFilePath = Path.Combine(Path.GetTempPath(), $"test-video-{Guid.NewGuid()}.mp4");
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
        public void WriteMetadata_WithValidFileAndMetadata_ReturnsSuccessResult()
        {
            _fileSystem.File.WriteAllBytes(_testFilePath, new byte[] { 0x00, 0x00, 0x00, 0x20, 0x66, 0x74, 0x79, 0x70 }); // MP4 header

            var metadata = new VideoMetadata
            {
                EventDateTime = DateTime.Now,
                DeviceName = "Test Camera",
                PersonDetected = true
            };

            var result = _writer.WriteMetadata(_testFilePath, metadata);

            Assert.IsNotNull(result);
            Assert.AreEqual(MetadataStatus.Valid, result.Status);
            Assert.IsTrue(result.IsValid);
            Assert.IsFalse(result.WasWritten); // No-op doesn't actually write
            Assert.IsFalse(result.WasCorrected);
        }

        [TestMethod]
        public void WriteMetadata_WithNullFilePath_ThrowsArgumentException()
        {
            var metadata = new VideoMetadata { DeviceName = "Test" };

            try
            {
                _writer.WriteMetadata(null!, metadata);
                Assert.Fail("Expected ArgumentException to be thrown");
            }
            catch (ArgumentException)
            {
                // expected
            }
        }

        [TestMethod]
        public void WriteMetadata_WithEmptyFilePath_ThrowsArgumentException()
        {
            var metadata = new VideoMetadata { DeviceName = "Test" };

            try
            {
                _writer.WriteMetadata("", metadata);
                Assert.Fail("Expected ArgumentException to be thrown");
            }
            catch (ArgumentException)
            {
                // expected
            }
        }

        [TestMethod]
        public void WriteMetadata_WithNullMetadata_ThrowsArgumentNullException()
        {
            _fileSystem.File.WriteAllBytes(_testFilePath, new byte[] { 0x00, 0x00, 0x00, 0x20, 0x66, 0x74, 0x79, 0x70 });

            try
            {
                _writer.WriteMetadata(_testFilePath, null!);
                Assert.Fail("Expected ArgumentNullException to be thrown");
            }
            catch (ArgumentNullException)
            {
                // expected
            }
        }

        [TestMethod]
        public void WriteMetadata_WithNonExistentFile_ReturnsFailed()
        {
            var nonExistentPath = Path.Combine(Path.GetTempPath(), $"nonexistent-{Guid.NewGuid()}.mp4");
            var metadata = new VideoMetadata { DeviceName = "Test" };

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
            File.WriteAllBytes(invalidPath, new byte[] { 0x00 });

            try
            {
                var metadata = new VideoMetadata { DeviceName = "Test" };
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
            _fileSystem.File.WriteAllBytes(_testFilePath, new byte[] { 0x00, 0x00, 0x00, 0x20, 0x66, 0x74, 0x79, 0x70 });

            var metadata = new VideoMetadata { PersonDetected = true };
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
            _fileSystem.File.WriteAllBytes(_testFilePath, new byte[] { 0x00, 0x00, 0x00, 0x20, 0x66, 0x74, 0x79, 0x70 });

            var metadata = new VideoMetadata { DeviceName = "Test Camera" };

            var result = await _writer.WriteMetadataAsync(_testFilePath, metadata);

            Assert.AreEqual(MetadataStatus.Valid, result.Status);
            Assert.IsTrue(result.IsValid);
        }

        [TestMethod]
        public async Task WriteMetadataAsync_WithNonExistentFile_ReturnsFailed()
        {
            var nonExistentPath = Path.Combine(Path.GetTempPath(), $"nonexistent-{Guid.NewGuid()}.mp4");
            var metadata = new VideoMetadata { DeviceName = "Test" };

            var result = await _writer.WriteMetadataAsync(nonExistentPath, metadata);

            Assert.AreEqual(MetadataStatus.Failed, result.Status);
        }

        #endregion

        #region ValidateVideo Tests

        [TestMethod]
        public void ValidateVideo_WithValidFile_ReturnsValid()
        {
            _fileSystem.File.WriteAllBytes(_testFilePath, new byte[] { 0x00, 0x00, 0x00, 0x20, 0x66, 0x74, 0x79, 0x70 });

            var result = _writer.ValidateVideo(_testFilePath);

            Assert.AreEqual(MetadataStatus.Valid, result.Status);
            Assert.IsTrue(result.IsValid);
            Assert.IsFalse(result.WasWritten);
            Assert.IsFalse(result.WasCorrected);
        }

        [TestMethod]
        public void ValidateVideo_WithNonExistentFile_ReturnsFailed()
        {
            var nonExistentPath = Path.Combine(Path.GetTempPath(), $"nonexistent-{Guid.NewGuid()}.mp4");

            var result = _writer.ValidateVideo(nonExistentPath);

            Assert.AreEqual(MetadataStatus.Failed, result.Status);
            Assert.IsFalse(result.IsValid);
        }

        [TestMethod]
        public void ValidateVideo_WithInvalidFileFormat_ReturnsCorrupt()
        {
            var invalidPath = Path.Combine(Path.GetTempPath(), $"test-{Guid.NewGuid()}.mp4");
            File.WriteAllBytes(invalidPath, new byte[] { 0xFF, 0xD8, 0xFF }); // JPEG header, not MP4

            try
            {
                var result = _writer.ValidateVideo(invalidPath);

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
        public void ValidateVideo_WithNullPath_ThrowsArgumentException()
        {
            try
            {
                _writer.ValidateVideo(null!);
                Assert.Fail("Expected ArgumentException to be thrown");
            }
            catch (ArgumentException)
            {
                // expected
            }
        }

        #endregion

        #region ValidateVideoAsync Tests

        [TestMethod]
        public async Task ValidateVideoAsync_WithValidFile_ReturnsValid()
        {
            _fileSystem.File.WriteAllBytes(_testFilePath, new byte[] { 0x00, 0x00, 0x00, 0x20, 0x66, 0x74, 0x79, 0x70 });

            var result = await _writer.ValidateVideoAsync(_testFilePath);

            Assert.AreEqual(MetadataStatus.Valid, result.Status);
            Assert.IsTrue(result.IsValid);
        }

        [TestMethod]
        public async Task ValidateVideoAsync_WithNonExistentFile_ReturnsFailed()
        {
            var nonExistentPath = Path.Combine(Path.GetTempPath(), $"nonexistent-{Guid.NewGuid()}.mp4");

            var result = await _writer.ValidateVideoAsync(nonExistentPath);

            Assert.AreEqual(MetadataStatus.Failed, result.Status);
        }

        #endregion

        #region PhotoPrism Tags Tests

        [TestMethod]
        public void WriteMetadata_WithPersonDetected_IncludesPhotoPrismTags()
        {
            _fileSystem.File.WriteAllBytes(_testFilePath, new byte[] { 0x00, 0x00, 0x00, 0x20, 0x66, 0x74, 0x79, 0x70 });

            var metadata = new VideoMetadata
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
            _fileSystem.File.WriteAllBytes(_testFilePath, new byte[] { 0x00, 0x00, 0x00, 0x20, 0x66, 0x74, 0x79, 0x70 });

            var metadata = new VideoMetadata
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
            _fileSystem.File.WriteAllBytes(_testFilePath, new byte[] { 0x00, 0x00, 0x00, 0x20, 0x66, 0x74, 0x79, 0x70 });

            var metadata = new VideoMetadata { DeviceName = "Inactive Camera" };

            var result = _writer.WriteMetadata(_testFilePath, metadata);

            // Tags can be null or empty
            var hasRelevantTags = result.PhotoPrismTags == null || result.PhotoPrismTags.Count == 0;
            Assert.IsTrue(hasRelevantTags);
        }

        #endregion

        #region Supported Formats Tests

        [TestMethod]
        public void ValidateVideo_SupportsMp4()
        {
            var path = Path.Combine(Path.GetTempPath(), $"test-{Guid.NewGuid()}.mp4");
            File.WriteAllBytes(path, new byte[] { 0x00, 0x00, 0x00, 0x20, 0x66, 0x74, 0x79, 0x70 });

            try
            {
                var result = _writer.ValidateVideo(path);
                Assert.AreEqual(MetadataStatus.Valid, result.Status);
            }
            finally
            {
                if (_fileSystem.File.Exists(path)) _fileSystem.File.Delete(path);
            }
        }

        [TestMethod]
        public void ValidateVideo_SupportsMov()
        {
            var path = Path.Combine(Path.GetTempPath(), $"test-{Guid.NewGuid()}.mov");
            File.WriteAllBytes(path, new byte[] { 0x00, 0x00, 0x00, 0x20, 0x66, 0x74, 0x79, 0x70 });

            try
            {
                var result = _writer.ValidateVideo(path);
                Assert.AreEqual(MetadataStatus.Valid, result.Status);
            }
            finally
            {
                if (_fileSystem.File.Exists(path)) _fileSystem.File.Delete(path);
            }
        }

        [TestMethod]
        public void ValidateVideo_SupportsMkv()
        {
            var path = Path.Combine(Path.GetTempPath(), $"test-{Guid.NewGuid()}.mkv");
            File.WriteAllBytes(path, new byte[] { 0x1A, 0x45, 0xDF, 0xA3 }); // MKV header

            try
            {
                var result = _writer.ValidateVideo(path);
                Assert.AreEqual(MetadataStatus.Valid, result.Status);
            }
            finally
            {
                if (_fileSystem.File.Exists(path)) _fileSystem.File.Delete(path);
            }
        }

        #endregion

        #region Result Timing Tests

        [TestMethod]
        public void WriteMetadata_ResultDurationIsReasonable()
        {
            _fileSystem.File.WriteAllBytes(_testFilePath, new byte[] { 0x00, 0x00, 0x00, 0x20, 0x66, 0x74, 0x79, 0x70 });

            var metadata = new VideoMetadata { DeviceName = "Test" };
            var result = _writer.WriteMetadata(_testFilePath, metadata);

            Assert.IsTrue(result.DurationMs < 1000, "Operation should complete in less than 1 second");
        }

        [TestMethod]
        public void WriteMetadata_ProcessedAtIsRecent()
        {
            _fileSystem.File.WriteAllBytes(_testFilePath, new byte[] { 0x00, 0x00, 0x00, 0x20, 0x66, 0x74, 0x79, 0x70 });

            var metadata = new VideoMetadata { DeviceName = "Test" };
            var beforeTime = DateTime.UtcNow;
            var result = _writer.WriteMetadata(_testFilePath, metadata);
            var afterTime = DateTime.UtcNow;

            Assert.IsTrue(result.ProcessedAt >= beforeTime, "ProcessedAt should be after operation start");
            Assert.IsTrue(result.ProcessedAt <= afterTime.AddSeconds(1), "ProcessedAt should be close to operation end");
        }

        #endregion
    }
}
