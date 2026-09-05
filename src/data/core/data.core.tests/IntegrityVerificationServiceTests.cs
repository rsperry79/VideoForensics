using Microsoft.Extensions.Logging;

using Moq;

using System.Security.Cryptography;

using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Core.Services;

using Xunit;

namespace VideoForensics.Data.Core.Tests
{
    public class IntegrityVerificationServiceTests
    {
        private readonly Mock<IMediaItemRepository> _mockMediaItemRepository;
        private readonly Mock<IIntegrityRecordRepository> _mockIntegrityRecordRepository;
        private readonly Mock<ILogger<IntegrityVerificationService>> _mockLogger;
        private readonly IntegrityVerificationService _service;

        public IntegrityVerificationServiceTests()
        {
            _mockMediaItemRepository = new Mock<IMediaItemRepository>();
            _mockIntegrityRecordRepository = new Mock<IIntegrityRecordRepository>();
            _mockLogger = new Mock<ILogger<IntegrityVerificationService>>();
            _service = new IntegrityVerificationService(
                _mockMediaItemRepository.Object,
                _mockIntegrityRecordRepository.Object,
                _mockLogger.Object);
        }

        [Fact]
        public async Task ComputeHashAsync_WithValidFile_ReturnsCorrectSha256Hash()
        {
            // Arrange
            string testContent = "This is a test file content for hashing";
            string tempFilePath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.txt");

            try
            {
                // Write test file
                await File.WriteAllTextAsync(tempFilePath, testContent);

                // Compute expected hash independently
                string expectedHash = await ComputeExpectedHash(tempFilePath);

                // Act
                string result = await _service.ComputeHashAsync(tempFilePath, CancellationToken.None);

                // Assert
                Assert.Equal(expectedHash, result);
                Assert.NotEmpty(result);
                Assert.Equal(64, result.Length); // SHA256 hex string is 64 chars
            }
            finally
            {
                if (File.Exists(tempFilePath))
                {
                    File.Delete(tempFilePath);
                }
            }
        }

        [Fact]
        public async Task ComputeHashAsync_WithNonExistentFile_ThrowsFileNotFoundException()
        {
            // Arrange
            string nonExistentPath = Path.Combine(Path.GetTempPath(), $"nonexistent_{Guid.NewGuid()}.txt");

            // Act & Assert
            _ = await Assert.ThrowsAsync<FileNotFoundException>(
                () => _service.ComputeHashAsync(nonExistentPath, CancellationToken.None));
        }

        [Fact]
        public async Task ComputeHashAsync_ReturnsLowercaseHex()
        {
            // Arrange
            string testContent = "uppercase test";
            string tempFilePath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.txt");

            try
            {
                await File.WriteAllTextAsync(tempFilePath, testContent);

                // Act
                string result = await _service.ComputeHashAsync(tempFilePath, CancellationToken.None);

                // Assert
                Assert.Equal(result, result.ToLowerInvariant());
            }
            finally
            {
                if (File.Exists(tempFilePath))
                {
                    File.Delete(tempFilePath);
                }
            }
        }

