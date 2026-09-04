using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using VideoForensics.Client.Common;
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
        private static Device MakeDevice(string providerDeviceId) => new()
        {
            Id = Guid.NewGuid(),
            LocationId = Guid.NewGuid(),
            ProviderDeviceId = providerDeviceId,
            Name = "Test Device",
            Type = "camera"
        };

        private static (DeviceHealthSyncService Service, Mock<IProviderHealthSource> HealthSource, Mock<IDeviceRepository> DeviceRepo, Mock<IVideoForensicsDataClient> DataClient)
            CreateService(IForensicsConfiguration? config = null, IBatteryStatusProvider? batteryProvider = null)
        {
            var healthSource = new Mock<IProviderHealthSource>();
            var deviceRepo = new Mock<IDeviceRepository>();
            var dataClient = new Mock<IVideoForensicsDataClient>();

            var services = new ServiceCollection();
            services.AddSingleton(healthSource.Object);
            services.AddSingleton(deviceRepo.Object);
            services.AddSingleton(dataClient.Object);
            var provider = services.BuildServiceProvider();

            var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

            var service = new DeviceHealthSyncService(
                scopeFactory,
                config ?? new ForensicsConfiguration(),
                batteryProvider ?? new AlwaysOnAcPower(),
                Mock.Of<ILogger<DeviceHealthSyncService>>());

            return (service, healthSource, deviceRepo, dataClient);
        }

        [Fact]
        public async Task RunOneTickAsync_MatchesReadingToDeviceAndPersistsSnapshot()
        {
            var (service, healthSource, deviceRepo, dataClient) = CreateService();

            var device = MakeDevice("ring-123");
            deviceRepo.Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<Device> { device });

            healthSource.Setup(h => h.FetchHealthAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<DeviceHealthReading>
                {
                    new("ring-123", Connected: true, BatteryPercentage: 87m, Rssi: -55, WifiName: "HomeWifi", FirmwareVersion: "1.2.3")
                });

            DeviceHealthSnapshot? captured = null;
            dataClient.Setup(d => d.RecordDeviceHealthSnapshotAsync(It.IsAny<DeviceHealthSnapshot>(), It.IsAny<CancellationToken>()))
                .Callback<DeviceHealthSnapshot, CancellationToken>((s, _) => captured = s)
                .ReturnsAsync((DeviceHealthSnapshot s, CancellationToken _) => s);

            await service.RunOneTickAsync(CancellationToken.None);

            Assert.NotNull(captured);
            Assert.Equal(device.Id, captured!.DeviceId);
            Assert.Equal(-55, captured.Rssi);
            Assert.Equal(87m, captured.BatteryPercentage);
            Assert.Equal("HomeWifi", captured.WifiName);
            dataClient.Verify(d => d.RecordDeviceHealthSnapshotAsync(It.IsAny<DeviceHealthSnapshot>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task RunOneTickAsync_ReadingForUnknownDevice_IsSkipped()
        {
            var (service, healthSource, deviceRepo, dataClient) = CreateService();

            deviceRepo.Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<Device> { MakeDevice("ring-known") });

            healthSource.Setup(h => h.FetchHealthAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<DeviceHealthReading>
                {
                    new("ring-unmapped", true, null, -60, null, null)
                });

            await service.RunOneTickAsync(CancellationToken.None);

            dataClient.Verify(d => d.RecordDeviceHealthSnapshotAsync(It.IsAny<DeviceHealthSnapshot>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task RunOneTickAsync_HealthSourceThrows_IsSwallowedAndDoesNotPropagate()
        {
            var (service, healthSource, deviceRepo, dataClient) = CreateService();

            deviceRepo.Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<Device> { MakeDevice("ring-1") });

            healthSource.Setup(h => h.FetchHealthAsync(It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("provider API exploded"));

            // Must not throw - one provider's failure must not stop the whole tick.
            await service.RunOneTickAsync(CancellationToken.None);

            dataClient.Verify(d => d.RecordDeviceHealthSnapshotAsync(It.IsAny<DeviceHealthSnapshot>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task RunOneTickAsync_DeviceRepositoryThrows_IsSwallowedAndDoesNotPropagate()
        {
            var (service, healthSource, deviceRepo, dataClient) = CreateService();

            deviceRepo.Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("db unavailable"));

            await service.RunOneTickAsync(CancellationToken.None);

            healthSource.Verify(h => h.FetchHealthAsync(It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task RunOneTickAsync_NoDevices_DoesNotCallHealthSource()
        {
            var (service, healthSource, deviceRepo, _) = CreateService();

            deviceRepo.Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<Device>());

            await service.RunOneTickAsync(CancellationToken.None);

            healthSource.Verify(h => h.FetchHealthAsync(It.IsAny<CancellationToken>()), Times.Never);
        }
    }
}
