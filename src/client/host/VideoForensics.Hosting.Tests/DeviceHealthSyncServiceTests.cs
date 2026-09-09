using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Moq;

using VideoForensics.Client.Common;
using VideoForensics.Client.Common.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Core.Contracts;
using VideoForensics.Hosting.BackgroundServices;
using VideoForensics.Providers.Common.Contracts;

using Xunit;

using Device = VideoForensics.Data.Common.Entities.Device;

namespace VideoForensics.Hosting.Tests
{
    public class DeviceHealthSyncServiceTests
    {
        private static Device MakeDevice(string providerDeviceId)
        {
            return new()
            {
                Id = Guid.NewGuid(),
                LocationId = Guid.NewGuid(),
                ProviderDeviceId = providerDeviceId,
                Name = "Test Device",
                Type = "camera"
            };
        }

        private static (DeviceHealthSyncService Service, Mock<IProviderHealthSource> HealthSource, Mock<IDeviceRepository> DeviceRepo, Mock<IVideoForensicsDataClient> DataClient, Mock<IProviderApiBudgetGuard> BudgetGuard)
            CreateService(IForensicsConfiguration? config = null, IBatteryStatusProvider? batteryProvider = null)
        {
            var healthSource = new Mock<IProviderHealthSource>();
            var deviceRepo = new Mock<IDeviceRepository>();
            var dataClient = new Mock<IVideoForensicsDataClient>();

            var budgetGuard = new Mock<IProviderApiBudgetGuard>();
            _ = budgetGuard.Setup(g => g.TryConsumeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
            var auditLog = new Mock<ISecurityAuditLogger>();

            var services = new ServiceCollection();
            _ = services.AddSingleton(healthSource.Object);
            _ = services.AddSingleton(deviceRepo.Object);
            _ = services.AddSingleton(dataClient.Object);
            _ = services.AddSingleton(budgetGuard.Object);
            _ = services.AddSingleton(auditLog.Object);
            ServiceProvider provider = services.BuildServiceProvider();

            IServiceScopeFactory scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

            var service = new DeviceHealthSyncService(
                scopeFactory,
                config ?? new ForensicsConfiguration(),
                batteryProvider ?? new AlwaysOnAcPower(),
                Mock.Of<ILogger<DeviceHealthSyncService>>());

            return (service, healthSource, deviceRepo, dataClient, budgetGuard);
        }

        [Fact]
        public async Task RunOneTickAsync_MatchesReadingToDeviceAndPersistsSnapshot()
        {
            (DeviceHealthSyncService? service, Mock<IProviderHealthSource>? healthSource, Mock<IDeviceRepository>? deviceRepo, Mock<IVideoForensicsDataClient>? dataClient, _) = CreateService();

            Device device = MakeDevice("ring-123");
            _ = deviceRepo.Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync([device]);

            _ = healthSource.Setup(h => h.FetchHealthAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(
                [
                    new("ring-123", Connected: true, BatteryPercentage: 87m, Rssi: -55, WifiName: "HomeWifi", FirmwareVersion: "1.2.3")
                ]);

            DeviceHealth? captured = null;
            _ = dataClient.Setup(d => d.RecordDeviceHealthAsync(It.IsAny<DeviceHealth>(), It.IsAny<CancellationToken>()))
                .Callback<DeviceHealth, CancellationToken>((h, _) => captured = h)
                .ReturnsAsync((DeviceHealth h, CancellationToken _) => h);

            await service.RunOneTickAsync(CancellationToken.None);

            Assert.NotNull(captured);
            Assert.Equal(device.Id, captured!.DeviceId);
            Assert.Equal(-55, captured.WifiSignalRssi);
            Assert.Equal(87m, captured.BatteryPercentage);
            Assert.Equal("HomeWifi", captured.WifiName);
            dataClient.Verify(d => d.RecordDeviceHealthAsync(It.IsAny<DeviceHealth>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task RunOneTickAsync_ReadingForUnknownDevice_IsSkipped()
        {
            (DeviceHealthSyncService? service, Mock<IProviderHealthSource>? healthSource, Mock<IDeviceRepository>? deviceRepo, Mock<IVideoForensicsDataClient>? dataClient, _) = CreateService();

            _ = deviceRepo.Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync([MakeDevice("ring-known")]);

            _ = healthSource.Setup(h => h.FetchHealthAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(
                [
                    new("ring-unmapped", true, null, -60, null, null)
                ]);

            await service.RunOneTickAsync(CancellationToken.None);

            dataClient.Verify(d => d.RecordDeviceHealthAsync(It.IsAny<DeviceHealth>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task RunOneTickAsync_HealthSourceThrows_IsSwallowedAndDoesNotPropagate()
        {
            (DeviceHealthSyncService? service, Mock<IProviderHealthSource>? healthSource, Mock<IDeviceRepository>? deviceRepo, Mock<IVideoForensicsDataClient>? dataClient, _) = CreateService();

            _ = deviceRepo.Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync([MakeDevice("ring-1")]);

            _ = healthSource.Setup(h => h.FetchHealthAsync(It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("provider API exploded"));

            // Must not throw - one provider's failure must not stop the whole tick.
            await service.RunOneTickAsync(CancellationToken.None);

            dataClient.Verify(d => d.RecordDeviceHealthAsync(It.IsAny<DeviceHealth>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task RunOneTickAsync_DeviceRepositoryThrows_IsSwallowedAndDoesNotPropagate()
        {
            (DeviceHealthSyncService? service, Mock<IProviderHealthSource>? healthSource, Mock<IDeviceRepository>? deviceRepo, Mock<IVideoForensicsDataClient>? dataClient, _) = CreateService();

            _ = deviceRepo.Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("db unavailable"));

            await service.RunOneTickAsync(CancellationToken.None);

            healthSource.Verify(h => h.FetchHealthAsync(It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task RunOneTickAsync_NoDevices_DoesNotCallHealthSource()
        {
            (DeviceHealthSyncService? service, Mock<IProviderHealthSource>? healthSource, Mock<IDeviceRepository>? deviceRepo, _, _) = CreateService();

            _ = deviceRepo.Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync([]);

            await service.RunOneTickAsync(CancellationToken.None);

            healthSource.Verify(h => h.FetchHealthAsync(It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task RunOneTickAsync_BudgetExceeded_SkipsHealthSourceWithoutThrowing()
        {
            (DeviceHealthSyncService? service, Mock<IProviderHealthSource>? healthSource, Mock<IDeviceRepository>? deviceRepo, Mock<IVideoForensicsDataClient>? dataClient, Mock<IProviderApiBudgetGuard>? budgetGuard) = CreateService();

            _ = deviceRepo.Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync([MakeDevice("ring-1")]);

            _ = budgetGuard.Setup(g => g.TryConsumeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);

            await service.RunOneTickAsync(CancellationToken.None);

            healthSource.Verify(h => h.FetchHealthAsync(It.IsAny<CancellationToken>()), Times.Never);
            dataClient.Verify(d => d.RecordDeviceHealthAsync(It.IsAny<DeviceHealth>(), It.IsAny<CancellationToken>()), Times.Never);
        }
    }
}
