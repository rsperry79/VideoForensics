using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Moq;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Hosting.BackgroundServices;
using VideoForensics.Providers.Common.Contracts;

using Xunit;

namespace VideoForensics.Hosting.Tests
{
    public class LiveViewIdleTimeoutServiceTests
    {
        private static LiveViewSession MakeLiveViewSession(
            Guid? id = null,
            Guid? deviceId = null,
            Guid? operatorId = null,
            LiveViewSessionState state = LiveViewSessionState.Active,
            DateTime? lastExtendedAtUtc = null,
            DateTime? sustainedSinceUtc = null,
            bool isSustained = false)
        {
            return new()
            {
                Id = id ?? Guid.NewGuid(),
                DeviceId = deviceId ?? Guid.NewGuid(),
                OperatorId = operatorId ?? Guid.NewGuid(),
                State = state,
                TriggerReason = LiveViewTriggerReason.Manual,
                StartedAtUtc = DateTime.UtcNow.AddMinutes(-10),
                LastExtendedAtUtc = lastExtendedAtUtc ?? DateTime.UtcNow,
                IsSustained = isSustained,
                SustainedSinceUtc = sustainedSinceUtc,
                PromotionReason = isSustained ? "Test promotion" : null
            };
        }

        private static (LiveViewIdleTimeoutService Service, Mock<ILiveViewSessionRepository> SessionRepo, Mock<ILiveViewSessionService> SessionService, Mock<INotificationDispatcher> NotificationDispatcher)
            CreateService(IForensicsConfiguration? config = null)
        {
            var sessionRepo = new Mock<ILiveViewSessionRepository>();
            var sessionService = new Mock<ILiveViewSessionService>();
            var notificationDispatcher = new Mock<INotificationDispatcher>();

            var services = new ServiceCollection();
            _ = services.AddSingleton(sessionRepo.Object);
            _ = services.AddSingleton(sessionService.Object);
            _ = services.AddSingleton(notificationDispatcher.Object);
            ServiceProvider provider = services.BuildServiceProvider();

            IServiceScopeFactory scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

            var service = new LiveViewIdleTimeoutService(
                scopeFactory,
                config ?? new ForensicsConfiguration(),
                Mock.Of<ILogger<LiveViewIdleTimeoutService>>());

            return (service, sessionRepo, sessionService, notificationDispatcher);
        }

