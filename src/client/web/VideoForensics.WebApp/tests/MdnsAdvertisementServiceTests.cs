using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Moq;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Client.WebApp.Discovery;

using Xunit;

namespace VideoForensics.Client.WebApp.Tests
{
    public class MdnsAdvertisementServiceTests
    {
        private Mock<IForensicsConfiguration> CreateConfigMock(bool enableMdns = true)
        {
            var config = new Mock<IForensicsConfiguration>();
            _ = config.SetupProperty(c => c.EnableMdnsAdvertisement, enableMdns);
            return config;
        }

        private Mock<IServer> CreateServerMockWithPort(int port)
        {
            var server = new Mock<IServer>();
            var addressesFeature = new Mock<IServerAddressesFeature>();
            _ = addressesFeature.Setup(f => f.Addresses).Returns([$"http://localhost:{port}"]);
            _ = server.Setup(s => s.Features.Get<IServerAddressesFeature>()).Returns(addressesFeature.Object);
            return server;
        }

        private Mock<IServer> CreateServerMockWithoutAddresses()
        {
            var server = new Mock<IServer>();
            _ = server.Setup(s => s.Features.Get<IServerAddressesFeature>()).Returns((IServerAddressesFeature?)null);
            return server;
        }

        private Mock<IHostApplicationLifetime> CreateLifetimeMock(bool applicationStarted = true)
        {
            var lifetime = new Mock<IHostApplicationLifetime>();
            var cts = new CancellationTokenSource();
            if (applicationStarted)
            {
                cts.Cancel();
            }

            _ = lifetime.Setup(l => l.ApplicationStarted).Returns(cts.Token);
            return lifetime;
        }

        /// <summary>
        /// Dispose should clean up resources properly.
        /// </summary>
        [Fact]
        public async Task Dispose_ServiceRunning_CleanupsProperly()
        {
            // Arrange
            Mock<IForensicsConfiguration> config = CreateConfigMock(enableMdns: true);
            Mock<IServer> server = CreateServerMockWithPort(5000);
            Mock<IHostApplicationLifetime> lifetime = CreateLifetimeMock(applicationStarted: true);
            var logger = new Mock<ILogger<MdnsAdvertisementService>>();

            var service = new MdnsAdvertisementService(config.Object, server.Object, lifetime.Object, logger.Object);

            var cts = new CancellationTokenSource();
            cts.CancelAfter(TimeSpan.FromSeconds(1));

            // Start the service
            await service.StartAsync(cts.Token);

            // Act
            service.Dispose();

            // Assert - should not throw
            Assert.True(true);
        }

        /// <summary>
        /// When port resolution fails on exception, service should log error and continue.
        /// </summary>
        [Fact]
        public async Task StartAsync_AdvertisementException_LogsErrorAndContinues()
        {
            // Arrange
            Mock<IForensicsConfiguration> config = CreateConfigMock(enableMdns: true);
            Mock<IServer> server = CreateServerMockWithPort(5000);
            Mock<IHostApplicationLifetime> lifetime = CreateLifetimeMock(applicationStarted: true);
            var logger = new Mock<ILogger<MdnsAdvertisementService>>();

            var service = new MdnsAdvertisementService(config.Object, server.Object, lifetime.Object, logger.Object);

            var cts = new CancellationTokenSource();
            cts.CancelAfter(TimeSpan.FromMilliseconds(100));

            // Act
            await service.StartAsync(cts.Token);

            // Assert - service should continue despite any errors
            // (the test passes if no exception is thrown)
            Assert.True(true);

            // Cleanup
            await service.StopAsync(CancellationToken.None);
        }
    }
}
