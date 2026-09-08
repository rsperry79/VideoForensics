using Microsoft.Extensions.Logging;
using Moq;
using VideoForensics.Client.Common.Contracts;
using VideoForensics.Providers.Common.Contracts;
using VideoForensics.Providers.Uniview.Services;
using Xunit;

namespace VideoForensics.Providers.Uniview.Tests
{
    /// <summary>
    /// Tests for UniviewDeviceDiscoveryService. Tests the "not authenticated" paths and discovery behavior.
    /// Live UniviewClient calls are not tested here (would require a live device).
    /// NOTE: Unlike other services, this one returns empty lists instead of throwing when not authenticated.
    /// </summary>
    public class UniviewDeviceDiscoveryServiceTests
    {
        [Fact]
        public async Task UniviewDeviceDiscoveryService_GetLocationsAsync_NotAuthenticated_ReturnsEmptyList()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<UniviewDeviceDiscoveryService>>();
            var mockSessionProvider = new Mock<IUniviewSessionProvider>();
            var mockConfig = new Mock<IForensicsConfiguration>();

            mockSessionProvider.Setup(s => s.GetClient()).Returns((UniviewClient?)null);

            var service = new UniviewDeviceDiscoveryService(
                mockLogger.Object,
                mockSessionProvider.Object,
                mockConfig.Object);

            // Act
            var locations = await service.GetLocationsAsync(CancellationToken.None);

            // Assert - IMPORTANT: returns empty list, not exception
            Assert.Empty(locations);
        }

        [Fact]
        public async Task UniviewDeviceDiscoveryService_GetDevicesAsync_NotAuthenticated_ReturnsEmptyList()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<UniviewDeviceDiscoveryService>>();
            var mockSessionProvider = new Mock<IUniviewSessionProvider>();
            var mockConfig = new Mock<IForensicsConfiguration>();

            mockSessionProvider.Setup(s => s.GetClient()).Returns((UniviewClient?)null);

            var service = new UniviewDeviceDiscoveryService(
                mockLogger.Object,
                mockSessionProvider.Object,
                mockConfig.Object);

            // Act
            var devices = await service.GetDevicesAsync("192.168.1.1", CancellationToken.None);

