using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using Moq;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Core.Services;
using Xunit;

namespace VideoForensics.Data.Core.Tests
{
    public class IntegrityVerificationServiceTests
    {
        private readonly Mock<IMediaItemRepository> _mockMediaItemRepository;
        private readonly Mock<ILogger<IntegrityVerificationService>> _mockLogger;
        private readonly IntegrityVerificationService _service;

        public IntegrityVerificationServiceTests()
        {
            _mockMediaItemRepository = new Mock<IMediaItemRepository>();
            _mockLogger = new Mock<ILogger<IntegrityVerificationService>>();
            _service = new IntegrityVerificationService(_mockMediaItemRepository.Object, _mockLogger.Object);
        }

        [Fact]
        public async Task ComputeHashAsync_WithValidFile_ReturnsCorrectSha256Hash()
        {
            // Arrange
            var testContent = "This is a test file content for hashing";
            var tempFilePath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.txt");

            try
            {
                // Write test file
                await File.WriteAllTextAsync(tempFilePath, testContent);

                // Compute expected hash independently
                var expectedHash = await ComputeExpectedHash(tempFilePath);

                // Act
                var result = await _service.ComputeHashAsync(tempFilePath, CancellationToken.None);

                // Assert
                Assert.Equal(expectedHash, result);
                Assert.NotEmpty(result);
                Assert.Equal(64, result.Length); // SHA256 hex string is 64 chars
            }
            finally
            {
                if (File.Exists(tempFilePath))
                    File.Delete(tempFilePath);
            }
        }

        [Fact]
        public async Task ComputeHashAsync_WithNonExistentFile_ThrowsFileNotFoundException()
        {
            // Arrange
            var nonExistentPath = Path.Combine(Path.GetTempPath(), $"nonexistent_{Guid.NewGuid()}.txt");

            // Act & Assert
            await Assert.ThrowsAsync<FileNotFoundException>(
                () => _service.ComputeHashAsync(nonExistentPath, CancellationToken.None));
        }

        [Fact]
        public async Task ComputeHashAsync_ReturnsLowercaseHex()
        {
            // Arrange
            var testContent = "uppercase test";
            var tempFilePath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.txt");

            try
            {
                await File.WriteAllTextAsync(tempFilePath, testContent);

                // Act
                var result = await _service.ComputeHashAsync(tempFilePath, CancellationToken.None);

                // Assert
                Assert.Equal(result, result.ToLowerInvariant());
            }
            finally
            {
                if (File.Exists(tempFilePath))
                    File.Delete(tempFilePath);
            }
        }

        [Fact]
        public async Task VerifyAsync_WithMatchingHash_ReturnsTrue()
        {
            // Arrange
            var testContent = "verify matching hash";
            var tempFilePath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.txt");

            try
            {
                await File.WriteAllTextAsync(tempFilePath, testContent);
                var computedHash = await ComputeExpectedHash(tempFilePath);

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

                _mockMediaItemRepository
                    .Setup(x => x.GetAsync(mediaItem.Id, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(mediaItem);

                // Act
                var result = await _service.VerifyAsync(mediaItem.Id, CancellationToken.None);

                // Assert
                Assert.True(result);
            }
            finally
            {
                if (File.Exists(tempFilePath))
                    File.Delete(tempFilePath);
            }
        }

        [Fact]
        public async Task VerifyAsync_WithMismatchedHash_ReturnsFalse()
        {
            // Arrange
            var testContent = "verify mismatched hash";
            var tempFilePath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.txt");

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

                _mockMediaItemRepository
                    .Setup(x => x.GetAsync(mediaItem.Id, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(mediaItem);

                // Act
                var result = await _service.VerifyAsync(mediaItem.Id, CancellationToken.None);

                // Assert
                Assert.False(result);
            }
            finally
            {
                if (File.Exists(tempFilePath))
                    File.Delete(tempFilePath);
            }
        }

        [Fact]
        public async Task VerifyAsync_WithNonExistentMediaItem_ReturnsFalse()
        {
            // Arrange
            var mediaItemId = Guid.NewGuid();

            _mockMediaItemRepository
                .Setup(x => x.GetAsync(mediaItemId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((MediaItem?)null);

            // Act
            var result = await _service.VerifyAsync(mediaItemId, CancellationToken.None);

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

            _mockMediaItemRepository
                .Setup(x => x.GetAsync(mediaItem.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(mediaItem);

            // Act
            var result = await _service.VerifyAsync(mediaItem.Id, CancellationToken.None);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task VerifyAllForDeviceAsync_VerifiesAllMediaItemsForDevice()
        {
            // Arrange
            var deviceId = Guid.NewGuid();
            var tempDir = Path.Combine(Path.GetTempPath(), $"verify_device_{Guid.NewGuid()}");
            Directory.CreateDirectory(tempDir);

            try
            {
                var mediaItems = new List<MediaItem>();
                for (int i = 0; i < 3; i++)
                {
                    var filePath = Path.Combine(tempDir, $"file_{i}.txt");
                    await File.WriteAllTextAsync(filePath, $"content {i}");
                    var hash = await ComputeExpectedHash(filePath);

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

                _mockMediaItemRepository
                    .Setup(x => x.GetByDeviceIdAsync(deviceId, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(mediaItems);

                foreach (var item in mediaItems)
                {
                    _mockMediaItemRepository
                        .Setup(x => x.GetAsync(item.Id, It.IsAny<CancellationToken>()))
                        .ReturnsAsync(item);
                }

                // Act
                var result = await _service.VerifyAllForDeviceAsync(deviceId, CancellationToken.None);

                // Assert
                Assert.Equal(3, result);
                _mockMediaItemRepository.Verify(
                    x => x.GetByDeviceIdAsync(deviceId, It.IsAny<CancellationToken>()),
                    Times.Once);
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, recursive: true);
            }
        }

        [Fact]
        public async Task VerifyAllForDeviceAsync_WithNoMediaItems_ReturnsZero()
        {
            // Arrange
            var deviceId = Guid.NewGuid();

            _mockMediaItemRepository
                .Setup(x => x.GetByDeviceIdAsync(deviceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<MediaItem>());

            // Act
            var result = await _service.VerifyAllForDeviceAsync(deviceId, CancellationToken.None);

            // Assert
            Assert.Equal(0, result);
        }

        private static async Task<string> ComputeExpectedHash(string filePath)
        {
            using var fileStream = File.OpenRead(filePath);
            var hash = await SHA256.HashDataAsync(fileStream, CancellationToken.None);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }
    }
}
