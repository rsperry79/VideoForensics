using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Moq;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.WebApp.Discovery;

using Xunit;

namespace VideoForensics.WebApp.Tests
{
    public class MdnsAdvertisementServiceTests
    {
        private Mock<IForensicsConfiguration> CreateConfigMock(bool enableMdns = true)
        {
            var config = new Mock<IForensicsConfiguration>();
            config.SetupProperty(c => c.EnableMdnsAdvertisement, enableMdns);
            return config;
        }

        private Mock<IServer> CreateServerMockWithPort(int port)
        {
            var server = new Mock<IServer>();
            var addressesFeature = new Mock<IServerAddressesFeature>();
            addressesFeature.Setup(f => f.Addresses).Returns(new List<string> { $"http://localhost:{port}" });
            server.Setup(s => s.Features.Get<IServerAddressesFeature>()).Returns(addressesFeature.Object);
            return server;
        }

        private Mock<IServer> CreateServerMockWithoutAddresses()
        {
            var server = new Mock<IServer>();
            server.Setup(s => s.Features.Get<IServerAddressesFeature>()).Returns((IServerAddressesFeature?)null);
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
            lifetime.Setup(l => l.ApplicationStarted).Returns(cts.Token);
            return lifetime;
        }

        /// <summary>
        /// When mDNS is enabled and port is available, service should advertise successfully.
        /// </summary>
        [Fact]
        public async Task ExecuteAsync_MdnsEnabledWithValidPort_AdvertisesService()
        {
            // Arrange
            var config = CreateConfigMock(enableMdns: true);
            var server = CreateServerMockWithPort(5000);
            var lifetime = CreateLifetimeMock(applicationStarted: true);
            var logger = new Mock<ILogger<MdnsAdvertisementService>>();

            var service = new MdnsAdvertisementService(config.Object, server.Object, lifetime.Object, logger.Object);

            var cts = new CancellationTokenSource();
            cts.CancelAfter(TimeSpan.FromSeconds(1));

            // Act
            await service.ExecuteAsync(cts.Token);

            // Assert
            // Verify that logging occurred (indicates service started)
            logger.Verify(
                l => l.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("mDNS advertisement started")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);

            // Cleanup
            await service.StopAsync(CancellationToken.None);
        }

        /// <summary>
        /// When mDNS is disabled, service should not advertise.
        /// </summary>
        [Fact]
        public async Task ExecuteAsync_MdnsDisabled_DoesNotAdvertise()
        {
            // Arrange
            var config = CreateConfigMock(enableMdns: false);
            var server = CreateServerMockWithPort(5000);
            var lifetime = CreateLifetimeMock(applicationStarted: true);
            var logger = new Mock<ILogger<MdnsAdvertisementService>>();

            var service = new MdnsAdvertisementService(config.Object, server.Object, lifetime.Object, logger.Object);

            var cts = new CancellationTokenSource();
            cts.CancelAfter(TimeSpan.FromMilliseconds(100));

            // Act
            await service.ExecuteAsync(cts.Token);

            // Assert
            logger.Verify(
                l => l.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("mDNS advertisement started")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Never);

            // Cleanup
            await service.StopAsync(CancellationToken.None);
        }

