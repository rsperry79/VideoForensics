using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Moq;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Client.Core.Tools;
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
            CreateService(IForensicsConfiguration? config = null, IBatteryStatusProvider? batteryProvider = null, ElevatedPollingWindowTracker? elevatedTracker = null)
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
                Mock.Of<ILogger<DeviceHealthSyncService>>(),
                elevatedTracker ?? new ElevatedPollingWindowTracker());

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

        [Fact]
        public async Task RunOneTickAsync_SingleDegradedReading_EntersElevatedPollingWindow()
        {
            var elevatedTracker = new ElevatedPollingWindowTracker();
            var healthRepository = new Mock<IDeviceHealthRepository>();

            var services = new ServiceCollection();
            var healthSource = new Mock<IProviderHealthSource>();
            var deviceRepo = new Mock<IDeviceRepository>();
            var dataClient = new Mock<IVideoForensicsDataClient>();
            var budgetGuard = new Mock<IProviderApiBudgetGuard>();
            _ = budgetGuard.Setup(g => g.TryConsumeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
            var auditLog = new Mock<ISecurityAuditLogger>();

            _ = services.AddSingleton(healthSource.Object);
            _ = services.AddSingleton(deviceRepo.Object);
            _ = services.AddSingleton(dataClient.Object);
            _ = services.AddSingleton(budgetGuard.Object);
            _ = services.AddSingleton(auditLog.Object);
            _ = services.AddSingleton(healthRepository.Object);

            ServiceProvider provider = services.BuildServiceProvider();
            IServiceScopeFactory scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

            var service = new DeviceHealthSyncService(
                scopeFactory,
                new ForensicsConfiguration(),
                new AlwaysOnAcPower(),
                Mock.Of<ILogger<DeviceHealthSyncService>>(),
                elevatedTracker);

            Device device = MakeDevice("ring-123");
            _ = deviceRepo.Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync([device]);

            // Fresh reading with low RSSI
            _ = healthSource.Setup(h => h.FetchHealthAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync([
                    new("ring-123", Connected: true, BatteryPercentage: 87m, Rssi: -75, WifiName: "HomeWifi", FirmwareVersion: "1.2.3")
                ]);

            // History shows baseline around -60, so -75 is ~15 dB below (exceeds 8 dB threshold)
            _ = healthRepository.Setup(r => r.GetHistoryAsync(device.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync([
                    new() { WifiSignalRssi = -60, CapturedAtUtc = DateTime.UtcNow.AddMinutes(-5) },
                    new() { WifiSignalRssi = -58, CapturedAtUtc = DateTime.UtcNow.AddMinutes(-3) },
                    new() { WifiSignalRssi = -62, CapturedAtUtc = DateTime.UtcNow.AddMinutes(-1) }
                ]);

            await service.RunOneTickAsync(CancellationToken.None);

            Assert.True(elevatedTracker.IsElevated(device.Id));
        }

        [Fact]
        public async Task RunOneTickAsync_ReadingWithinBaseline_DoesNotEnterElevatedWindow()
        {
            var elevatedTracker = new ElevatedPollingWindowTracker();
            var healthRepository = new Mock<IDeviceHealthRepository>();

            var services = new ServiceCollection();
            var healthSource = new Mock<IProviderHealthSource>();
            var deviceRepo = new Mock<IDeviceRepository>();
            var dataClient = new Mock<IVideoForensicsDataClient>();
            var budgetGuard = new Mock<IProviderApiBudgetGuard>();
            _ = budgetGuard.Setup(g => g.TryConsumeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
            var auditLog = new Mock<ISecurityAuditLogger>();

            _ = services.AddSingleton(healthSource.Object);
            _ = services.AddSingleton(deviceRepo.Object);
            _ = services.AddSingleton(dataClient.Object);
            _ = services.AddSingleton(budgetGuard.Object);
            _ = services.AddSingleton(auditLog.Object);
            _ = services.AddSingleton(healthRepository.Object);

            ServiceProvider provider = services.BuildServiceProvider();
            IServiceScopeFactory scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

            var service = new DeviceHealthSyncService(
                scopeFactory,
                new ForensicsConfiguration(),
                new AlwaysOnAcPower(),
                Mock.Of<ILogger<DeviceHealthSyncService>>(),
                elevatedTracker);

            Device device = MakeDevice("ring-123");
            _ = deviceRepo.Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync([device]);

            // Reading close to baseline
            _ = healthSource.Setup(h => h.FetchHealthAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync([
                    new("ring-123", Connected: true, BatteryPercentage: 87m, Rssi: -62, WifiName: "HomeWifi", FirmwareVersion: "1.2.3")
                ]);

            // History shows baseline around -60, so -62 is only ~2 dB below (below 8 dB threshold)
            _ = healthRepository.Setup(r => r.GetHistoryAsync(device.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync([
                    new() { WifiSignalRssi = -60, CapturedAtUtc = DateTime.UtcNow.AddMinutes(-5) },
                    new() { WifiSignalRssi = -58, CapturedAtUtc = DateTime.UtcNow.AddMinutes(-3) },
                    new() { WifiSignalRssi = -62, CapturedAtUtc = DateTime.UtcNow.AddMinutes(-1) }
                ]);

            await service.RunOneTickAsync(CancellationToken.None);

            Assert.False(elevatedTracker.IsElevated(device.Id));
        }

        [Fact]
        public async Task RunElevatedTickAsync_NoElevatedDevices_DoesNothing()
        {
            var elevatedTracker = new ElevatedPollingWindowTracker();

            var services = new ServiceCollection();
            var healthSource = new Mock<IProviderHealthSource>();
            var deviceRepo = new Mock<IDeviceRepository>();
            var budgetGuard = new Mock<IProviderApiBudgetGuard>();
            var jammingOrchestrator = new Mock<JammingToolsOrchestrator>();
            var liveViewService = new Mock<ILiveViewSessionService>();
            var auditLog = new Mock<ISecurityAuditLogger>();

            _ = services.AddSingleton(healthSource.Object);
            _ = services.AddSingleton(deviceRepo.Object);
            _ = services.AddSingleton(budgetGuard.Object);
            _ = services.AddSingleton(jammingOrchestrator.Object);
            _ = services.AddSingleton(liveViewService.Object);
            _ = services.AddSingleton(auditLog.Object);

            ServiceProvider provider = services.BuildServiceProvider();
            IServiceScopeFactory scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

            var service = new DeviceHealthSyncService(
                scopeFactory,
                new ForensicsConfiguration(),
                new AlwaysOnAcPower(),
                Mock.Of<ILogger<DeviceHealthSyncService>>(),
                elevatedTracker);

            await service.RunElevatedTickAsync(CancellationToken.None);

            // No elevated devices, so nothing should be called
            healthSource.Verify(h => h.FetchHealthAsync(It.IsAny<CancellationToken>()), Times.Never);
            deviceRepo.Verify(r => r.ListAsync(It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task RunElevatedTickAsync_JammingConfirmed_NoActiveSession_StartsSession()
        {
            var elevatedTracker = new ElevatedPollingWindowTracker();
            var device = MakeDevice("ring-123");
            elevatedTracker.EnterElevated(device.Id, TimeSpan.FromSeconds(30));

            var services = new ServiceCollection();
            var healthSource = new Mock<IProviderHealthSource>();
            var deviceRepo = new Mock<IDeviceRepository>();
            var dataClient = new Mock<IVideoForensicsDataClient>();
            var budgetGuard = new Mock<IProviderApiBudgetGuard>();
            _ = budgetGuard.Setup(g => g.TryConsumeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
            var jammingOrchestrator = new Mock<JammingToolsOrchestrator>();
            var liveViewService = new Mock<ILiveViewSessionService>();
            var auditLog = new Mock<ISecurityAuditLogger>();

            _ = services.AddSingleton(healthSource.Object);
            _ = services.AddSingleton(deviceRepo.Object);
            _ = services.AddSingleton(dataClient.Object);
            _ = services.AddSingleton(budgetGuard.Object);
            _ = services.AddSingleton(jammingOrchestrator.Object);
            _ = services.AddSingleton(liveViewService.Object);
            _ = services.AddSingleton(auditLog.Object);

            ServiceProvider provider = services.BuildServiceProvider();
            IServiceScopeFactory scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

            var service = new DeviceHealthSyncService(
                scopeFactory,
                new ForensicsConfiguration(),
                new AlwaysOnAcPower(),
                Mock.Of<ILogger<DeviceHealthSyncService>>(),
                elevatedTracker);

            _ = deviceRepo.Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync([device]);

            _ = healthSource.Setup(h => h.FetchHealthAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync([
                    new("ring-123", Connected: true, BatteryPercentage: 87m, Rssi: -60, WifiName: "HomeWifi", FirmwareVersion: "1.2.3")
                ]);

            // Jamming analysis returns 1 newly detected incident
            _ = jammingOrchestrator.Setup(j => j.AnalyzeJammingAsync(device.Id, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new JammingAnalysisReport { Success = true, NewlyDetectedCount = 1, DeviceId = device.Id });

            // No active session
            _ = liveViewService.Setup(l => l.GetActiveSessionAsync(device.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync((LiveViewSession?)null);

            var newSession = new LiveViewSession { Id = Guid.NewGuid(), DeviceId = device.Id, TriggerReason = LiveViewTriggerReason.JammingConfirmed };
            _ = liveViewService.Setup(l => l.StartAsync(device.Id, LiveViewTriggerReason.JammingConfirmed, null, It.IsAny<CancellationToken>()))
                .ReturnsAsync(newSession);

            await service.RunElevatedTickAsync(CancellationToken.None);

            liveViewService.Verify(
                l => l.StartAsync(device.Id, LiveViewTriggerReason.JammingConfirmed, null, It.IsAny<CancellationToken>()),
                Times.Once);
            Assert.False(elevatedTracker.IsElevated(device.Id));
        }

        [Fact]
        public async Task RunElevatedTickAsync_JammingConfirmed_ActiveSessionExists_PromotesToSustained()
        {
            var elevatedTracker = new ElevatedPollingWindowTracker();
            var device = MakeDevice("ring-123");
            elevatedTracker.EnterElevated(device.Id, TimeSpan.FromSeconds(30));

            var services = new ServiceCollection();
            var healthSource = new Mock<IProviderHealthSource>();
            var deviceRepo = new Mock<IDeviceRepository>();
            var dataClient = new Mock<IVideoForensicsDataClient>();
            var budgetGuard = new Mock<IProviderApiBudgetGuard>();
            _ = budgetGuard.Setup(g => g.TryConsumeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
            var jammingOrchestrator = new Mock<JammingToolsOrchestrator>();
            var liveViewService = new Mock<ILiveViewSessionService>();
            var auditLog = new Mock<ISecurityAuditLogger>();

            _ = services.AddSingleton(healthSource.Object);
            _ = services.AddSingleton(deviceRepo.Object);
            _ = services.AddSingleton(dataClient.Object);
            _ = services.AddSingleton(budgetGuard.Object);
            _ = services.AddSingleton(jammingOrchestrator.Object);
            _ = services.AddSingleton(liveViewService.Object);
            _ = services.AddSingleton(auditLog.Object);

            ServiceProvider provider = services.BuildServiceProvider();
            IServiceScopeFactory scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

            var service = new DeviceHealthSyncService(
                scopeFactory,
                new ForensicsConfiguration(),
                new AlwaysOnAcPower(),
                Mock.Of<ILogger<DeviceHealthSyncService>>(),
                elevatedTracker);

            _ = deviceRepo.Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync([device]);

            _ = healthSource.Setup(h => h.FetchHealthAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync([
                    new("ring-123", Connected: true, BatteryPercentage: 87m, Rssi: -60, WifiName: "HomeWifi", FirmwareVersion: "1.2.3")
                ]);

            // Jamming analysis returns incidents detected
            _ = jammingOrchestrator.Setup(j => j.AnalyzeJammingAsync(device.Id, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new JammingAnalysisReport { Success = true, NewlyDetectedCount = 2, DeviceId = device.Id });

            var activeSession = new LiveViewSession { Id = Guid.NewGuid(), DeviceId = device.Id, TriggerReason = LiveViewTriggerReason.JammingSuspected };
            _ = liveViewService.Setup(l => l.GetActiveSessionAsync(device.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(activeSession);

            var promotedSession = new LiveViewSession { Id = activeSession.Id, DeviceId = device.Id, IsSustained = true };
            _ = liveViewService.Setup(l => l.PromoteToSustainedAsync(activeSession.Id, It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(promotedSession);

            await service.RunElevatedTickAsync(CancellationToken.None);

            liveViewService.Verify(
                l => l.PromoteToSustainedAsync(activeSession.Id, It.IsAny<string>(), It.IsAny<CancellationToken>()),
                Times.Once);
            Assert.False(elevatedTracker.IsElevated(device.Id));
        }

        [Fact]
        public async Task RunElevatedTickAsync_NotConfirmed_ClearsElevatedWindow()
        {
            var elevatedTracker = new ElevatedPollingWindowTracker();
            var device = MakeDevice("ring-123");
            elevatedTracker.EnterElevated(device.Id, TimeSpan.FromSeconds(30));

            var services = new ServiceCollection();
            var healthSource = new Mock<IProviderHealthSource>();
            var deviceRepo = new Mock<IDeviceRepository>();
            var dataClient = new Mock<IVideoForensicsDataClient>();
            var budgetGuard = new Mock<IProviderApiBudgetGuard>();
            _ = budgetGuard.Setup(g => g.TryConsumeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
            var jammingOrchestrator = new Mock<JammingToolsOrchestrator>();
            var liveViewService = new Mock<ILiveViewSessionService>();
            var auditLog = new Mock<ISecurityAuditLogger>();

            _ = services.AddSingleton(healthSource.Object);
            _ = services.AddSingleton(deviceRepo.Object);
            _ = services.AddSingleton(dataClient.Object);
            _ = services.AddSingleton(budgetGuard.Object);
            _ = services.AddSingleton(jammingOrchestrator.Object);
            _ = services.AddSingleton(liveViewService.Object);
            _ = services.AddSingleton(auditLog.Object);

            ServiceProvider provider = services.BuildServiceProvider();
            IServiceScopeFactory scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

            var service = new DeviceHealthSyncService(
                scopeFactory,
                new ForensicsConfiguration(),
                new AlwaysOnAcPower(),
                Mock.Of<ILogger<DeviceHealthSyncService>>(),
                elevatedTracker);

            _ = deviceRepo.Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync([device]);

            _ = healthSource.Setup(h => h.FetchHealthAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync([
                    new("ring-123", Connected: true, BatteryPercentage: 87m, Rssi: -60, WifiName: "HomeWifi", FirmwareVersion: "1.2.3")
                ]);

            // Jamming analysis finds NO new incidents
            _ = jammingOrchestrator.Setup(j => j.AnalyzeJammingAsync(device.Id, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new JammingAnalysisReport { Success = true, NewlyDetectedCount = 0, DeviceId = device.Id });

            await service.RunElevatedTickAsync(CancellationToken.None);

            // Should clear the window regardless of result
            Assert.False(elevatedTracker.IsElevated(device.Id));
        }

        [Fact]
        public async Task RunElevatedTickAsync_BudgetExceeded_SkipsPollWithoutCallingProvider()
        {
            var elevatedTracker = new ElevatedPollingWindowTracker();
            var device = MakeDevice("ring-123");
            elevatedTracker.EnterElevated(device.Id, TimeSpan.FromSeconds(30));

            var services = new ServiceCollection();
            var healthSource = new Mock<IProviderHealthSource>();
            var deviceRepo = new Mock<IDeviceRepository>();
            var budgetGuard = new Mock<IProviderApiBudgetGuard>();
            _ = budgetGuard.Setup(g => g.TryConsumeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
            var jammingOrchestrator = new Mock<JammingToolsOrchestrator>();
            var liveViewService = new Mock<ILiveViewSessionService>();
            var auditLog = new Mock<ISecurityAuditLogger>();

            _ = services.AddSingleton(healthSource.Object);
            _ = services.AddSingleton(deviceRepo.Object);
            _ = services.AddSingleton(budgetGuard.Object);
            _ = services.AddSingleton(jammingOrchestrator.Object);
            _ = services.AddSingleton(liveViewService.Object);
            _ = services.AddSingleton(auditLog.Object);

            ServiceProvider provider = services.BuildServiceProvider();
            IServiceScopeFactory scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

            var service = new DeviceHealthSyncService(
                scopeFactory,
                new ForensicsConfiguration(),
                new AlwaysOnAcPower(),
                Mock.Of<ILogger<DeviceHealthSyncService>>(),
                elevatedTracker);

            _ = deviceRepo.Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync([device]);

            await service.RunElevatedTickAsync(CancellationToken.None);

            // Budget guard should prevent provider call
            healthSource.Verify(h => h.FetchHealthAsync(It.IsAny<CancellationToken>()), Times.Never);
        }
    }
}
