using Microsoft.Extensions.Logging;
using Moq;
using VideoForensics.Providers.Common.Contracts;
using VideoForensics.Providers.Uniview.Services;
using Xunit;

namespace VideoForensics.Providers.Uniview.Tests
{
    /// <summary>
    /// Tests for UniviewEventAndConfigService. Tests the "not authenticated" and unsupported event type paths.
    /// Live device calls and HTTP communication are not tested here (would require a live device).
    /// NOTE: matches Ring's convention (log + return empty/null/false) rather than throwing when not
    /// authenticated - see RingDeviceDiscoveryService/RingEventAndConfigService for the pattern this follows.
    /// </summary>
    public class UniviewEventAndConfigServiceTests
    {
        [Fact]
        public async Task UniviewEventAndConfigService_GetEventsAsync_NotAuthenticated_ReturnsEmptyList()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<UniviewEventAndConfigService>>();
            var mockSessionProvider = new Mock<IUniviewSessionProvider>();

            mockSessionProvider.Setup(s => s.GetClient()).Returns((UniviewClient?)null);

            var service = new UniviewEventAndConfigService(
                mockLogger.Object,
                mockSessionProvider.Object);

            // Act
            var events = await service.GetEventsAsync("1", DateTime.Now.AddDays(-7), DateTime.Now, cancellationToken: CancellationToken.None);

            // Assert
            Assert.Empty(events);
        }

        [Fact]
        public async Task UniviewEventAndConfigService_GetEventsAsync_UnsupportedEventType_ReturnsEmptyList()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<UniviewEventAndConfigService>>();
            var mockSessionProvider = new Mock<IUniviewSessionProvider>();

            var client = new UniviewClient("192.168.1.1", "admin", "password");
            mockSessionProvider.Setup(s => s.GetClient()).Returns(client);

            var service = new UniviewEventAndConfigService(
                mockLogger.Object,
                mockSessionProvider.Object);