        [Fact]
        public async Task VerifyAsync_WithMatchingHash_ReturnsTrue()
        {
            // Arrange
            string testContent = "verify matching hash";
            string tempFilePath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.txt");

            try
            {
                await File.WriteAllTextAsync(tempFilePath, testContent);
                string computedHash = await ComputeExpectedHash(tempFilePath);

                var mediaItem = new MediaItem
                {
                    Id = Guid.NewGuid(),
                    DeviceId = Guid.NewGuid(),
                    FileName = "test.txt",
                    FilePath = tempFilePath,
                    MediaFormat = "text/plain",
                    Sha256Hash = computedHash,
                    RecordedAtUtc = DateTime.UtcNow,
                    DownloadedAtUtc = DateTime.UtcNow
                };

                _ = _mockMediaItemRepository
                    .Setup(x => x.GetAsync(mediaItem.Id, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(mediaItem);

                // Act
                bool result = await _service.VerifyAsync(mediaItem.Id, CancellationToken.None);

                // Assert
                Assert.True(result);
            }
            finally
            {
                if (File.Exists(tempFilePath))
                {
                    File.Delete(tempFilePath);
                }
            }
        }

        [Fact]
        public async Task VerifyAsync_WithMismatchedHash_ReturnsFalse()
        {
            // Arrange
            string testContent = "verify mismatched hash";
            string tempFilePath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.txt");

            try
            {
                await File.WriteAllTextAsync(tempFilePath, testContent);

                var mediaItem = new MediaItem
                {
                    Id = Guid.NewGuid(),
                    DeviceId = Guid.NewGuid(),
                    FileName = "test.txt",
                    FilePath = tempFilePath,
                    MediaFormat = "text/plain",
                    Sha256Hash = "0000000000000000000000000000000000000000000000000000000000000000",
                    RecordedAtUtc = DateTime.UtcNow,
                    DownloadedAtUtc = DateTime.UtcNow
                };

                _ = _mockMediaItemRepository
                    .Setup(x => x.GetAsync(mediaItem.Id, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(mediaItem);

                // Act
                bool result = await _service.VerifyAsync(mediaItem.Id, CancellationToken.None);

                // Assert
                Assert.False(result);
            }
            finally
            {
                if (File.Exists(tempFilePath))
                {
                    File.Delete(tempFilePath);
                }
            }
        }

        [Fact]
        public async Task VerifyAsync_WithNonExistentMediaItem_ReturnsFalse()
        {
            // Arrange
            var mediaItemId = Guid.NewGuid();

            _ = _mockMediaItemRepository
                .Setup(x => x.GetAsync(mediaItemId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((MediaItem?)null);

            // Act
            bool result = await _service.VerifyAsync(mediaItemId, CancellationToken.None);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task VerifyAsync_WithNonExistentFile_ReturnsFalse()
        {
            // Arrange
            var mediaItem = new MediaItem
            {
                Id = Guid.NewGuid(),
                DeviceId = Guid.NewGuid(),
                FileName = "nonexistent.txt",
                FilePath = Path.Combine(Path.GetTempPath(), $"nonexistent_{Guid.NewGuid()}.txt"),
                MediaFormat = "text/plain",
                Sha256Hash = "abc123",
                RecordedAtUtc = DateTime.UtcNow,
                DownloadedAtUtc = DateTime.UtcNow
            };

            _ = _mockMediaItemRepository
                .Setup(x => x.GetAsync(mediaItem.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(mediaItem);

            // Act
            bool result = await _service.VerifyAsync(mediaItem.Id, CancellationToken.None);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task VerifyAllForDeviceAsync_VerifiesAllMediaItemsForDevice()
        {
            // Arrange
            var deviceId = Guid.NewGuid();
            string tempDir = Path.Combine(Path.GetTempPath(), $"verify_device_{Guid.NewGuid()}");
            _ = Directory.CreateDirectory(tempDir);

            try
            {
                var mediaItems = new List<MediaItem>();
                for (int i = 0; i < 3; i++)
                {
                    string filePath = Path.Combine(tempDir, $"file_{i}.txt");
                    await File.WriteAllTextAsync(filePath, $"content {i}");
                    string hash = await ComputeExpectedHash(filePath);

                    mediaItems.Add(new MediaItem
                    {
                        Id = Guid.NewGuid(),
                        DeviceId = deviceId,
                        FileName = $"file_{i}.txt",
                        FilePath = filePath,
                        MediaFormat = "text/plain",
                        Sha256Hash = hash,
                        RecordedAtUtc = DateTime.UtcNow,
                        DownloadedAtUtc = DateTime.UtcNow
                    });
                }

                _ = _mockMediaItemRepository
                    .Setup(x => x.GetByDeviceIdAsync(deviceId, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(mediaItems);

                foreach (var item in mediaItems)
                {
                    _ = _mockMediaItemRepository
                        .Setup(x => x.GetAsync(item.Id, It.IsAny<CancellationToken>()))
                        .ReturnsAsync(item);
                }

                // Act
                int result = await _service.VerifyAllForDeviceAsync(deviceId, CancellationToken.None);

                // Assert
                Assert.Equal(3, result);
                _mockMediaItemRepository.Verify(
                    x => x.GetByDeviceIdAsync(deviceId, It.IsAny<CancellationToken>()),
                    Times.Once);
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, recursive: true);
                }
            }
        }

        [Fact]
        public async Task VerifyAllForDeviceAsync_WithNoMediaItems_ReturnsZero()
        {
            // Arrange
            var deviceId = Guid.NewGuid();

            _ = _mockMediaItemRepository
                .Setup(x => x.GetByDeviceIdAsync(deviceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync([]);

            // Act
            int result = await _service.VerifyAllForDeviceAsync(deviceId, CancellationToken.None);

            // Assert
            Assert.Equal(0, result);
        }

        private static async Task<string> ComputeExpectedHash(string filePath)
        {
            using var fileStream = File.OpenRead(filePath);
            byte[] hash = await SHA256.HashDataAsync(fileStream, CancellationToken.None);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }
    }
}
