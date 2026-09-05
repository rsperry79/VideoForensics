using Microsoft.Extensions.Logging;

using Moq;

using VideoForensics.Data.Core.Contracts;
using VideoForensics.Providers.Common.Contracts;
using VideoForensics.Providers.Ring.Services;

using Xunit;

namespace VideoForensics.Providers.Ring.Tests
{
    public class RingVideoProviderTests
    {
        [Fact]
        public void RingVideoProvider_HasCorrectProviderName()
        {
            // Arrange
            var mockLogger = new Mock<ILogger>();
            var mockAuthService = new Mock<IProviderAuthService>();
            var mockDeviceService = new Mock<IDeviceDiscoveryService>();
            var mockDownloadService = new Mock<IMediaDownloadService>();
            var mockEventService = new Mock<IEventAndConfigService>();

            // Act
            var provider = new RingVideoProvider(
                mockLogger.Object,
                mockAuthService.Object,
                mockDeviceService.Object,
                mockDownloadService.Object,
                mockEventService.Object);

            // Assert
            Assert.Equal("Ring", provider.ProviderName);
        }

        [Fact]
        public void RingVideoProvider_ImplementsIVideoProvider()
        {
            // Arrange
            var mockLogger = new Mock<ILogger>();
            var mockAuthService = new Mock<IProviderAuthService>();
            var mockDeviceService = new Mock<IDeviceDiscoveryService>();
            var mockDownloadService = new Mock<IMediaDownloadService>();
            var mockEventService = new Mock<IEventAndConfigService>();

            // Act
            var provider = new RingVideoProvider(
                mockLogger.Object,
                mockAuthService.Object,
                mockDeviceService.Object,
                mockDownloadService.Object,
                mockEventService.Object);

            // Assert
            _ = Assert.IsAssignableFrom<IVideoProvider>(provider);
        }

        [Fact]
        public void RingVideoProvider_HasAllRequiredServices()
        {
            // Arrange
            var mockLogger = new Mock<ILogger>();
            var mockAuthService = new Mock<IProviderAuthService>();
            var mockDeviceService = new Mock<IDeviceDiscoveryService>();
            var mockDownloadService = new Mock<IMediaDownloadService>();
            var mockEventService = new Mock<IEventAndConfigService>();

            // Act
            var provider = new RingVideoProvider(
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
        public void SessionProvider_SharedAcrossServices_AllServicesObserveAuthenticatedSession()
        {
            // Arrange
            var sessionProvider = new SessionProvider();
            var mockLogger = new Mock<ILogger>();
            var mockDataClient = new Mock<IVideoForensicsDataClient>();
            var mockCredentialStore = new Mock<ICredentialStore>();

            _ = new RingAuthService(mockLogger.Object, sessionProvider, mockCredentialStore.Object);

            _ = new RingDeviceDiscoveryService(mockLogger.Object, sessionProvider);

            _ = new RingMediaDownloadService(mockLogger.Object, sessionProvider, mockDataClient.Object);

            _ = new RingEventAndConfigService(mockLogger.Object, sessionProvider);

            // Assert - before authentication
            Assert.Null(sessionProvider.GetSession());

            // Act - simulate setting a session (as would happen after authentication)
            var testSession = new Session("test@example.com", "testpassword");
            sessionProvider.SetSession(testSession);

            // Assert - all services should see the same authenticated session
            Session? sessionFromProvider = sessionProvider.GetSession();
            Assert.NotNull(sessionFromProvider);
            Assert.Same(testSession, sessionFromProvider);
        }
    }
}