        [Fact]
        public async Task RunOneTickAsync_ActiveSessionPastIdleTimeout_StopsSession()
        {
            (LiveViewIdleTimeoutService? service, Mock<ILiveViewSessionRepository>? sessionRepo, Mock<ILiveViewSessionService>? sessionService, _) = CreateService(
                new ForensicsConfiguration { LiveViewIdleTimeoutMinutes = 5 });

            var sessionId = Guid.NewGuid();
            var deviceId = Guid.NewGuid();
            var operatorId = Guid.NewGuid();

            // Session last extended 10 minutes ago - past the 5-minute timeout
            var session = MakeLiveViewSession(
                id: sessionId,
                deviceId: deviceId,
                operatorId: operatorId,
                state: LiveViewSessionState.Active,
                lastExtendedAtUtc: DateTime.UtcNow.AddMinutes(-10),
                isSustained: false);

            _ = sessionRepo.Setup(r => r.ListActiveAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync([session]);

            await service.RunOneTickAsync(CancellationToken.None);

            sessionService.Verify(s => s.StopAsync(sessionId, "IdleTimeout", It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task RunOneTickAsync_ActiveSessionWithinTimeout_DoesNotStop()
        {
            (LiveViewIdleTimeoutService? service, Mock<ILiveViewSessionRepository>? sessionRepo, Mock<ILiveViewSessionService>? sessionService, _) = CreateService(
                new ForensicsConfiguration { LiveViewIdleTimeoutMinutes = 5 });

            var sessionId = Guid.NewGuid();

            // Session last extended 2 minutes ago - within the 5-minute timeout
            var session = MakeLiveViewSession(
                id: sessionId,
                state: LiveViewSessionState.Active,
                lastExtendedAtUtc: DateTime.UtcNow.AddMinutes(-2),
                isSustained: false);

            _ = sessionRepo.Setup(r => r.ListActiveAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync([session]);

            await service.RunOneTickAsync(CancellationToken.None);

            sessionService.Verify(s => s.StopAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task RunOneTickAsync_SustainedSession_IgnoredByIdleCheck()
        {
            (LiveViewIdleTimeoutService? service, Mock<ILiveViewSessionRepository>? sessionRepo, Mock<ILiveViewSessionService>? sessionService, _) = CreateService(
                new ForensicsConfiguration { LiveViewIdleTimeoutMinutes = 5, SustainedModeMaxDurationMinutes = 240 });

            var sessionId = Guid.NewGuid();

            // Sustained session last extended 10 minutes ago - past the idle timeout
            // but should be ignored because it's sustained (not Active state)
            var session = MakeLiveViewSession(
                id: sessionId,
                state: LiveViewSessionState.Sustained,
                lastExtendedAtUtc: DateTime.UtcNow.AddMinutes(-10),
                sustainedSinceUtc: DateTime.UtcNow.AddMinutes(-5),
                isSustained: true);

            _ = sessionRepo.Setup(r => r.ListActiveAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync([session]);

            await service.RunOneTickAsync(CancellationToken.None);

            // Should NOT stop due to idle timeout (sustained sessions are exempt)
            sessionService.Verify(s => s.StopAsync(sessionId, "IdleTimeout", It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task RunOneTickAsync_SustainedSessionPastSafetyValve_StopsWithSafetyValveReasonAndDispatchesNotification()
        {
            (LiveViewIdleTimeoutService? service, Mock<ILiveViewSessionRepository>? sessionRepo, Mock<ILiveViewSessionService>? sessionService, Mock<INotificationDispatcher>? notificationDispatcher) = CreateService(
                new ForensicsConfiguration { SustainedModeMaxDurationMinutes = 60 });

            var sessionId = Guid.NewGuid();
            var deviceId = Guid.NewGuid();
            var operatorId = Guid.NewGuid();

            // Sustained session for 90 minutes - past the 60-minute safety valve
            var sustainedSince = DateTime.UtcNow.AddMinutes(-90);
            var session = MakeLiveViewSession(
                id: sessionId,
                deviceId: deviceId,
                operatorId: operatorId,
                state: LiveViewSessionState.Sustained,
                sustainedSinceUtc: sustainedSince,
                isSustained: true);

            _ = sessionRepo.Setup(r => r.ListActiveAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync([session]);

            await service.RunOneTickAsync(CancellationToken.None);

            sessionService.Verify(s => s.StopAsync(sessionId, "SafetyValve", It.IsAny<CancellationToken>()), Times.Once);
            notificationDispatcher.Verify(d => d.DispatchAsync(It.IsAny<NotificationEvent>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task RunOneTickAsync_NotificationEventHasCorrectFields()
        {
            (LiveViewIdleTimeoutService? service, Mock<ILiveViewSessionRepository>? sessionRepo, Mock<ILiveViewSessionService>? sessionService, Mock<INotificationDispatcher>? notificationDispatcher) = CreateService(
                new ForensicsConfiguration { SustainedModeMaxDurationMinutes = 60 });

            var sessionId = Guid.NewGuid();
            var deviceId = Guid.NewGuid();
            var operatorId = Guid.NewGuid();

            var sustainedSince = DateTime.UtcNow.AddMinutes(-90);
            var session = MakeLiveViewSession(
                id: sessionId,
                deviceId: deviceId,
                operatorId: operatorId,
                state: LiveViewSessionState.Sustained,
                sustainedSinceUtc: sustainedSince,
                isSustained: true);

            _ = sessionRepo.Setup(r => r.ListActiveAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync([session]);

            NotificationEvent? capturedEvent = null;
            _ = notificationDispatcher.Setup(d => d.DispatchAsync(It.IsAny<NotificationEvent>(), It.IsAny<CancellationToken>()))
                .Callback<NotificationEvent, CancellationToken>((evt, _) => capturedEvent = evt)
                .Returns(Task.CompletedTask);

            await service.RunOneTickAsync(CancellationToken.None);

            Assert.NotNull(capturedEvent);
            Assert.Equal("LiveViewSessionSafetyValveTriggered", capturedEvent!.EventType);
            Assert.Equal(operatorId, capturedEvent.OperatorId);
            Assert.Equal(deviceId, capturedEvent.PairedDeviceId);
            Assert.Equal(NotificationAudience.AdminsOnly, capturedEvent.Audience);
            Assert.Equal(NoticeSeverity.Alert, capturedEvent.Severity);
            Assert.Null(capturedEvent.SourceIp);
            Assert.Contains("Sustained live view session", capturedEvent.Details);
            Assert.Contains("60", capturedEvent.Details); // Minutes value
        }

        [Fact]
        public async Task RunOneTickAsync_SafetyValveDisabledZero_NeverStopsSustainedSession()
        {
            (LiveViewIdleTimeoutService? service, Mock<ILiveViewSessionRepository>? sessionRepo, Mock<ILiveViewSessionService>? sessionService, _) = CreateService(
                new ForensicsConfiguration { SustainedModeMaxDurationMinutes = 0 }); // 0 = unlimited

            var sessionId = Guid.NewGuid();

            // Sustained session for 1000 minutes (unlimited mode should never stop)
            var session = MakeLiveViewSession(
                id: sessionId,
                state: LiveViewSessionState.Sustained,
                sustainedSinceUtc: DateTime.UtcNow.AddMinutes(-1000),
                isSustained: true);

            _ = sessionRepo.Setup(r => r.ListActiveAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync([session]);

            await service.RunOneTickAsync(CancellationToken.None);

            // With SustainedModeMaxDurationMinutes = 0, should never stop
            sessionService.Verify(s => s.StopAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task RunOneTickAsync_NoActiveSessions_DoesNothing()
        {
            (LiveViewIdleTimeoutService? service, Mock<ILiveViewSessionRepository>? sessionRepo, Mock<ILiveViewSessionService>? sessionService, Mock<INotificationDispatcher>? notificationDispatcher) = CreateService();

            _ = sessionRepo.Setup(r => r.ListActiveAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync([]);

            await service.RunOneTickAsync(CancellationToken.None);

            sessionService.Verify(s => s.StopAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
            notificationDispatcher.Verify(d => d.DispatchAsync(It.IsAny<NotificationEvent>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task RunOneTickAsync_StopThrowsForOneSession_ContinuesToNextSession()
        {
            (LiveViewIdleTimeoutService? service, Mock<ILiveViewSessionRepository>? sessionRepo, Mock<ILiveViewSessionService>? sessionService, _) = CreateService(
                new ForensicsConfiguration { LiveViewIdleTimeoutMinutes = 5 });

            var session1Id = Guid.NewGuid();
            var session2Id = Guid.NewGuid();

            var session1 = MakeLiveViewSession(
                id: session1Id,
                state: LiveViewSessionState.Active,
                lastExtendedAtUtc: DateTime.UtcNow.AddMinutes(-10),
                isSustained: false);

            var session2 = MakeLiveViewSession(
                id: session2Id,
                state: LiveViewSessionState.Active,
                lastExtendedAtUtc: DateTime.UtcNow.AddMinutes(-10),
                isSustained: false);

            _ = sessionRepo.Setup(r => r.ListActiveAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync([session1, session2]);

            // First stop throws, second should still be attempted
            var callCount = 0;
            _ = sessionService.Setup(s => s.StopAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Callback(() =>
                {
                    callCount++;
                    if (callCount == 1)
                    {
                        throw new InvalidOperationException("Stop failed for session 1");
                    }
                })
                .Returns(Task.CompletedTask);

            // Must not throw - one session's failure must not stop the whole tick
            await service.RunOneTickAsync(CancellationToken.None);

            // Both sessions should have been attempted
            sessionService.Verify(s => s.StopAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        }
    }
}
