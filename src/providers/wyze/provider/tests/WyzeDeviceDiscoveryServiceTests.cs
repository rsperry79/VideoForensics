using Microsoft.Extensions.Logging;
using Moq;
using VideoForensics.Providers.Common.Contracts;
using VideoForensics.Providers.Wyze.Services;
using Xunit;

namespace VideoForensics.Providers.Wyze.Tests
{
    /// <summary>
    /// Tests for WyzeDeviceDiscoveryService stub implementation.
    /// Verifies that unimplemented methods return empty results.
    /// </summary>
    public class WyzeDeviceDiscoveryServiceTests
    {
        [Fact]
        public async Task GetLocationsAsync_Stub_ReturnsEmptyList()
        {
            // Arrange
            var logger = new Mock<ILogger>().Object;
            var service = new WyzeDeviceDiscoveryService(logger);

            // Act
            var result = await service.GetLocationsAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Fact]
        public async Task GetDevicesAsync_Stub_ReturnsEmptyList()
        {
            // Arrange
            var logger = new Mock<ILogger>().Object;
            var service = new WyzeDeviceDiscoveryService(logger);

            // Act
            var result = await service.GetDevicesAsync("location123");

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Fact]
        public async Task GetDeviceAsync_Stub_ReturnsNull()
        {
            // Arrange
            var logger = new Mock<ILogger>().Object;
            var service = new WyzeDeviceDiscoveryService(logger);

            // Act
            var result = await service.GetDeviceAsync("device123");

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void Constructor_WithLogger_CreatesService()
        {
            // Arrange
            var logger = new Mock<ILogger>().Object;

            // Act
            var service = new WyzeDeviceDiscoveryService(logger);

            // Assert
            Assert.NotNull(service);
        }

        [Fact]
        public async Task GetLocationsAsync_ReturnsReadOnlyList()
        {
            // Arrange
            var logger = new Mock<ILogger>().Object;
            var service = new WyzeDeviceDiscoveryService(logger);

            // Act
            var result = await service.GetLocationsAsync();

            // Assert
            Assert.NotNull(result);
            Assert.IsAssignableFrom<IReadOnlyList<Location>>(result);
        }

        [Fact]
        public async Task GetDevicesAsync_WithLocationId_ReturnsEmptyList()
        {
            // Arrange
            var logger = new Mock<ILogger>().Object;
            var service = new WyzeDeviceDiscoveryService(logger);

            // Act
            var result = await service.GetDevicesAsync("any-location-id");

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Fact]
        public async Task GetDeviceAsync_WithDeviceId_ReturnsNull()
        {
            // Arrange
            var logger = new Mock<ILogger>().Object;
            var service = new WyzeDeviceDiscoveryService(logger);

            // Act
            var result = await service.GetDeviceAsync("any-device-id");

            // Assert
            Assert.Null(result);
        }
    }
}
