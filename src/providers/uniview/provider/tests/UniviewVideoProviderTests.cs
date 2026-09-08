using Microsoft.Extensions.Logging;
using Moq;
using VideoForensics.Providers.Common.Contracts;
using Xunit;

namespace VideoForensics.Providers.Uniview.Tests
{
    /// <summary>
    /// Tests for UniviewVideoProvider implementation.
    /// Verifies provider initialization and interface implementation.
    /// </summary>
    public class UniviewVideoProviderTests
    {
        [Fact]
        public void UniviewVideoProvider_HasCorrectProviderName()
        {
            // Arrange
            var mockLogger = new Mock<ILogger>();
            var mockAuthService = new Mock<IProviderAuthService>();
            var mockDeviceService = new Mock<IDeviceDiscoveryService>();
            var mockDownloadService = new Mock<IMediaDownloadService>();
            var mockEventService = new Mock<IEventAndConfigService>();

            // Act
            var provider = new UniviewVideoProvider(
                mockLogger.Object,
                mockAuthService.Object,
                mockDeviceService.Object,
                mockDownloadService.Object,
                mockEventService.Object);

            // Assert
            Assert.Equal("Uniview", provider.ProviderName);
        }

        [Fact]
        public void UniviewVideoProvider_ImplementsIVideoProvider()
        {
            // Arrange
            var mockLogger = new Mock<ILogger>();
            var mockAuthService = new Mock<IProviderAuthService>();
            var mockDeviceService = new Mock<IDeviceDiscoveryService>();
            var mockDownloadService = new Mock<IMediaDownloadService>();
            var mockEventService = new Mock<IEventAndConfigService>();

            // Act
            var provider = new UniviewVideoProvider(
                mockLogger.Object,
                mockAuthService.Object,
                mockDeviceService.Object,
                mockDownloadService.Object,
                mockEventService.Object);

            // Assert
            _ = Assert.IsAssignableFrom<IVideoProvider>(provider);
        }

        [Fact]
        public void UniviewVideoProvider_HasAllRequiredServices()
        {
            // Arrange
            var mockLogger = new Mock<ILogger>();
            var mockAuthService = new Mock<IProviderAuthService>();
            var mockDeviceService = new Mock<IDeviceDiscoveryService>();
            var mockDownloadService = new Mock<IMediaDownloadService>();
            var mockEventService = new Mock<IEventAndConfigService>();

            // Act
            var provider = new UniviewVideoProvider(
                mockLogger.Object,
                mockAuthService.Object,
                mockDeviceService.Object,
                mockDownloadService.Object,
                mockEventService.Object);

            // Assert
            Assert.NotNull(provider.AuthService);
            Assert.NotNull(provider.DeviceService);
            Assert.NotNull(provider.DownloadService);
            Assert.NotNull(provider.EventService);

            _ = Assert.IsAssignableFrom<IProviderAuthService>(provider.AuthService);
            _ = Assert.IsAssignableFrom<IDeviceDiscoveryService>(provider.DeviceService);
            _ = Assert.IsAssignableFrom<IMediaDownloadService>(provider.DownloadService);
            _ = Assert.IsAssignableFrom<IEventAndConfigService>(provider.EventService);
        }

        [Fact]
        public void UniviewVideoProvider_ServiceReferencesAreInjected()
        {
            // Arrange
            var mockLogger = new Mock<ILogger>();
            var mockAuthService = new Mock<IProviderAuthService>();
            var mockDeviceService = new Mock<IDeviceDiscoveryService>();
            var mockDownloadService = new Mock<IMediaDownloadService>();
            var mockEventService = new Mock<IEventAndConfigService>();

            // Act
            var provider = new UniviewVideoProvider(
                mockLogger.Object,
                mockAuthService.Object,
                mockDeviceService.Object,
                mockDownloadService.Object,
                mockEventService.Object);

            // Assert
            // Verify that the injected services are stored correctly
            Assert.Same(mockAuthService.Object, provider.AuthService);
            Assert.Same(mockDeviceService.Object, provider.DeviceService);
            Assert.Same(mockDownloadService.Object, provider.DownloadService);
            Assert.Same(mockEventService.Object, provider.EventService);
        }
    }
}
