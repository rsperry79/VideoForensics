using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Moq;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Core.Contracts;
using VideoForensics.Hosting.BackgroundServices;

using Xunit;

namespace VideoForensics.Hosting.Tests
{
    public class CameraBitrateCalibrationServiceTests
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

        private static (
            CameraBitrateCalibrationService Service,
            Mock<ICameraBitrateBaselineRepository> BaselineRepo,
            Mock<ILiveViewSessionService> SessionService,
            Mock<IDeviceRepository> DeviceRepo,
            Mock<IProviderApiBudgetGuard> BudgetGuard
        ) CreateService(IForensicsConfiguration? config = null)
        {
            var baselineRepo = new Mock<ICameraBitrateBaselineRepository>();
            var sessionService = new Mock<ILiveViewSessionService>();
            var deviceRepo = new Mock<IDeviceRepository>();
            var budgetGuard = new Mock<IProviderApiBudgetGuard>();
            _ = budgetGuard.Setup(g => g.TryConsumeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

            var services = new ServiceCollection();
            _ = services.AddSingleton(baselineRepo.Object);
            _ = services.AddSingleton(sessionService.Object);
            _ = services.AddSingleton(deviceRepo.Object);
            _ = services.AddSingleton(budgetGuard.Object);
            ServiceProvider provider = services.BuildServiceProvider();

            IServiceScopeFactory scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

            var service = new CameraBitrateCalibrationService(
                scopeFactory,
                config ?? new ForensicsConfiguration(),
                Mock.Of<ILogger<CameraBitrateCalibrationService>>());

            return (service, baselineRepo, sessionService, deviceRepo, budgetGuard);
        }

        [Fact]
        public async Task RunOneTickAsync_CalibrationDisabled_DoesNothing()
        {
            var config = new ForensicsConfiguration { EnableBitrateCalibration = false };
            (CameraBitrateCalibrationService? service, Mock<ICameraBitrateBaselineRepository>? baselineRepo, _, _, _) = CreateService(config);

            await service.RunOneTickAsync(CancellationToken.None);

            baselineRepo.Verify(r => r.ListBucketsNeedingCalibrationAsync(It.IsAny<int>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task RunOneTickAsync_NoBucketsNeedCalibration_DoesNothing()
        {
            (CameraBitrateCalibrationService? service, Mock<ICameraBitrateBaselineRepository>? baselineRepo, Mock<ILiveViewSessionService>? sessionService, _, _) = CreateService();

            _ = baselineRepo.Setup(r => r.ListBucketsNeedingCalibrationAsync(It.IsAny<int>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync([]);

            await service.RunOneTickAsync(CancellationToken.None);

            sessionService.Verify(s => s.StartAsync(It.IsAny<Guid>(), It.IsAny<LiveViewTriggerReason>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task RunOneTickAsync_BucketNotForCurrentHour_SkipsDevice()
        {
            (CameraBitrateCalibrationService? service, Mock<ICameraBitrateBaselineRepository>? baselineRepo, Mock<ILiveViewSessionService>? sessionService, Mock<IDeviceRepository>? deviceRepo, _) = CreateService();

            Device device = MakeDevice("ring-123");
            _ = deviceRepo.Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync([device]);

            // Return a bucket for a different hour (not current)
            int differentHour = (DateTime.UtcNow.Hour + 1) % 24;
            _ = baselineRepo.Setup(r => r.ListBucketsNeedingCalibrationAsync(It.IsAny<int>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync([(device.Id, differentHour, false)]);

            await service.RunOneTickAsync(CancellationToken.None);

            sessionService.Verify(s => s.StartAsync(It.IsAny<Guid>(), It.IsAny<LiveViewTriggerReason>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task RunOneTickAsync_UndersampledCurrentHourBucket_StartsCalibrationSession()
        {
            (CameraBitrateCalibrationService? service, Mock<ICameraBitrateBaselineRepository>? baselineRepo, Mock<ILiveViewSessionService>? sessionService, Mock<IDeviceRepository>? deviceRepo, _) = CreateService();

            Device device = MakeDevice("ring-123");
            _ = deviceRepo.Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync([device]);

            // Return a bucket for current hour
            int currentHour = DateTime.UtcNow.Hour;
            bool isWeekend = DateTime.UtcNow.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
            _ = baselineRepo.Setup(r => r.ListBucketsNeedingCalibrationAsync(It.IsAny<int>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync([(device.Id, currentHour, isWeekend)]);

            _ = sessionService.Setup(s => s.GetActiveSessionAsync(device.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync((LiveViewSession?)null);

            var sessionResult = new LiveViewSession
            {
                Id = Guid.NewGuid(),
                DeviceId = device.Id,
                TriggerReason = LiveViewTriggerReason.Calibration,
                State = LiveViewSessionState.Starting,
                StartedAtUtc = DateTime.UtcNow,
                LastExtendedAtUtc = DateTime.UtcNow
            };
            _ = sessionService.Setup(s => s.StartAsync(device.Id, LiveViewTriggerReason.Calibration, null, It.IsAny<CancellationToken>()))
                .ReturnsAsync(sessionResult);

            await service.RunOneTickAsync(CancellationToken.None);

            sessionService.Verify(s => s.StartAsync(device.Id, LiveViewTriggerReason.Calibration, null, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task RunOneTickAsync_DeviceHasActiveSession_SkipsCalibration()
        {
            (CameraBitrateCalibrationService? service, Mock<ICameraBitrateBaselineRepository>? baselineRepo, Mock<ILiveViewSessionService>? sessionService, Mock<IDeviceRepository>? deviceRepo, _) = CreateService();

            Device device = MakeDevice("ring-123");
            _ = deviceRepo.Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync([device]);

            int currentHour = DateTime.UtcNow.Hour;
            bool isWeekend = DateTime.UtcNow.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
            _ = baselineRepo.Setup(r => r.ListBucketsNeedingCalibrationAsync(It.IsAny<int>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync([(device.Id, currentHour, isWeekend)]);

            // Device has an active session (manual or other reason)
            var existingSession = new LiveViewSession
            {
                Id = Guid.NewGuid(),
                DeviceId = device.Id,
                TriggerReason = LiveViewTriggerReason.Manual,
                State = LiveViewSessionState.Active,
                StartedAtUtc = DateTime.UtcNow,
                LastExtendedAtUtc = DateTime.UtcNow
            };
            _ = sessionService.Setup(s => s.GetActiveSessionAsync(device.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(existingSession);

            await service.RunOneTickAsync(CancellationToken.None);

            sessionService.Verify(s => s.StartAsync(It.IsAny<Guid>(), It.IsAny<LiveViewTriggerReason>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task RunOneTickAsync_BudgetExceeded_SkipsWithoutCallingProvider()
        {
            (CameraBitrateCalibrationService? service, Mock<ICameraBitrateBaselineRepository>? baselineRepo, Mock<ILiveViewSessionService>? sessionService, Mock<IDeviceRepository>? deviceRepo, Mock<IProviderApiBudgetGuard>? budgetGuard) = CreateService();

            Device device = MakeDevice("ring-123");
            _ = deviceRepo.Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync([device]);

            int currentHour = DateTime.UtcNow.Hour;
            bool isWeekend = DateTime.UtcNow.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
            _ = baselineRepo.Setup(r => r.ListBucketsNeedingCalibrationAsync(It.IsAny<int>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync([(device.Id, currentHour, isWeekend)]);

            _ = sessionService.Setup(s => s.GetActiveSessionAsync(device.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync((LiveViewSession?)null);

            // Budget exceeded
            _ = budgetGuard.Setup(g => g.TryConsumeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            await service.RunOneTickAsync(CancellationToken.None);

            sessionService.Verify(s => s.StartAsync(It.IsAny<Guid>(), It.IsAny<LiveViewTriggerReason>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task RunOneTickAsync_StartAsyncThrowsForOneDevice_ContinuesToNextDevice()
        {
            (CameraBitrateCalibrationService? service, Mock<ICameraBitrateBaselineRepository>? baselineRepo, Mock<ILiveViewSessionService>? sessionService, Mock<IDeviceRepository>? deviceRepo, _) = CreateService();

            Device device1 = MakeDevice("ring-123");
            Device device2 = MakeDevice("ring-456");
            _ = deviceRepo.Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync([device1, device2]);

            int currentHour = DateTime.UtcNow.Hour;
            bool isWeekend = DateTime.UtcNow.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
            _ = baselineRepo.Setup(r => r.ListBucketsNeedingCalibrationAsync(It.IsAny<int>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync([(device1.Id, currentHour, isWeekend), (device2.Id, currentHour, isWeekend)]);

            _ = sessionService.Setup(s => s.GetActiveSessionAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((LiveViewSession?)null);

            // First device throws
            _ = sessionService.Setup(s => s.StartAsync(device1.Id, LiveViewTriggerReason.Calibration, null, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("provider unavailable"));

            // Second device succeeds
            var sessionResult = new LiveViewSession
            {
                Id = Guid.NewGuid(),
                DeviceId = device2.Id,
                TriggerReason = LiveViewTriggerReason.Calibration,
                State = LiveViewSessionState.Starting,
                StartedAtUtc = DateTime.UtcNow,
                LastExtendedAtUtc = DateTime.UtcNow
            };
            _ = sessionService.Setup(s => s.StartAsync(device2.Id, LiveViewTriggerReason.Calibration, null, It.IsAny<CancellationToken>()))
                .ReturnsAsync(sessionResult);

            // Must not throw - one device's failure must not stop the whole tick
            await service.RunOneTickAsync(CancellationToken.None);

            // Verify second device's StartAsync was called despite first device's failure
            sessionService.Verify(s => s.StartAsync(device2.Id, LiveViewTriggerReason.Calibration, null, It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}
