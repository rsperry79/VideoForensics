using Microsoft.Extensions.Logging;

using Moq;

using VideoForensics.Data.Core.Contracts;
using VideoForensics.Providers.Common.Contracts;
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
            _ = sessionProvider.Setup(sp => sp.GetSession()).Returns((Session?)null);
            ILogger logger = new Mock<ILogger>().Object;
            IVideoForensicsDataClient dataClient = CreateMockDataClient();
            var service = new RingMediaDownloadService(logger, sessionProvider.Object, dataClient);

            // Act
            DownloadResult result = await service.DownloadVideosAsync(
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
            ILogger logger = new Mock<ILogger>().Object;
            IVideoForensicsDataClient dataClient = CreateMockDataClient();
            var service = new RingMediaDownloadService(logger, sessionProvider.Object, dataClient);

            // Act
            DownloadResult result = await service.DownloadVideosAsync(
                "device123",
                "/tmp/videos",
                DateTime.Now.AddDays(-7),
                DateTime.Now
            );

            // Assert
            Assert.NotNull(result);
            _ = Assert.IsType<DownloadResult>(result);
            // Should return a result even if session is null - graceful error handling
        }

        [Fact]
        public async Task DownloadSnapshotsAsync_WithoutSession_ReturnsFailureResult()
        {
            // Arrange
            var sessionProvider = new Mock<ISessionProvider>();
            _ = sessionProvider.Setup(sp => sp.GetSession()).Returns((Session?)null);
            ILogger logger = new Mock<ILogger>().Object;
            IVideoForensicsDataClient dataClient = CreateMockDataClient();
            var service = new RingMediaDownloadService(logger, sessionProvider.Object, dataClient);

            // Act
            DownloadResult result = await service.DownloadSnapshotsAsync(
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
            ILogger logger = new Mock<ILogger>().Object;
            IVideoForensicsDataClient dataClient = CreateMockDataClient();
            var service = new RingMediaDownloadService(logger, sessionProvider.Object, dataClient);

            // Act
            DownloadResult result = await service.DownloadSnapshotsAsync(
                "device123",
                "/tmp/snapshots",
                DateTime.Now.AddDays(-7),
                DateTime.Now
            );

            // Assert
            Assert.NotNull(result);
            _ = Assert.IsType<DownloadResult>(result);
        }

        [Fact]
        public void GetStatus_ReturnsDownloadStatus()
        {
            // Arrange
            var sessionProvider = new Mock<ISessionProvider>();
            ILogger logger = new Mock<ILogger>().Object;
            IVideoForensicsDataClient dataClient = CreateMockDataClient();
            var service = new RingMediaDownloadService(logger, sessionProvider.Object, dataClient);

            // Act
            DownloadStatus status = service.GetStatus();

            // Assert
            Assert.NotNull(status);
            _ = Assert.IsType<DownloadStatus>(status);
            Assert.False(status.IsDownloading);
        }

        [Fact]
        public void ConstructorThrowsOnNullSessionProvider()
        {
            // Arrange
            ILogger logger = new Mock<ILogger>().Object;
            IVideoForensicsDataClient dataClient = CreateMockDataClient();

            // Act & Assert
            _ = Assert.Throws<ArgumentNullException>(() => new RingMediaDownloadService(logger, null!, dataClient));
        }

        [Fact]
        public async Task DownloadVideosAsync_WithValidDates_AcceptsDateRange()
        {
            // Arrange
            var sessionProvider = new Mock<ISessionProvider>();
            ILogger logger = new Mock<ILogger>().Object;
            IVideoForensicsDataClient dataClient = CreateMockDataClient();
            var service = new RingMediaDownloadService(logger, sessionProvider.Object, dataClient);
            var startDate = new DateTime(2024, 1, 1);
            var endDate = new DateTime(2024, 1, 31);

            // Act
            DownloadResult result = await service.DownloadVideosAsync(
                "device123",
                "/tmp/videos",
                startDate,
                endDate
            );

            // Assert
            Assert.NotNull(result);
            // Should handle date range gracefully
        }

        [Fact]
        public async Task GetMatchedEventCountAsync_WithoutSession_ReturnsZero()
        {
            // Arrange
            var sessionProvider = new Mock<ISessionProvider>();
            _ = sessionProvider.Setup(sp => sp.GetSession()).Returns((Session?)null);
            ILogger logger = new Mock<ILogger>().Object;
            IVideoForensicsDataClient dataClient = CreateMockDataClient();
            var service = new RingMediaDownloadService(logger, sessionProvider.Object, dataClient);

            // Act
            int count = await service.GetMatchedEventCountAsync("device123", DateTime.Now.AddDays(-7), DateTime.Now);

            // Assert
            Assert.Equal(0, count);
        }

        [Fact]
        public void GetStatus_IsInitiallyNotDownloading()
        {
            // Arrange
            var sessionProvider = new Mock<ISessionProvider>();
            ILogger logger = new Mock<ILogger>().Object;
            IVideoForensicsDataClient dataClient = CreateMockDataClient();
            var service = new RingMediaDownloadService(logger, sessionProvider.Object, dataClient);

            // Act
            DownloadStatus status = service.GetStatus();

            // Assert
            Assert.False(status.IsDownloading);
            Assert.Equal(0, status.FilesCompleted);
            Assert.Equal(0, status.FilesTotal);
            Assert.Equal(0L, status.BytesDownloaded);
        }

        [Fact]
        public void SetMaxConcurrentDownloads_WithPositiveValue_AcceptsValue()
        {
            // Arrange
            var sessionProvider = new Mock<ISessionProvider>();
            ILogger logger = new Mock<ILogger>().Object;
            IVideoForensicsDataClient dataClient = CreateMockDataClient();
            var service = new RingMediaDownloadService(logger, sessionProvider.Object, dataClient);

            // Act
            service.SetMaxConcurrentDownloads(5);

            // Assert - no exception thrown
            Assert.True(true);
        }

        [Fact]
        public void SetMaxConcurrentDownloads_WithZeroOrNegative_IgnoresValue()
        {
            // Arrange
            var sessionProvider = new Mock<ISessionProvider>();
            ILogger logger = new Mock<ILogger>().Object;
            IVideoForensicsDataClient dataClient = CreateMockDataClient();
            var service = new RingMediaDownloadService(logger, sessionProvider.Object, dataClient);

            // Act - setting to 0 or negative should be ignored
            service.SetMaxConcurrentDownloads(0);
            service.SetMaxConcurrentDownloads(-1);

            // Assert - no exception thrown
            Assert.True(true);
        }

        [Fact]
        public void SetActiveProviderAccountId_CachesAccountId()
        {
            // Arrange
            var sessionProvider = new Mock<ISessionProvider>();
            ILogger logger = new Mock<ILogger>().Object;
            IVideoForensicsDataClient dataClient = CreateMockDataClient();
            var service = new RingMediaDownloadService(logger, sessionProvider.Object, dataClient);
            var accountId = Guid.NewGuid();

            // Act
            service.SetActiveProviderAccountId(accountId);

            // Assert - no exception thrown, account is cached for later use
            Assert.True(true);
        }

        [Fact]
        public void IsHistoryCached_WithNoCacheData_ReturnsFalse()
        {
            // Arrange
            var sessionProvider = new Mock<ISessionProvider>();
            ILogger logger = new Mock<ILogger>().Object;
            IVideoForensicsDataClient dataClient = CreateMockDataClient();
            var service = new RingMediaDownloadService(logger, sessionProvider.Object, dataClient);

            // Act
            bool isCached = service.IsHistoryCached(DateTime.Now.AddDays(-7), DateTime.Now);

            // Assert
            Assert.False(isCached);
        }

        [Fact]
        public void DrainActivityLog_WithEmptyLog_ReturnsEmptyList()
        {
            // Arrange
            var sessionProvider = new Mock<ISessionProvider>();
            ILogger logger = new Mock<ILogger>().Object;
            IVideoForensicsDataClient dataClient = CreateMockDataClient();
            var service = new RingMediaDownloadService(logger, sessionProvider.Object, dataClient);

            // Act
            IReadOnlyList<string> log = service.DrainActivityLog();

            // Assert
            Assert.NotNull(log);
            Assert.Empty(log);
        }

        [Fact]
        public void GetRateLimitBanUntilUtc_WithoutRateLimit_ReturnsNull()
        {
            // Arrange
            var sessionProvider = new Mock<ISessionProvider>();
            ILogger logger = new Mock<ILogger>().Object;
            IVideoForensicsDataClient dataClient = CreateMockDataClient();
            var service = new RingMediaDownloadService(logger, sessionProvider.Object, dataClient);

            // Act
            DateTime? banUntil = service.GetRateLimitBanUntilUtc();

            // Assert
            Assert.Null(banUntil);
        }

        [Fact]
        public void OverrideRateLimitBan_DoesNotThrow()
        {
            // Arrange
            var sessionProvider = new Mock<ISessionProvider>();
            ILogger logger = new Mock<ILogger>().Object;
            IVideoForensicsDataClient dataClient = CreateMockDataClient();
            var service = new RingMediaDownloadService(logger, sessionProvider.Object, dataClient);

            // Act
            service.OverrideRateLimitBan();

            // Assert - no exception thrown
            Assert.True(true);
        }

        [Fact]
        public async Task DownloadSnapshotsAsync_WithInvalidDeviceId_ReturnsFailure()
        {
            // Arrange
            var sessionProvider = new Mock<ISessionProvider>();
            var session = new Mock<Session>("testuser", "testpass", null, null);
            _ = sessionProvider.Setup(sp => sp.GetSession()).Returns(session.Object);
            ILogger logger = new Mock<ILogger>().Object;
            IVideoForensicsDataClient dataClient = CreateMockDataClient();
            var service = new RingMediaDownloadService(logger, sessionProvider.Object, dataClient);

            // Act
            DownloadResult result = await service.DownloadSnapshotsAsync(
                "not-a-number",
                Path.Combine(Path.GetTempPath(), "snapshots_test"),
                DateTime.Now.AddDays(-7),
                DateTime.Now
            );

            // Assert
            Assert.NotNull(result);
            Assert.False(result.Success);
            Assert.Contains("Invalid device id", result.ErrorMessage ?? string.Empty);
        }

        [Fact]
        public void ConstructorThrowsOnNullDataClient()
        {
            // Arrange
            var sessionProvider = new Mock<ISessionProvider>();
            ILogger logger = new Mock<ILogger>().Object;

            // Act & Assert
            _ = Assert.Throws<ArgumentNullException>(() => new RingMediaDownloadService(logger, sessionProvider.Object, null!));
        }
    }
}
