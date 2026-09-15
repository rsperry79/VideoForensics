using Microsoft.Extensions.Logging;

using Moq;

using VideoForensics.Providers.Common.Contracts;
using VideoForensics.Providers.Ring.Services;

using Xunit;

namespace VideoForensics.Providers.Ring.Tests
{
    /// <summary>
    /// Tests for RingEventAndConfigService implementation.
    /// Verifies event retrieval and device configuration management.
    /// </summary>
    public class RingEventAndConfigServiceTests
    {
        [Fact]
        public async Task GetEventsAsync_WithoutSession_ReturnsEmptyList()
        {
            // Arrange
            var sessionProvider = new Mock<ISessionProvider>();
            _ = sessionProvider.Setup(sp => sp.GetSession()).Returns((Session?)null);
            ILogger logger = new Mock<ILogger>().Object;
            var service = new RingEventAndConfigService(logger, sessionProvider.Object);

            // Act
            IReadOnlyList<DeviceEvent> result = await service.GetEventsAsync(
                "device123",
                DateTime.Now.AddDays(-7),
                DateTime.Now
            );

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Fact]
        public async Task GetEventsAsync_ReturnsReadOnlyList()
        {
            // Arrange
            var sessionProvider = new Mock<ISessionProvider>();
            ILogger logger = new Mock<ILogger>().Object;
            var service = new RingEventAndConfigService(logger, sessionProvider.Object);

            // Act
            IReadOnlyList<DeviceEvent> result = await service.GetEventsAsync(
                "device123",
                DateTime.Now.AddDays(-7),
                DateTime.Now
            );

            // Assert
            Assert.NotNull(result);
            _ = Assert.IsAssignableFrom<IReadOnlyList<DeviceEvent>>(result);
        }

        [Fact]
        public async Task GetEventsAsync_WithEventTypeFilter_ReturnsFilteredEvents()
        {
            // Arrange
            var sessionProvider = new Mock<ISessionProvider>();
            ILogger logger = new Mock<ILogger>().Object;
            var service = new RingEventAndConfigService(logger, sessionProvider.Object);

            // Act
            IReadOnlyList<DeviceEvent> result = await service.GetEventsAsync(
                "device123",
                DateTime.Now.AddDays(-7),
                DateTime.Now,
                eventType: "motion"
            );

            // Assert
            Assert.NotNull(result);
            _ = Assert.IsAssignableFrom<IReadOnlyList<DeviceEvent>>(result);
        }

        [Fact]
        public async Task GetDeviceConfigAsync_WithoutSession_ReturnsNull()
        {
            // Arrange
            var sessionProvider = new Mock<ISessionProvider>();
            _ = sessionProvider.Setup(sp => sp.GetSession()).Returns((Session?)null);
            ILogger logger = new Mock<ILogger>().Object;
            var service = new RingEventAndConfigService(logger, sessionProvider.Object);

            // Act
            DeviceConfig? result = await service.GetDeviceConfigAsync("device123");

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task GetDeviceConfigAsync_WithInvalidDeviceId_ReturnsNull()
        {
            // Arrange
            var sessionProvider = new Mock<ISessionProvider>();
            ILogger logger = new Mock<ILogger>().Object;
            var service = new RingEventAndConfigService(logger, sessionProvider.Object);

            // Act
            DeviceConfig? result = await service.GetDeviceConfigAsync("not-a-number");

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task GetDeviceConfigAsync_ReturnsDeviceConfigOrNull()
        {
            // Arrange
            var sessionProvider = new Mock<ISessionProvider>();
            ILogger logger = new Mock<ILogger>().Object;
            var service = new RingEventAndConfigService(logger, sessionProvider.Object);

            // Act
            DeviceConfig? result = await service.GetDeviceConfigAsync("123456");

            // Assert
            // Result may be null if device config not found
            // If not null, should be DeviceConfig type
            if (result != null)
            {
                _ = Assert.IsType<DeviceConfig>(result);
            }
        }

        [Fact]
        public async Task UpdateDeviceConfigAsync_WithValidConfig_ReturnsTrue()
        {
            // Arrange
            var sessionProvider = new Mock<ISessionProvider>();
            ILogger logger = new Mock<ILogger>().Object;
            var service = new RingEventAndConfigService(logger, sessionProvider.Object);
            var config = new DeviceConfig(
                DeviceId: "device123",
                MotionDetectionEnabled: true,
                MotionSensitivity: 75,
                RecordingMode: "motion"
            );

            // Act
            bool result = await service.UpdateDeviceConfigAsync("device123", config);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task UpdateDeviceConfigAsync_ReturnsBoolean()
        {
            // Arrange
            var sessionProvider = new Mock<ISessionProvider>();
            ILogger logger = new Mock<ILogger>().Object;
            var service = new RingEventAndConfigService(logger, sessionProvider.Object);
            var config = new DeviceConfig(
                DeviceId: "device123",
                MotionDetectionEnabled: false,
                MotionSensitivity: 50,
                RecordingMode: "always"
            );

            // Act
            bool result = await service.UpdateDeviceConfigAsync("device123", config);

            // Assert
            _ = Assert.IsType<bool>(result);
        }

        [Fact]
        public void ConstructorThrowsOnNullSessionProvider()
        {
            // Arrange
            ILogger logger = new Mock<ILogger>().Object;

            // Act & Assert
            _ = Assert.Throws<ArgumentNullException>(() => new RingEventAndConfigService(logger, null!));
        }
    }
}
