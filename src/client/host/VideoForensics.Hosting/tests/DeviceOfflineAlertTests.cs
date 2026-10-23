using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Core.Providers.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Core.Contracts;
using VideoForensics.Client.Hosting.BackgroundServices;

using Device = VideoForensics.Data.Common.Entities.Device;

namespace VideoForensics.Client.Hosting.Tests
{
    /// <summary>
    /// Online-to-offline transitions raise a DeviceOffline alert. Only the transition does: a device that stays
    /// offline across ticks, or that was never seen online, must not alert on every tick.
    /// </summary>
    public class DeviceOfflineAlertTests
    {
        private const string DeviceName = "Front Door";

        private static (DeviceHealthSyncService Service, Mock<IDeviceHealthRepository> HealthRepo, Mock<IVideoForensicsDataClient> DataClient, Mock<INotificationDispatcher> Dispatcher)
            CreateService(bool connectedNow)
        {
            var device = new Device
            {
                Id = Guid.NewGuid(),
                LocationId = Guid.NewGuid(),
                ProviderDeviceId = "ring-1",
                Name = DeviceName,
                Type = "stickup_cam"
            };

            var deviceRepo = new Mock<IDeviceRepository>();
            _ = deviceRepo.Setup(r => r.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync([device]);

            var healthSource = new Mock<IProviderHealthSource>();
            _ = healthSource.Setup(h => h.FetchHealthAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync([new DeviceHealthReading("ring-1", Connected: connectedNow, BatteryPercentage: null, Rssi: null, WifiName: null, FirmwareVersion: null)]);

            var healthRepo = new Mock<IDeviceHealthRepository>();
            var dataClient = new Mock<IVideoForensicsDataClient>();
            _ = dataClient.Setup(d => d.RecordDeviceHealthAsync(It.IsAny<DeviceHealth>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((DeviceHealth h, CancellationToken _) => h);

            var budgetGuard = new Mock<IProviderApiBudgetGuard>();
            _ = budgetGuard.Setup(g => g.TryConsumeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

            var dispatcher = new Mock<INotificationDispatcher>();

            var services = new ServiceCollection();
            _ = services.AddSingleton(healthSource.Object);
            _ = services.AddSingleton(deviceRepo.Object);
            _ = services.AddSingleton(dataClient.Object);
            _ = services.AddSingleton(budgetGuard.Object);
            _ = services.AddSingleton(Mock.Of<ISecurityAuditLogger>());
            _ = services.AddSingleton(healthRepo.Object);
            _ = services.AddSingleton(dispatcher.Object);
            ServiceProvider provider = services.BuildServiceProvider();

            var service = new DeviceHealthSyncService(
                provider.GetRequiredService<IServiceScopeFactory>(),
                new ForensicsConfiguration(),
                new AlwaysOnAcPower(),
                Mock.Of<ILogger<DeviceHealthSyncService>>(),
                new ElevatedPollingWindowTracker());

            return (service, healthRepo, dataClient, dispatcher);
        }

        private static DeviceHealth PriorReading(bool wasOnline)
        {
            return new() { Id = Guid.NewGuid(), DeviceId = Guid.NewGuid(), IsOnline = wasOnline, CapturedAtUtc = DateTime.UtcNow.AddMinutes(-15) };
        }

        [Fact]
        public async Task RunOneTickAsync_OnlineToOffline_DispatchesDeviceOfflineAlert()
        {
            (DeviceHealthSyncService service, Mock<IDeviceHealthRepository> healthRepo, _, Mock<INotificationDispatcher> dispatcher) = CreateService(connectedNow: false);
            _ = healthRepo.Setup(r => r.GetLatestAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(PriorReading(wasOnline: true));

            await service.RunOneTickAsync(CancellationToken.None);

            dispatcher.Verify(d => d.DispatchAsync(
                It.Is<NotificationEvent>(e =>
                    e.EventType == "DeviceOffline"
                    && e.Severity == NoticeSeverity.Alert
                    && e.Audience == NotificationAudience.AdminsOnly
                    && e.Details != null && e.Details.Contains(DeviceName)),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task RunOneTickAsync_StillOffline_DoesNotDispatch()
        {
            (DeviceHealthSyncService service, Mock<IDeviceHealthRepository> healthRepo, _, Mock<INotificationDispatcher> dispatcher) = CreateService(connectedNow: false);
            _ = healthRepo.Setup(r => r.GetLatestAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(PriorReading(wasOnline: false));

            await service.RunOneTickAsync(CancellationToken.None);

            dispatcher.Verify(d => d.DispatchAsync(It.IsAny<NotificationEvent>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task RunOneTickAsync_StillOnline_DoesNotDispatch()
        {
            (DeviceHealthSyncService service, Mock<IDeviceHealthRepository> healthRepo, _, Mock<INotificationDispatcher> dispatcher) = CreateService(connectedNow: true);
            _ = healthRepo.Setup(r => r.GetLatestAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(PriorReading(wasOnline: true));

            await service.RunOneTickAsync(CancellationToken.None);

            dispatcher.Verify(d => d.DispatchAsync(It.IsAny<NotificationEvent>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task RunOneTickAsync_NoPriorReading_DoesNotDispatch()
        {
            (DeviceHealthSyncService service, _, _, Mock<INotificationDispatcher> dispatcher) = CreateService(connectedNow: false);

            await service.RunOneTickAsync(CancellationToken.None);

            dispatcher.Verify(d => d.DispatchAsync(It.IsAny<NotificationEvent>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task RunOneTickAsync_DispatchFails_StillPersistsReading()
        {
            (DeviceHealthSyncService service, Mock<IDeviceHealthRepository> healthRepo, Mock<IVideoForensicsDataClient> dataClient, Mock<INotificationDispatcher> dispatcher) = CreateService(connectedNow: false);
            _ = healthRepo.Setup(r => r.GetLatestAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(PriorReading(wasOnline: true));
            _ = dispatcher.Setup(d => d.DispatchAsync(It.IsAny<NotificationEvent>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("notification channel down"));

            await service.RunOneTickAsync(CancellationToken.None);

            dataClient.Verify(d => d.RecordDeviceHealthAsync(It.IsAny<DeviceHealth>(), It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}
