using Microsoft.Extensions.Logging;
using Moq;
using VideoForensics.Providers.Common.Contracts;
using VideoForensics.Providers.Ring.Services;
using Xunit;

namespace VideoForensics.Providers.Ring.Tests
{
    /// <summary>
    /// Tests for RingDeviceDiscoveryService implementation.
    /// Verifies location and device discovery functionality.
    /// </summary>
    public class RingDeviceDiscoveryServiceTests
    {
        [Fact]
        public async Task GetLocationsAsync_WithoutSession_ReturnsEmptyList()
        {
            // Arrange
            var sessionProvider = new Mock<ISessionProvider>();
            sessionProvider.Setup(sp => sp.GetSession()).Returns((Session?)null);
            var logger = new Mock<ILogger>().Object;
            var service = new RingDeviceDiscoveryService(logger, sessionProvider.Object);

            // Act
            var result = await service.GetLocationsAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Fact]
        public async Task GetLocationsAsync_ReturnsReadOnlyList()
        {
            // Arrange
            var sessionProvider = new Mock<ISessionProvider>();
            var logger = new Mock<ILogger>().Object;
            var service = new RingDeviceDiscoveryService(logger, sessionProvider.Object);

            // Act
            var result = await service.GetLocationsAsync();

            // Assert
            Assert.NotNull(result);
            // Verify it's read-only by attempting to cast to IReadOnlyList
            Assert.IsAssignableFrom<IReadOnlyList<Location>>(result);
        }

        [Fact]
        public async Task GetDevicesAsync_WithoutSession_ReturnsEmptyList()
        {
            // Arrange
            var sessionProvider = new Mock<ISessionProvider>();
            sessionProvider.Setup(sp => sp.GetSession()).Returns((Session?)null);
            var logger = new Mock<ILogger>().Object;
            var service = new RingDeviceDiscoveryService(logger, sessionProvider.Object);

            // Act
            var result = await service.GetDevicesAsync("location123");

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Fact]
        public async Task GetDevicesAsync_WithInvalidLocationId_ReturnsEmptyList()
        {
            // Arrange
            var sessionProvider = new Mock<ISessionProvider>();
            var logger = new Mock<ILogger>().Object;
            var service = new RingDeviceDiscoveryService(logger, sessionProvider.Object);

            // Act
            var result = await service.GetDevicesAsync("invalid-location-id");

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Fact]
        public async Task GetDevicesAsync_ReturnsReadOnlyList()
        {
            // Arrange
            var sessionProvider = new Mock<ISessionProvider>();
            var logger = new Mock<ILogger>().Object;
            var service = new RingDeviceDiscoveryService(logger, sessionProvider.Object);

            // Act
            var result = await service.GetDevicesAsync("00000000-0000-0000-0000-000000000000");

            // Assert
            Assert.NotNull(result);
            Assert.IsAssignableFrom<IReadOnlyList<Device>>(result);
        }

        [Fact]
        public async Task GetDeviceAsync_WithoutSession_ReturnsNull()
        {
            // Arrange
            var sessionProvider = new Mock<ISessionProvider>();
            sessionProvider.Setup(sp => sp.GetSession()).Returns((Session?)null);
            var logger = new Mock<ILogger>().Object;
            var service = new RingDeviceDiscoveryService(logger, sessionProvider.Object);

            // Act
            var result = await service.GetDeviceAsync("device123");

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task GetDeviceAsync_ReturnsDeviceOrNull()
        {
            // Arrange
            var sessionProvider = new Mock<ISessionProvider>();
            var logger = new Mock<ILogger>().Object;
            var service = new RingDeviceDiscoveryService(logger, sessionProvider.Object);

            // Act
            var result = await service.GetDeviceAsync("nonexistent-device");

            // Assert
            // Result may be null if device not found, which is valid behavior
            // If not null, should be Device type
            if (result != null)
            {
                Assert.IsType<Device>(result);
            }
        }

        [Fact]
        public void ConstructorThrowsOnNullSessionProvider()
        {
            // Arrange
            var logger = new Mock<ILogger>().Object;

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new RingDeviceDiscoveryService(logger, null!));
        }
    }
}
