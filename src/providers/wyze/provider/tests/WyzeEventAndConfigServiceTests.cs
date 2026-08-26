using Microsoft.Extensions.Logging;
using Moq;
using VideoForensics.Providers.Common.Contracts;
using VideoForensics.Providers.Wyze.Services;
using Xunit;

namespace VideoForensics.Providers.Wyze.Tests
{
    /// <summary>
    /// Tests for WyzeEventAndConfigService stub implementation.
    /// Verifies that unimplemented methods return empty or null results.
    /// </summary>
    public class WyzeEventAndConfigServiceTests
    {
        [Fact]
        public async Task GetEventsAsync_Stub_ReturnsEmptyList()
        {
            // Arrange
            var logger = new Mock<ILogger>().Object;
            var service = new WyzeEventAndConfigService(logger);

            // Act
            var result = await service.GetEventsAsync(
                "device123",
                DateTime.Now.AddDays(-7),
                DateTime.Now
            );

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Fact]
        public async Task GetDeviceConfigAsync_Stub_ReturnsNull()
        {
            // Arrange
            var logger = new Mock<ILogger>().Object;
            var service = new WyzeEventAndConfigService(logger);

            // Act
            var result = await service.GetDeviceConfigAsync("device123");

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task UpdateDeviceConfigAsync_Stub_ReturnsFalse()
        {
            // Arrange
            var logger = new Mock<ILogger>().Object;
            var service = new WyzeEventAndConfigService(logger);
            var config = new DeviceConfig(
                DeviceId: "device123",
                MotionDetectionEnabled: true,
                MotionSensitivity: 75,
                RecordingMode: "motion"
            );

            // Act
            var result = await service.UpdateDeviceConfigAsync("device123", config);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void Constructor_WithLogger_CreatesService()
        {
            // Arrange
            var logger = new Mock<ILogger>().Object;

            // Act
            var service = new WyzeEventAndConfigService(logger);

            // Assert
            Assert.NotNull(service);
        }

        [Fact]
        public async Task GetEventsAsync_WithEventTypeFilter_ReturnsEmptyList()
        {
            // Arrange
            var logger = new Mock<ILogger>().Object;
            var service = new WyzeEventAndConfigService(logger);

            // Act
            var result = await service.GetEventsAsync(
                "device123",
                DateTime.Now.AddDays(-7),
                DateTime.Now,
                eventType: "motion"
            );

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Fact]
        public async Task GetDeviceConfigAsync_WithDeviceId_ReturnsNull()
        {
            // Arrange
            var logger = new Mock<ILogger>().Object;
            var service = new WyzeEventAndConfigService(logger);

            // Act
            var result = await service.GetDeviceConfigAsync("any-device-id");

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task UpdateDeviceConfigAsync_WithValidConfig_ReturnsFalse()
        {
            // Arrange
            var logger = new Mock<ILogger>().Object;
            var service = new WyzeEventAndConfigService(logger);
            var config = new DeviceConfig(
                DeviceId: "device456",
                MotionDetectionEnabled: false,
                MotionSensitivity: 50,
                RecordingMode: "always"
            );

            // Act
            var result = await service.UpdateDeviceConfigAsync("device456", config);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task GetEventsAsync_ReturnsReadOnlyList()
        {
            // Arrange
            var logger = new Mock<ILogger>().Object;
            var service = new WyzeEventAndConfigService(logger);

            // Act
            var result = await service.GetEventsAsync("device", DateTime.Now, DateTime.Now);

            // Assert
            Assert.NotNull(result);
            Assert.IsAssignableFrom<IReadOnlyList<DeviceEvent>>(result);
        }
    }
}
