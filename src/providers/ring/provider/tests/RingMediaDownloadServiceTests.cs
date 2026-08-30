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

        // FindDeviceHealth backs CaptureDeviceHealthSnapshotAsync's device matching. It's the only
        // part of that capture logic testable without a live Ring session - Session makes real
        // HTTP calls and has no interface to mock, so the tests above can only exercise the
        // "no session" short-circuit that skips health capture entirely.
        [Fact]
        public void FindDeviceHealth_MatchesDoorbotById_ReturnsHealth()
        {
            var health = new DeviceHealth { BatteryPercentage = 42, Connected = true };
            var devices = new Devices
            {
                Doorbots = new List<Doorbot> { new() { Id = 111, Health = health } }
            };

            var result = RingMediaDownloadService.FindDeviceHealth(devices, "111");

            Assert.Same(health, result);
        }

        [Fact]
        public void FindDeviceHealth_MatchesStickupCamById_ReturnsHealth()
        {
            var health = new DeviceHealth { BatteryPercentage = 77 };
            var devices = new Devices
            {
                StickupCams = new List<StickupCam> { new() { Id = 222, Health = health } }
            };

            var result = RingMediaDownloadService.FindDeviceHealth(devices, "222");

            Assert.Same(health, result);
        }

        [Fact]
        public void FindDeviceHealth_MatchesAuthorizedDoorbotById_ReturnsHealth()
        {
            var health = new DeviceHealth { Connected = false };
            var devices = new Devices
            {
                AuthorizedDoorbots = new List<Doorbot> { new() { Id = 333, Health = health } }
            };

            var result = RingMediaDownloadService.FindDeviceHealth(devices, "333");

            Assert.Same(health, result);
        }

        [Fact]
        public void FindDeviceHealth_NoMatchingDevice_ReturnsNull()
        {
            var devices = new Devices
            {
                Doorbots = new List<Doorbot> { new() { Id = 111, Health = new DeviceHealth() } }
            };

            var result = RingMediaDownloadService.FindDeviceHealth(devices, "does-not-exist");

            Assert.Null(result);
        }

        [Fact]
        public void FindDeviceHealth_NullDevices_ReturnsNull()
        {
            var result = RingMediaDownloadService.FindDeviceHealth(null, "111");

            Assert.Null(result);
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