            // Assert - IMPORTANT: returns empty list, not exception
            Assert.Empty(devices);
        }

        [Fact]
        public async Task UniviewDeviceDiscoveryService_GetDeviceAsync_NotAuthenticated_ReturnsNull()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<UniviewDeviceDiscoveryService>>();
            var mockSessionProvider = new Mock<IUniviewSessionProvider>();
            var mockConfig = new Mock<IForensicsConfiguration>();

            mockSessionProvider.Setup(s => s.GetClient()).Returns((UniviewClient?)null);

            var service = new UniviewDeviceDiscoveryService(
                mockLogger.Object,
                mockSessionProvider.Object,
                mockConfig.Object);

            // Act
            var device = await service.GetDeviceAsync("1", CancellationToken.None);

            // Assert
            Assert.Null(device);
        }

        [Fact]
        public async Task UniviewDeviceDiscoveryService_GetLocationsAsync_NoHostConfigured_ReturnsEmptyList()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<UniviewDeviceDiscoveryService>>();
            var mockSessionProvider = new Mock<IUniviewSessionProvider>();
            var mockConfig = new Mock<IForensicsConfiguration>();

            var client = new UniviewClient("192.168.1.1", "admin", "password");
            mockSessionProvider.Setup(s => s.GetClient()).Returns(client);
            mockConfig.Setup(c => c.UniviewNvrHost).Returns((string?)null);

            var service = new UniviewDeviceDiscoveryService(
                mockLogger.Object,
                mockSessionProvider.Object,
                mockConfig.Object);

            try
            {
                // Act
                var locations = await service.GetLocationsAsync(CancellationToken.None);

                // Assert
                Assert.Empty(locations);
            }
            finally
            {
                client.Dispose();
            }
        }

        [Fact]
        public async Task UniviewDeviceDiscoveryService_GetDevicesAsync_NoHostConfigured_ReturnsEmptyList()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<UniviewDeviceDiscoveryService>>();
            var mockSessionProvider = new Mock<IUniviewSessionProvider>();
            var mockConfig = new Mock<IForensicsConfiguration>();

            var client = new UniviewClient("192.168.1.1", "admin", "password");
            mockSessionProvider.Setup(s => s.GetClient()).Returns(client);
            mockConfig.Setup(c => c.UniviewNvrHost).Returns((string?)null);

            var service = new UniviewDeviceDiscoveryService(
                mockLogger.Object,
                mockSessionProvider.Object,
                mockConfig.Object);

            try
            {
                // Act
                var devices = await service.GetDevicesAsync("192.168.1.1", CancellationToken.None);

                // Assert
                Assert.Empty(devices);
            }
            finally
            {
                client.Dispose();
            }
        }

        [Fact]
        public async Task UniviewDeviceDiscoveryService_GetDevicesAsync_LocationIdMismatch_ReturnsEmptyList()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<UniviewDeviceDiscoveryService>>();
            var mockSessionProvider = new Mock<IUniviewSessionProvider>();
            var mockConfig = new Mock<IForensicsConfiguration>();

            var client = new UniviewClient("192.168.1.1", "admin", "password");
            mockSessionProvider.Setup(s => s.GetClient()).Returns(client);
            mockConfig.Setup(c => c.UniviewNvrHost).Returns("192.168.1.1");

            var service = new UniviewDeviceDiscoveryService(
                mockLogger.Object,
                mockSessionProvider.Object,
                mockConfig.Object);

            try
            {
                // Act - request devices for a different location ID
                var devices = await service.GetDevicesAsync("192.168.1.99", CancellationToken.None);

                // Assert - should return empty, not call client
                Assert.Empty(devices);
            }
            finally
            {
                client.Dispose();
            }
        }

        [Fact]
        public async Task UniviewDeviceDiscoveryService_GetDeviceAsync_InvalidDeviceId_ReturnsNull()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<UniviewDeviceDiscoveryService>>();
            var mockSessionProvider = new Mock<IUniviewSessionProvider>();
            var mockConfig = new Mock<IForensicsConfiguration>();

            var client = new UniviewClient("192.168.1.1", "admin", "password");
            mockSessionProvider.Setup(s => s.GetClient()).Returns(client);
            mockConfig.Setup(c => c.UniviewNvrHost).Returns("192.168.1.1");

            var service = new UniviewDeviceDiscoveryService(
                mockLogger.Object,
                mockSessionProvider.Object,
                mockConfig.Object);

            try
            {
                // Act - request device with non-numeric ID
                var device = await service.GetDeviceAsync("invalid", CancellationToken.None);

                // Assert
                Assert.Null(device);
            }
            finally
            {
                client.Dispose();
            }
        }

        [Fact]
        public void UniviewDeviceDiscoveryService_Constructor_ThrowsOnNullLogger()
        {
            // Arrange
            var mockSessionProvider = new Mock<IUniviewSessionProvider>();
            var mockConfig = new Mock<IForensicsConfiguration>();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new UniviewDeviceDiscoveryService(
                    null!,
                    mockSessionProvider.Object,
                    mockConfig.Object));
        }

        [Fact]
        public void UniviewDeviceDiscoveryService_Constructor_ThrowsOnNullSessionProvider()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<UniviewDeviceDiscoveryService>>();
            var mockConfig = new Mock<IForensicsConfiguration>();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new UniviewDeviceDiscoveryService(
                    mockLogger.Object,
                    null!,
                    mockConfig.Object));
        }

        [Fact]
        public void UniviewDeviceDiscoveryService_Constructor_ThrowsOnNullConfig()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<UniviewDeviceDiscoveryService>>();
            var mockSessionProvider = new Mock<IUniviewSessionProvider>();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new UniviewDeviceDiscoveryService(
                    mockLogger.Object,
                    mockSessionProvider.Object,
                    null!));
        }
    }
}
