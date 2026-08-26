using Microsoft.Extensions.Logging;
using Moq;
using VideoForensics.Providers.Common.Contracts;
using VideoForensics.Providers.Core;
using Xunit;

namespace VideoForensics.Providers.Core.Tests
{
    public class BaseVideoProviderTests
    {
        private class TestVideoProvider : BaseVideoProvider
        {
            public override string ProviderName => "TestProvider";
            public override IProviderAuthService AuthService { get; }
            public override IDeviceDiscoveryService DeviceService { get; }
            public override IMediaDownloadService DownloadService { get; }
            public override IEventAndConfigService EventService { get; }

            public TestVideoProvider(ILogger logger) : base(logger)
            {
                AuthService = new Mock<IProviderAuthService>().Object;
                DeviceService = new Mock<IDeviceDiscoveryService>().Object;
                DownloadService = new Mock<IMediaDownloadService>().Object;
                EventService = new Mock<IEventAndConfigService>().Object;
            }
        }

        [Fact]
        public void BaseVideoProvider_CanBeInstantiated()
        {
            // Arrange
            var mockLogger = new Mock<ILogger>();

            // Act
            var provider = new TestVideoProvider(mockLogger.Object);

            // Assert
            Assert.NotNull(provider);
            Assert.Equal("TestProvider", provider.ProviderName);
            Assert.NotNull(provider.AuthService);
            Assert.NotNull(provider.DeviceService);
            Assert.NotNull(provider.DownloadService);
            Assert.NotNull(provider.EventService);
        }

        [Fact]
        public void BaseVideoProvider_HasAllRequiredServices()
        {
            // Arrange
            var mockLogger = new Mock<ILogger>();
            var provider = new TestVideoProvider(mockLogger.Object);

            // Act & Assert - all services should be implemented by IVideoProvider
            Assert.IsAssignableFrom<IVideoProvider>(provider);
            Assert.NotNull(provider.AuthService);
            Assert.NotNull(provider.DeviceService);
            Assert.NotNull(provider.DownloadService);
            Assert.NotNull(provider.EventService);
        }
    }
}