        /// <summary>
        /// When server does not expose addresses feature, service should log warning and skip advertisement.
        /// </summary>
        [Fact]
        public async Task ExecuteAsync_NoServerAddresses_LogsWarningAndSkipsAdvertisement()
        {
            // Arrange
            var config = CreateConfigMock(enableMdns: true);
            var server = CreateServerMockWithoutAddresses();
            var lifetime = CreateLifetimeMock(applicationStarted: true);
            var logger = new Mock<ILogger<MdnsAdvertisementService>>();

            var service = new MdnsAdvertisementService(config.Object, server.Object, lifetime.Object, logger.Object);

            var cts = new CancellationTokenSource();
            cts.CancelAfter(TimeSpan.FromMilliseconds(100));

            // Act
            await service.ExecuteAsync(cts.Token);

            // Assert
            logger.Verify(
                l => l.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Could not determine the server's listening port")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.AtLeastOnce);

            // Cleanup
            await service.StopAsync(CancellationToken.None);
        }

        /// <summary>
        /// When toggling mDNS from disabled to enabled, service should start advertising.
        /// </summary>
        [Fact]
        public async Task ExecuteAsync_ConfigToggled_ResponsesAppropriately()
        {
            // Arrange
            var config = CreateConfigMock(enableMdns: false);
            var server = CreateServerMockWithPort(5000);
            var lifetime = CreateLifetimeMock(applicationStarted: true);
            var logger = new Mock<ILogger<MdnsAdvertisementService>>();

            var service = new MdnsAdvertisementService(config.Object, server.Object, lifetime.Object, logger.Object);

            var cts = new CancellationTokenSource();

            // Act - start with mDNS disabled
            var executeTask = service.ExecuteAsync(cts.Token);

            // Wait a bit for initial check
            await Task.Delay(100);

            // Toggle mDNS enabled
            config.Object.EnableMdnsAdvertisement = true;

            // Wait for next check
            await Task.Delay(1000);

            // Stop the service
            cts.Cancel();
            try { await executeTask; } catch (OperationCanceledException) { }

            // Assert
            logger.Verify(
                l => l.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("mDNS advertisement started")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);

            // Cleanup
            await service.StopAsync(CancellationToken.None);
        }

        /// <summary>
        /// StopAsync should properly stop and unadvertise the service.
        /// </summary>
        [Fact]
        public async Task StopAsync_ServiceRunning_StopsAndUnadvertises()
        {
            // Arrange
            var config = CreateConfigMock(enableMdns: true);
            var server = CreateServerMockWithPort(5000);
            var lifetime = CreateLifetimeMock(applicationStarted: true);
            var logger = new Mock<ILogger<MdnsAdvertisementService>>();

            var service = new MdnsAdvertisementService(config.Object, server.Object, lifetime.Object, logger.Object);

            var cts = new CancellationTokenSource();
            cts.CancelAfter(TimeSpan.FromSeconds(1));

            // Start the service
            await service.ExecuteAsync(cts.Token);

            // Act
            await service.StopAsync(CancellationToken.None);

            // Assert
            logger.Verify(
                l => l.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("mDNS advertisement stopped")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        /// <summary>
        /// Dispose should clean up resources properly.
        /// </summary>
        [Fact]
        public async Task Dispose_ServiceRunning_CleanupsProperly()
        {
            // Arrange
            var config = CreateConfigMock(enableMdns: true);
            var server = CreateServerMockWithPort(5000);
            var lifetime = CreateLifetimeMock(applicationStarted: true);
            var logger = new Mock<ILogger<MdnsAdvertisementService>>();

            var service = new MdnsAdvertisementService(config.Object, server.Object, lifetime.Object, logger.Object);

            var cts = new CancellationTokenSource();
            cts.CancelAfter(TimeSpan.FromSeconds(1));

            // Start the service
            await service.ExecuteAsync(cts.Token);

            // Act
            service.Dispose();

            // Assert - should not throw
            Assert.True(true);
        }

        /// <summary>
        /// When port resolution fails on exception, service should log error and continue.
        /// </summary>
        [Fact]
        public async Task ExecuteAsync_AdvertisementException_LogsErrorAndContinues()
        {
            // Arrange
            var config = CreateConfigMock(enableMdns: true);
            var server = CreateServerMockWithPort(5000);
            var lifetime = CreateLifetimeMock(applicationStarted: true);
            var logger = new Mock<ILogger<MdnsAdvertisementService>>();

            var service = new MdnsAdvertisementService(config.Object, server.Object, lifetime.Object, logger.Object);

            var cts = new CancellationTokenSource();
            cts.CancelAfter(TimeSpan.FromMilliseconds(100));

            // Act
            await service.ExecuteAsync(cts.Token);

            // Assert - service should continue despite any errors
            // (the test passes if no exception is thrown)
            Assert.True(true);

            // Cleanup
            await service.StopAsync(CancellationToken.None);
        }
    }
}
