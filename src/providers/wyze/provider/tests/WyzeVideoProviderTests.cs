using Microsoft.Extensions.Logging;
using Moq;
using VideoForensics.Providers.Common.Contracts;
using Xunit;

namespace VideoForensics.Providers.Wyze.Tests
{
    /// <summary>
    /// Tests for WyzeVideoProvider implementation.
    /// Verifies provider initialization and interface implementation.
    /// </summary>
    public class WyzeVideoProviderTests
    {
        [Fact]
        public void WyzeVideoProvider_HasCorrectProviderName()
        {
            // Arrange
            var mockLogger = new Mock<ILogger>();
            var mockAuthService = new Mock<IProviderAuthService>();
            var mockDeviceService = new Mock<IDeviceDiscoveryService>();
            var mockDownloadService = new Mock<IMediaDownloadService>();
            var mockEventService = new Mock<IEventAndConfigService>();

            // Act
            var provider = new WyzeVideoProvider(
                mockLogger.Object,
                mockAuthService.Object,
                mockDeviceService.Object,
                mockDownloadService.Object,
                mockEventService.Object);

            // Assert
            Assert.Equal("Wyze", provider.ProviderName);
        }

        [Fact]
        public void WyzeVideoProvider_ImplementsIVideoProvider()
        {
            // Arrange
            var mockLogger = new Mock<ILogger>();
            var mockAuthService = new Mock<IProviderAuthService>();
            var mockDeviceService = new Mock<IDeviceDiscoveryService>();
            var mockDownloadService = new Mock<IMediaDownloadService>();
            var mockEventService = new Mock<IEventAndConfigService>();

            // Act
            var provider = new WyzeVideoProvider(
                mockLogger.Object,
                mockAuthService.Object,
                mockDeviceService.Object,
                mockDownloadService.Object,
                mockEventService.Object);

            // Assert
            Assert.IsAssignableFrom<IVideoProvider>(provider);
        }

        [Fact]
        public void WyzeVideoProvider_HasAllRequiredServices()
        {
            // Arrange
            var mockLogger = new Mock<ILogger>();
            var mockAuthService = new Mock<IProviderAuthService>();
            var mockDeviceService = new Mock<IDeviceDiscoveryService>();
            var mockDownloadService = new Mock<IMediaDownloadService>();
            var mockEventService = new Mock<IEventAndConfigService>();

            // Act
            var provider = new WyzeVideoProvider(
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

            Assert.IsAssignableFrom<IProviderAuthService>(provider.AuthService);
            Assert.IsAssignableFrom<IDeviceDiscoveryService>(provider.DeviceService);
            Assert.IsAssignableFrom<IMediaDownloadService>(provider.DownloadService);
            Assert.IsAssignableFrom<IEventAndConfigService>(provider.EventService);
        }

        [Fact]
        public void WyzeVideoProvider_ServiceReferencesAreInjected()
        {
            // Arrange
            var mockLogger = new Mock<ILogger>();
            var mockAuthService = new Mock<IProviderAuthService>();
            var mockDeviceService = new Mock<IDeviceDiscoveryService>();
            var mockDownloadService = new Mock<IMediaDownloadService>();
            var mockEventService = new Mock<IEventAndConfigService>();

            // Act
            var provider = new WyzeVideoProvider(
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