            try
            {
                // Act - request events with unsupported type (only "motion" is supported)
                var events = await service.GetEventsAsync(
                    "1",
                    DateTime.Now.AddDays(-7),
                    DateTime.Now,
                    eventType: "alarm",
                    cancellationToken: CancellationToken.None);

                // Assert - should return empty, not call client
                Assert.Empty(events);
            }
            finally
            {
                client.Dispose();
            }
        }

        [Fact]
        public async Task UniviewEventAndConfigService_GetEventsAsync_InvalidDeviceId_Throws()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<UniviewEventAndConfigService>>();
            var mockSessionProvider = new Mock<IUniviewSessionProvider>();

            var client = new UniviewClient("192.168.1.1", "admin", "password");
            mockSessionProvider.Setup(s => s.GetClient()).Returns(client);

            var service = new UniviewEventAndConfigService(
                mockLogger.Object,
                mockSessionProvider.Object);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(async () =>
                await service.GetEventsAsync(
                    "invalid",
                    DateTime.Now.AddDays(-7),
                    DateTime.Now,
                    cancellationToken: CancellationToken.None));
        }

        [Fact]
        public async Task UniviewEventAndConfigService_GetDeviceConfigAsync_NotAuthenticated_ReturnsNull()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<UniviewEventAndConfigService>>();
            var mockSessionProvider = new Mock<IUniviewSessionProvider>();

            mockSessionProvider.Setup(s => s.GetClient()).Returns((UniviewClient?)null);

            var service = new UniviewEventAndConfigService(
                mockLogger.Object,
                mockSessionProvider.Object);

            // Act
            var config = await service.GetDeviceConfigAsync("1", CancellationToken.None);

            // Assert
            Assert.Null(config);
        }

        [Fact]
        public async Task UniviewEventAndConfigService_GetDeviceConfigAsync_InvalidDeviceId_Throws()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<UniviewEventAndConfigService>>();
            var mockSessionProvider = new Mock<IUniviewSessionProvider>();

            var client = new UniviewClient("192.168.1.1", "admin", "password");
            mockSessionProvider.Setup(s => s.GetClient()).Returns(client);

            var service = new UniviewEventAndConfigService(
                mockLogger.Object,
                mockSessionProvider.Object);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(async () =>
                await service.GetDeviceConfigAsync("invalid", CancellationToken.None));
        }

        [Fact]
        public async Task UniviewEventAndConfigService_UpdateDeviceConfigAsync_NotAuthenticated_ReturnsFalse()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<UniviewEventAndConfigService>>();
            var mockSessionProvider = new Mock<IUniviewSessionProvider>();

            mockSessionProvider.Setup(s => s.GetClient()).Returns((UniviewClient?)null);

            var service = new UniviewEventAndConfigService(
                mockLogger.Object,
                mockSessionProvider.Object);

            var config = new DeviceConfig(
                DeviceId: "1",
                MotionDetectionEnabled: false,
                MotionSensitivity: 0,
                RecordingMode: "off");

            // Act
            var result = await service.UpdateDeviceConfigAsync("1", config, CancellationToken.None);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task UniviewEventAndConfigService_UpdateDeviceConfigAsync_InvalidDeviceId_Throws()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<UniviewEventAndConfigService>>();
            var mockSessionProvider = new Mock<IUniviewSessionProvider>();

            var client = new UniviewClient("192.168.1.1", "admin", "password");
            mockSessionProvider.Setup(s => s.GetClient()).Returns(client);

            var service = new UniviewEventAndConfigService(
                mockLogger.Object,
                mockSessionProvider.Object);

            var config = new DeviceConfig(
                DeviceId: "invalid",
                MotionDetectionEnabled: false,
                MotionSensitivity: 0,
                RecordingMode: "off");

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(async () =>
                await service.UpdateDeviceConfigAsync("invalid", config, CancellationToken.None));
        }

        [Fact]
        public void UniviewEventAndConfigService_Constructor_AllowsNullLogger()
        {
            // Arrange
            var mockSessionProvider = new Mock<IUniviewSessionProvider>();

            // Act - Note: UniviewEventAndConfigService does NOT validate logger parameter
            var service = new UniviewEventAndConfigService(
                null!,
                mockSessionProvider.Object);

            // Assert
            Assert.NotNull(service);
        }

        [Fact]
        public void UniviewEventAndConfigService_Constructor_AllowsNullSessionProvider()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<UniviewEventAndConfigService>>();

            // Act - Note: UniviewEventAndConfigService does NOT validate sessionProvider parameter
            var service = new UniviewEventAndConfigService(
                mockLogger.Object,
                null!);

            // Assert
            Assert.NotNull(service);
        }

        [Fact]
        public async Task UniviewEventAndConfigService_GetEventsAsync_MotionEventType_CaseInsensitive_AcceptsMotion()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<UniviewEventAndConfigService>>();
            var mockSessionProvider = new Mock<IUniviewSessionProvider>();

            var client = new UniviewClient("192.168.1.1", "admin", "password");
            mockSessionProvider.Setup(s => s.GetClient()).Returns(client);

            var service = new UniviewEventAndConfigService(
                mockLogger.Object,
                mockSessionProvider.Object);

            try
            {
                // Act - use "Motion" with capital M - should be accepted due to OrdinalIgnoreCase
                // We can't test the full flow (ListSegmentsAsync requires login), but we can test
                // that the code doesn't reject it early due to case sensitivity
                // Actually, this will throw InvalidOperationException since client isn't logged in,
                // which means it got past the case-insensitive check. That's what we're testing.
                await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                    await service.GetEventsAsync(
                        "1",
                        DateTime.Now.AddDays(-7),
                        DateTime.Now,
                        eventType: "Motion",  // Capital M - should be accepted (case-insensitive)
                        cancellationToken: CancellationToken.None));

                // If we got here, the case-insensitive check passed (otherwise it would have returned empty list)
            }
            finally
            {
                client.Dispose();
            }
        }
    }
}
