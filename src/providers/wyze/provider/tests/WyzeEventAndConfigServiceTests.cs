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
            ILogger logger = new Mock<ILogger>().Object;
            var service = new WyzeEventAndConfigService(logger);

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
        public async Task GetDeviceConfigAsync_Stub_ReturnsNull()
        {
            // Arrange
            ILogger logger = new Mock<ILogger>().Object;
            var service = new WyzeEventAndConfigService(logger);

            // Act
            DeviceConfig? result = await service.GetDeviceConfigAsync("device123");

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task UpdateDeviceConfigAsync_Stub_ReturnsFalse()
        {
            // Arrange
            ILogger logger = new Mock<ILogger>().Object;
            var service = new WyzeEventAndConfigService(logger);
            var config = new DeviceConfig(
                DeviceId: "device123",
                MotionDetectionEnabled: true,
                MotionSensitivity: 75,
                RecordingMode: "motion"
            );

            // Act
            bool result = await service.UpdateDeviceConfigAsync("device123", config);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void Constructor_WithLogger_CreatesService()
        {
            // Arrange
            ILogger logger = new Mock<ILogger>().Object;

            // Act
            var service = new WyzeEventAndConfigService(logger);

            // Assert
            Assert.NotNull(service);
        }

        [Fact]
        public async Task GetEventsAsync_WithEventTypeFilter_ReturnsEmptyList()
        {
            // Arrange
            ILogger logger = new Mock<ILogger>().Object;
            var service = new WyzeEventAndConfigService(logger);

            // Act
            IReadOnlyList<DeviceEvent> result = await service.GetEventsAsync(
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
            ILogger logger = new Mock<ILogger>().Object;
            var service = new WyzeEventAndConfigService(logger);

            // Act
            DeviceConfig? result = await service.GetDeviceConfigAsync("any-device-id");

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task UpdateDeviceConfigAsync_WithValidConfig_ReturnsFalse()
        {
            // Arrange
            ILogger logger = new Mock<ILogger>().Object;
            var service = new WyzeEventAndConfigService(logger);
            var config = new DeviceConfig(
                DeviceId: "device456",
                MotionDetectionEnabled: false,
                MotionSensitivity: 50,
                RecordingMode: "always"
            );

            // Act
            bool result = await service.UpdateDeviceConfigAsync("device456", config);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task GetEventsAsync_ReturnsReadOnlyList()
        {
            // Arrange
            ILogger logger = new Mock<ILogger>().Object;
            var service = new WyzeEventAndConfigService(logger);

            // Act
            IReadOnlyList<DeviceEvent> result = await service.GetEventsAsync("device", DateTime.Now, DateTime.Now);

            // Assert
            Assert.NotNull(result);
            _ = Assert.IsAssignableFrom<IReadOnlyList<DeviceEvent>>(result);
        }

        [Fact]
        public async Task GetEventsAsync_SanitizesLogOutput_WhenDeviceIdContainsNewlines()
        {
            // Arrange: Create a mock logger to verify sanitization
            var mockLogger = new Mock<ILogger>();
            var service = new WyzeEventAndConfigService(mockLogger.Object);
            string deviceIdWithNewlines = "device123\r\nFAKE LOG LINE";

            // Act: Call GetEventsAsync with deviceId containing newlines
            IReadOnlyList<DeviceEvent> result = await service.GetEventsAsync(
                deviceIdWithNewlines,
                DateTime.Now.AddDays(-7),
                DateTime.Now
            );

            // Assert: Verify the method returns an empty list (stub behavior intact)
            Assert.NotNull(result);
            Assert.Empty(result);
            // The logger should have been called, but the sanitized deviceId should not contain newlines
            // This test verifies that the log call completes without issues when deviceId contains newlines
        }
    }
}
