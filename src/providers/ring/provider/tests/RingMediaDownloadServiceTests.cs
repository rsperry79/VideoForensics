using Microsoft.Extensions.Logging;
using Moq;
using VideoForensics.Data.Core.Contracts;
using VideoForensics.Providers.Common.Contracts;
using VideoForensics.Providers.Ring.Entities;
using VideoForensics.Providers.Ring.Services;
using Xunit;

namespace VideoForensics.Providers.Ring.Tests
{
    /// <summary>
    /// Tests for RingMediaDownloadService implementation.
    /// Verifies video and snapshot download functionality.
    /// </summary>
    public class RingMediaDownloadServiceTests
    {
        private static IVideoForensicsDataClient CreateMockDataClient()
        {
            var mock = new Mock<IVideoForensicsDataClient>();
            // For tests that don't actually call DB methods, this mock will suffice.
            // Real integration tests would set up more specific behaviors.
            return mock.Object;
        }
        [Fact]
        public async Task DownloadVideosAsync_WithoutSession_ReturnsFailureResult()
        {
            // Arrange
            var sessionProvider = new Mock<ISessionProvider>();
            sessionProvider.Setup(sp => sp.GetSession()).Returns((Session?)null);
            var logger = new Mock<ILogger>().Object;
            var dataClient = CreateMockDataClient();
            var service = new RingMediaDownloadService(logger, sessionProvider.Object, dataClient);

            // Act
            var result = await service.DownloadVideosAsync(
                "device123",
                "/tmp/videos",
                DateTime.Now.AddDays(-7),
                DateTime.Now
            );

            // Assert
            Assert.NotNull(result);
            Assert.False(result.Success);
            Assert.NotNull(result.ErrorMessage);
        }

        [Fact]
        public async Task DownloadVideosAsync_ReturnsDownloadResult()
        {
            // Arrange
            var sessionProvider = new Mock<ISessionProvider>();
            var logger = new Mock<ILogger>().Object;
            var dataClient = CreateMockDataClient();
            var service = new RingMediaDownloadService(logger, sessionProvider.Object, dataClient);

            // Act
            var result = await service.DownloadVideosAsync(
                "device123",
                "/tmp/videos",
                DateTime.Now.AddDays(-7),
                DateTime.Now
            );

            // Assert
            Assert.NotNull(result);
            Assert.IsType<DownloadResult>(result);
            // Should return a result even if session is null - graceful error handling
        }

        [Fact]
        public async Task DownloadSnapshotsAsync_WithoutSession_ReturnsFailureResult()
        {
            // Arrange
            var sessionProvider = new Mock<ISessionProvider>();
            sessionProvider.Setup(sp => sp.GetSession()).Returns((Session?)null);
            var logger = new Mock<ILogger>().Object;
            var dataClient = CreateMockDataClient();
            var service = new RingMediaDownloadService(logger, sessionProvider.Object, dataClient);

            // Act
            var result = await service.DownloadSnapshotsAsync(
                "device123",
                "/tmp/snapshots",
                DateTime.Now.AddDays(-7),
                DateTime.Now
            );

            // Assert
            Assert.NotNull(result);
            Assert.False(result.Success);
        }

        [Fact]
        public async Task DownloadSnapshotsAsync_ReturnsDownloadResult()
        {
            // Arrange
            var sessionProvider = new Mock<ISessionProvider>();
            var logger = new Mock<ILogger>().Object;
            var dataClient = CreateMockDataClient();
            var service = new RingMediaDownloadService(logger, sessionProvider.Object, dataClient);

            // Act
            var result = await service.DownloadSnapshotsAsync(
                "device123",
                "/tmp/snapshots",
                DateTime.Now.AddDays(-7),
                DateTime.Now
            );

            // Assert
            Assert.NotNull(result);
            Assert.IsType<DownloadResult>(result);
        }

        [Fact]
        public void GetStatus_ReturnsDownloadStatus()
        {
            // Arrange
            var sessionProvider = new Mock<ISessionProvider>();
            var logger = new Mock<ILogger>().Object;
            var dataClient = CreateMockDataClient();
            var service = new RingMediaDownloadService(logger, sessionProvider.Object, dataClient);

            // Act
            var status = service.GetStatus();

            // Assert
            Assert.NotNull(status);
            Assert.IsType<DownloadStatus>(status);
            Assert.False(status.IsDownloading);
        }

        [Fact]
        public void ConstructorThrowsOnNullSessionProvider()
        {
            // Arrange
            var logger = new Mock<ILogger>().Object;
            var dataClient = CreateMockDataClient();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new RingMediaDownloadService(logger, null!, dataClient));
        }

        [Fact]
        public async Task DownloadVideosAsync_WithValidDates_AcceptsDateRange()
        {
            // Arrange
            var sessionProvider = new Mock<ISessionProvider>();
            var logger = new Mock<ILogger>().Object;
            var dataClient = CreateMockDataClient();
            var service = new RingMediaDownloadService(logger, sessionProvider.Object, dataClient);
            var startDate = new DateTime(2024, 1, 1);
            var endDate = new DateTime(2024, 1, 31);

            // Act
            var result = await service.DownloadVideosAsync(
                "device123",
                "/tmp/videos",
                startDate,
                endDate
            );

            // Assert
            Assert.NotNull(result);
            // Should handle date range gracefully
        }
    }
}
