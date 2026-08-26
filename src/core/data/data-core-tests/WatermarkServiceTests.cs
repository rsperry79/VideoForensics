using Microsoft.Extensions.Logging;
using Moq;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Core.Services;
using Xunit;

namespace VideoForensics.Data.Core.Tests
{
    public class WatermarkServiceTests
    {
        private readonly Mock<IDownloadEventRepository> _mockDownloadEventRepository;
        private readonly Mock<ILogger<WatermarkService>> _mockLogger;
        private readonly WatermarkService _watermarkService;

        public WatermarkServiceTests()
        {
            _mockDownloadEventRepository = new Mock<IDownloadEventRepository>();
            _mockLogger = new Mock<ILogger<WatermarkService>>();
            _watermarkService = new WatermarkService(_mockDownloadEventRepository.Object, _mockLogger.Object);
        }

        [Fact]
        public async Task ResolveStartDateAsync_ForceTrue_ReturnsRequestedStartDate()
        {
            // Arrange
            var deviceId = Guid.NewGuid();
            var requestedDate = new DateTime(2024, 1, 15, 12, 0, 0, DateTimeKind.Utc);
            var watermarkDate = new DateTime(2024, 1, 10, 12, 0, 0, DateTimeKind.Utc);

            _mockDownloadEventRepository
                .Setup(x => x.GetLatestSuccessfulEventTimeAsync(deviceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(watermarkDate);

            // Act
            var result = await _watermarkService.ResolveStartDateAsync(deviceId, requestedDate, force: true, CancellationToken.None);

            // Assert
            Assert.Equal(requestedDate, result);
            _mockDownloadEventRepository.Verify(
                x => x.GetLatestSuccessfulEventTimeAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Fact]
        public async Task ResolveStartDateAsync_ForcefalseNoWatermark_ReturnsRequestedStartDate()
        {
            // Arrange
            var deviceId = Guid.NewGuid();
            var requestedDate = new DateTime(2024, 1, 15, 12, 0, 0, DateTimeKind.Utc);

            _mockDownloadEventRepository
                .Setup(x => x.GetLatestSuccessfulEventTimeAsync(deviceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((DateTime?)null);

            // Act
            var result = await _watermarkService.ResolveStartDateAsync(deviceId, requestedDate, force: false, CancellationToken.None);

            // Assert
            Assert.Equal(requestedDate, result);
        }

        [Fact]
        public async Task ResolveStartDateAsync_ForcefalsWithWatermarkLaterThanRequested_ReturnsWatermarkMinusBuffer()
        {
            // Arrange
            var deviceId = Guid.NewGuid();
            var requestedDate = new DateTime(2024, 1, 10, 12, 0, 0, DateTimeKind.Utc);
            var watermarkDate = new DateTime(2024, 1, 20, 12, 0, 0, DateTimeKind.Utc);
            var expectedDate = new DateTime(2024, 1, 20, 11, 0, 0, DateTimeKind.Utc); // watermark - 1 hour

            _mockDownloadEventRepository
                .Setup(x => x.GetLatestSuccessfulEventTimeAsync(deviceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(watermarkDate);

            // Act
            var result = await _watermarkService.ResolveStartDateAsync(deviceId, requestedDate, force: false, CancellationToken.None);

            // Assert
            Assert.Equal(expectedDate, result);
        }

        [Fact]
        public async Task ResolveStartDateAsync_ForcefalseWithWatermarkEarlierThanRequested_ReturnsRequestedStartDate()
        {
            // Arrange
            var deviceId = Guid.NewGuid();
            var requestedDate = new DateTime(2024, 1, 20, 12, 0, 0, DateTimeKind.Utc);
            var watermarkDate = new DateTime(2024, 1, 10, 12, 0, 0, DateTimeKind.Utc);

            _mockDownloadEventRepository
                .Setup(x => x.GetLatestSuccessfulEventTimeAsync(deviceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(watermarkDate);

            // Act
            var result = await _watermarkService.ResolveStartDateAsync(deviceId, requestedDate, force: false, CancellationToken.None);

            // Assert
            // Should return max(requestedDate, watermarkDate - buffer)
            // watermarkDate - 1 hour = 2024-01-10 11:00:00
            // max(2024-01-20 12:00:00, 2024-01-10 11:00:00) = 2024-01-20 12:00:00
            Assert.Equal(requestedDate, result);
        }

        [Fact]
        public async Task ResolveStartDateAsync_ForcefalseWithWatermarkExactly1HourAfterRequested_ReturnsWatermarkMinusBuffer()
        {
            // Arrange
            var deviceId = Guid.NewGuid();
            var requestedDate = new DateTime(2024, 1, 15, 12, 0, 0, DateTimeKind.Utc);
            var watermarkDate = new DateTime(2024, 1, 15, 13, 0, 0, DateTimeKind.Utc);
            var expectedDate = new DateTime(2024, 1, 15, 12, 0, 0, DateTimeKind.Utc); // watermark - 1 hour

            _mockDownloadEventRepository
                .Setup(x => x.GetLatestSuccessfulEventTimeAsync(deviceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(watermarkDate);

            // Act
            var result = await _watermarkService.ResolveStartDateAsync(deviceId, requestedDate, force: false, CancellationToken.None);

            // Assert
            Assert.Equal(expectedDate, result);
        }
    }
}
