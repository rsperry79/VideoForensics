using Microsoft.Extensions.Logging;

using Moq;

using VideoForensics.Core.Providers.Common.Contracts;
using VideoForensics.Core.Providers.Common.Services;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Core.Contracts;
using VideoForensics.Providers.Ring.Exceptions;
using VideoForensics.Providers.Ring.Services;

using Xunit;

namespace VideoForensics.Providers.Ring.Tests
{
    /// <summary>Per-account media request pacing and the rate-limit alert in RingMediaDownloadService.</summary>
    public class RingMediaRequestPacingTests
    {
        private static readonly Guid AccountA = Guid.Parse("11111111-1111-1111-1111-111111111111");
        private static readonly Guid AccountB = Guid.Parse("22222222-2222-2222-2222-222222222222");

        private static (RingMediaDownloadService Service, Mock<INotificationDispatcher> Dispatcher) CreateService(Func<DateTime>? utcNow = null)
        {
            var dispatcher = new Mock<INotificationDispatcher>();
            var service = new RingMediaDownloadService(
                new Mock<ILogger>().Object,
                new Mock<ISessionProvider>().Object,
                new Mock<IVideoForensicsDataClient>().Object,
                metadataTagger: null,
                notificationDispatcher: dispatcher.Object)
            {
                PacerClock = utcNow
            };
            return (service, dispatcher);
        }

        [Fact]
        public void GetPacerForAccount_DifferentAccounts_ReturnsIndependentPacers()
        {
            (RingMediaDownloadService service, _) = CreateService();

            MediaRequestPacer pacerA = service.GetPacerForAccount(AccountA);
            MediaRequestPacer pacerB = service.GetPacerForAccount(AccountB);

            Assert.NotSame(pacerA, pacerB);
            Assert.Same(pacerA, service.GetPacerForAccount(AccountA));
        }

        [Fact]
        public async Task RecordThrottledAsync_RateLimitOnAccountA_SlowsOnlyAccountA()
        {
            (RingMediaDownloadService service, _) = CreateService();
            MediaRequestPacer pacerA = service.GetPacerForAccount(AccountA);
            MediaRequestPacer pacerB = service.GetPacerForAccount(AccountB);
            TimeSpan baseInterval = pacerB.MinInterval;

            await service.RecordThrottledAsync(AccountA, new ThrottledException { RetryAfter = TimeSpan.FromSeconds(5) }, CancellationToken.None);

            Assert.Equal(baseInterval + TimeSpan.FromSeconds(1), pacerA.MinInterval);
            Assert.Equal(baseInterval, pacerB.MinInterval);
        }

        [Fact]
        public async Task RecordThrottledAsync_TwoRateLimitsInOneIncident_RaisesOneAlert()
        {
            (RingMediaDownloadService service, Mock<INotificationDispatcher> dispatcher) = CreateService();

            await service.RecordThrottledAsync(AccountA, new ThrottledException(), CancellationToken.None);
            await service.RecordThrottledAsync(AccountA, new ThrottledException(), CancellationToken.None);

            dispatcher.Verify(d => d.DispatchAsync(It.IsAny<NotificationEvent>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task RecordThrottledAsync_TwoRateLimitsInOneIncident_AddsOneSecondOfSpacing()
        {
            (RingMediaDownloadService service, _) = CreateService();
            MediaRequestPacer pacerA = service.GetPacerForAccount(AccountA);
            TimeSpan baseInterval = pacerA.MinInterval;

            await service.RecordThrottledAsync(AccountA, new ThrottledException(), CancellationToken.None);
            await service.RecordThrottledAsync(AccountA, new ThrottledException(), CancellationToken.None);

            Assert.Equal(baseInterval + TimeSpan.FromSeconds(1), pacerA.MinInterval);
        }

        [Fact]
        public async Task RecordThrottledAsync_RateLimitAfterSuccess_RaisesSecondAlert()
        {
            (RingMediaDownloadService service, Mock<INotificationDispatcher> dispatcher) = CreateService();
            MediaRequestPacer pacerA = service.GetPacerForAccount(AccountA);

            await service.RecordThrottledAsync(AccountA, new ThrottledException(), CancellationToken.None);
            pacerA.MarkRequestSucceeded();
            await service.RecordThrottledAsync(AccountA, new ThrottledException(), CancellationToken.None);

            dispatcher.Verify(d => d.DispatchAsync(It.IsAny<NotificationEvent>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        }

        [Fact]
        public async Task RecordThrottledAsync_RateLimit_AlertNamesProviderAccountNotGuidOrExceptionMessage()
        {
            (RingMediaDownloadService service, Mock<INotificationDispatcher> dispatcher) = CreateService();
            var captured = new List<NotificationEvent>();
            _ = dispatcher.Setup(d => d.DispatchAsync(It.IsAny<NotificationEvent>(), It.IsAny<CancellationToken>()))
                .Callback<NotificationEvent, CancellationToken>((e, _) => captured.Add(e))
                .Returns(Task.CompletedTask);

            await service.RecordThrottledAsync(AccountA, new ThrottledException("secret-detail-xyz"), CancellationToken.None);

            NotificationEvent alert = Assert.Single(captured);
            Assert.Contains("Ring account", alert.Details);
            Assert.DoesNotContain(AccountA.ToString(), alert.Details);
            Assert.DoesNotContain("secret-detail-xyz", alert.Details);
        }

        [Fact]
        public async Task RecordThrottledAsync_HardBan_DoesNotChangeSpacing()
        {
            (RingMediaDownloadService service, _) = CreateService();
            MediaRequestPacer pacerA = service.GetPacerForAccount(AccountA);
            TimeSpan baseInterval = pacerA.MinInterval;

            await service.RecordThrottledAsync(AccountA, new ThrottledException("ban", isHardBan: true), CancellationToken.None);

            Assert.Equal(baseInterval, pacerA.MinInterval);
            Assert.Equal(TimeSpan.Zero, pacerA.RateLimitPenalty);
        }

        [Fact]
        public async Task RecordThrottledAsync_HardBan_RaisesOneHardBanAlert()
        {
            (RingMediaDownloadService service, Mock<INotificationDispatcher> dispatcher) = CreateService();

            await service.RecordThrottledAsync(AccountA, new ThrottledException("ban", isHardBan: true), CancellationToken.None);

            dispatcher.Verify(d => d.DispatchAsync(It.IsAny<NotificationEvent>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task RecordThrottledAsync_HardBanRepeatedInsideWindow_RaisesOneAlert()
        {
            (RingMediaDownloadService service, Mock<INotificationDispatcher> dispatcher) = CreateService();

            await service.RecordThrottledAsync(AccountA, new ThrottledException("ban", isHardBan: true), CancellationToken.None);
            await service.RecordThrottledAsync(AccountA, new ThrottledException("ban", isHardBan: true), CancellationToken.None);

            dispatcher.Verify(d => d.DispatchAsync(It.IsAny<NotificationEvent>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task RecordThrottledAsync_RateLimitAfterHardBanBeforeSuccess_AddsNoSpacingAndNoSecondAlert()
        {
            (RingMediaDownloadService service, Mock<INotificationDispatcher> dispatcher) = CreateService();
            MediaRequestPacer pacerA = service.GetPacerForAccount(AccountA);
            TimeSpan baseInterval = pacerA.MinInterval;

            await service.RecordThrottledAsync(AccountA, new ThrottledException("ban", isHardBan: true), CancellationToken.None);
            await service.RecordThrottledAsync(AccountA, new ThrottledException(), CancellationToken.None);

            Assert.Equal(baseInterval, pacerA.MinInterval);
            dispatcher.Verify(d => d.DispatchAsync(It.IsAny<NotificationEvent>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task RecordThrottledAsync_HardBanAfterNormalRateLimitInSameIncident_RaisesHardBanAlert()
        {
            (RingMediaDownloadService service, Mock<INotificationDispatcher> dispatcher) = CreateService();

            await service.RecordThrottledAsync(AccountA, new ThrottledException(), CancellationToken.None);
            await service.RecordThrottledAsync(AccountA, new ThrottledException("ban", isHardBan: true), CancellationToken.None);

            dispatcher.Verify(d => d.DispatchAsync(It.IsAny<NotificationEvent>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        }

        [Fact]
        public async Task RecordThrottledAsync_HardBan_AlertNamesRingAccountAndHardBanWithoutGuidOrExceptionMessage()
        {
            (RingMediaDownloadService service, Mock<INotificationDispatcher> dispatcher) = CreateService();
            var captured = new List<NotificationEvent>();
            _ = dispatcher.Setup(d => d.DispatchAsync(It.IsAny<NotificationEvent>(), It.IsAny<CancellationToken>()))
                .Callback<NotificationEvent, CancellationToken>((e, _) => captured.Add(e))
                .Returns(Task.CompletedTask);

            await service.RecordThrottledAsync(AccountA, new ThrottledException("secret-ban-detail", isHardBan: true), CancellationToken.None);

            NotificationEvent alert = Assert.Single(captured);
            Assert.Equal("ProviderRateLimited", alert.EventType);
            Assert.Contains("Ring account", alert.Details);
            Assert.Contains("hard ban", alert.Details);
            Assert.Contains("15 minutes", alert.Details);
            Assert.DoesNotContain(AccountA.ToString(), alert.Details);
            Assert.DoesNotContain("secret-ban-detail", alert.Details);
        }

        [Fact]
        public async Task RecordThrottledAsync_SecondHardBanBeforeSuccessAfterWindowLapses_AlertSaysThirtyMinutes()
        {
            DateTime now = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            (RingMediaDownloadService service, Mock<INotificationDispatcher> dispatcher) = CreateService(() => now);
            var captured = new List<NotificationEvent>();
            _ = dispatcher.Setup(d => d.DispatchAsync(It.IsAny<NotificationEvent>(), It.IsAny<CancellationToken>()))
                .Callback<NotificationEvent, CancellationToken>((e, _) => captured.Add(e))
                .Returns(Task.CompletedTask);

            await service.RecordThrottledAsync(AccountA, new ThrottledException("ban", isHardBan: true), CancellationToken.None);
            // The first 15-minute window has lapsed and Ring is still refusing: no successful request in between.
            now = now.AddMinutes(16);
            await service.RecordThrottledAsync(AccountA, new ThrottledException("ban", isHardBan: true), CancellationToken.None);

            Assert.Equal(2, captured.Count);
            Assert.Contains("15 minutes", captured[0].Details);
            Assert.Contains("30 minutes", captured[1].Details);
        }

        [Fact]
        public async Task RecordMediaSuccess_AfterRateLimit_EndsIncidentSoNextRateLimitAlerts()
        {
            (RingMediaDownloadService service, Mock<INotificationDispatcher> dispatcher) = CreateService();
            MediaRequestPacer pacerA = service.GetPacerForAccount(AccountA);
            await service.RecordThrottledAsync(AccountA, new ThrottledException(), CancellationToken.None);

            service.RecordMediaSuccess(AccountA);

            Assert.False(pacerA.IsInRateLimitIncident);
            await service.RecordThrottledAsync(AccountA, new ThrottledException(), CancellationToken.None);
            dispatcher.Verify(d => d.DispatchAsync(It.IsAny<NotificationEvent>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        }

        [Fact]
        public async Task SetRequestPacing_AfterRateLimit_KeepsRateLimitPenalty()
        {
            (RingMediaDownloadService service, _) = CreateService();
            MediaRequestPacer pacerA = service.GetPacerForAccount(AccountA);
            await service.RecordThrottledAsync(AccountA, new ThrottledException(), CancellationToken.None);

            service.SetRequestPacing(1, TimeSpan.FromSeconds(2));

            Assert.Equal(TimeSpan.FromSeconds(3), pacerA.MinInterval);
        }

        private static (RingMediaDownloadService Service, Mock<ISyncScheduleRepository> Repository) CreateServiceWithScheduleRepository()
        {
            var repository = new Mock<ISyncScheduleRepository>();
            var service = new RingMediaDownloadService(
                new Mock<ILogger>().Object,
                new Mock<ISessionProvider>().Object,
                new Mock<IVideoForensicsDataClient>().Object,
                metadataTagger: null,
                notificationDispatcher: new Mock<INotificationDispatcher>().Object,
                syncScheduleRepository: repository.Object);
            return (service, repository);
        }

        [Fact]
        public async Task RecordThrottledAsync_NewIncidentWithBaseSpacing_SavesBasePlusOneSecond()
        {
            (RingMediaDownloadService service, Mock<ISyncScheduleRepository> repository) = CreateServiceWithScheduleRepository();
            service.SetRequestPacing(1, TimeSpan.FromSeconds(2));

            await service.RecordThrottledAsync(AccountA, new ThrottledException(), CancellationToken.None);

            repository.Verify(r => r.SetSnapshotIntervalSecondsAsync(AccountA, 3, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task RecordThrottledAsync_RepeatInsideSameIncident_SavesOnlyOnce()
        {
            (RingMediaDownloadService service, Mock<ISyncScheduleRepository> repository) = CreateServiceWithScheduleRepository();
            service.SetRequestPacing(1, TimeSpan.FromSeconds(2));

            await service.RecordThrottledAsync(AccountA, new ThrottledException(), CancellationToken.None);
            await service.RecordThrottledAsync(AccountA, new ThrottledException(), CancellationToken.None);

            repository.Verify(r => r.SetSnapshotIntervalSecondsAsync(AccountA, 3, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task RecordThrottledAsync_NewIncidentWithStoredInterval_SavesStoredPlusOneSecond()
        {
            (RingMediaDownloadService service, Mock<ISyncScheduleRepository> repository) = CreateServiceWithScheduleRepository();
            _ = repository.Setup(r => r.GetByProviderAccountIdAsync(AccountA, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new SyncSchedule { ProviderAccountId = AccountA, SnapshotIntervalSeconds = 5 });
            service.SetRequestPacing(1, TimeSpan.FromSeconds(2));

            await service.RecordThrottledAsync(AccountA, new ThrottledException(), CancellationToken.None);

            repository.Verify(r => r.SetSnapshotIntervalSecondsAsync(AccountA, 6, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task RecordThrottledAsync_HardBan_DoesNotSaveSnapshotInterval()
        {
            (RingMediaDownloadService service, Mock<ISyncScheduleRepository> repository) = CreateServiceWithScheduleRepository();

            await service.RecordThrottledAsync(AccountA, new ThrottledException("ban", isHardBan: true), CancellationToken.None);

            repository.Verify(r => r.SetSnapshotIntervalSecondsAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        /// <summary>Collects log entries so tests can assert a failure was logged without a Moq expression over generic Log.</summary>
        private sealed class CapturingLogger : ILogger
        {
            public List<(LogLevel Level, string Message, Exception? Exception)> Entries { get; } = [];

            public IDisposable? BeginScope<TState>(TState state) where TState : notnull
            {
                return null;
            }

            public bool IsEnabled(LogLevel logLevel)
            {
                return true;
            }

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                Entries.Add((logLevel, formatter(state, exception), exception));
            }
        }

        private static (RingMediaDownloadService Service, Mock<ISyncScheduleRepository> Repository, CapturingLogger Logger) CreateServiceForStoredInterval(
            int? storedSeconds, Exception? loadFailure = null)
        {
            var repository = new Mock<ISyncScheduleRepository>();
            var logger = new CapturingLogger();
            _ = loadFailure != null
                ? repository.Setup(r => r.GetByProviderAccountIdAsync(AccountA, It.IsAny<CancellationToken>())).ThrowsAsync(loadFailure)
                : repository.Setup(r => r.GetByProviderAccountIdAsync(AccountA, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new SyncSchedule { ProviderAccountId = AccountA, SnapshotIntervalSeconds = storedSeconds });

            var service = new RingMediaDownloadService(
                logger,
                new Mock<ISessionProvider>().Object,
                new Mock<IVideoForensicsDataClient>().Object,
                metadataTagger: null,
                notificationDispatcher: new Mock<INotificationDispatcher>().Object,
                syncScheduleRepository: repository.Object);
            service.SetActiveProviderAccountId(AccountA);
            service.SetRequestPacing(1, TimeSpan.FromSeconds(2));
            return (service, repository, logger);
        }

        // Session is null in these tests, so the download returns before any Ring call; the stored interval loads first in the entry point.
        private static Task<DownloadResult> DownloadOnceAsync(RingMediaDownloadService service)
        {
            return service.DownloadVideosAsync("device-1", Path.GetTempPath(), DateTime.UtcNow.AddDays(-1), DateTime.UtcNow, cancellationToken: CancellationToken.None);
        }

        [Fact]
        public async Task DownloadVideosAsync_StoredIntervalAboveBase_RaisesPacerToStoredInterval()
        {
            (RingMediaDownloadService service, _, _) = CreateServiceForStoredInterval(storedSeconds: 3);

            _ = await DownloadOnceAsync(service);

            Assert.Equal(TimeSpan.FromSeconds(3), service.GetPacerForAccount(AccountA).MinInterval);
        }

        [Fact]
        public async Task DownloadVideosAsync_StoredIntervalBelowBase_KeepsBaseSpacing()
        {
            (RingMediaDownloadService service, _, _) = CreateServiceForStoredInterval(storedSeconds: 1);

            _ = await DownloadOnceAsync(service);

            Assert.Equal(TimeSpan.FromSeconds(2), service.GetPacerForAccount(AccountA).MinInterval);
        }

        [Fact]
        public async Task DownloadVideosAsync_NoStoredInterval_KeepsBaseSpacing()
        {
            (RingMediaDownloadService service, _, _) = CreateServiceForStoredInterval(storedSeconds: null);

            _ = await DownloadOnceAsync(service);

            Assert.Equal(TimeSpan.FromSeconds(2), service.GetPacerForAccount(AccountA).MinInterval);
        }

        [Fact]
        public async Task DownloadVideosAsync_RepositoryThrows_KeepsBaseSpacingLogsAndDoesNotThrow()
        {
            (RingMediaDownloadService service, _, CapturingLogger logger) =
                CreateServiceForStoredInterval(storedSeconds: null, loadFailure: new InvalidOperationException("db-detail-xyz"));

            _ = await DownloadOnceAsync(service);

            Assert.Equal(TimeSpan.FromSeconds(2), service.GetPacerForAccount(AccountA).MinInterval);
            (LogLevel level, string message, Exception? exception) = Assert.Single(logger.Entries, e => e.Message.Contains("snapshot interval"));
            Assert.Equal(LogLevel.Error, level);
            Assert.Contains("Ring account", message);
            Assert.DoesNotContain(AccountA.ToString(), message);
            _ = Assert.IsType<InvalidOperationException>(exception);
        }

        [Fact]
        public async Task DownloadVideosAsync_TwoDownloads_LoadsStoredIntervalOnce()
        {
            (RingMediaDownloadService service, Mock<ISyncScheduleRepository> repository, _) = CreateServiceForStoredInterval(storedSeconds: 3);

            _ = await DownloadOnceAsync(service);
            _ = await DownloadOnceAsync(service);

            repository.Verify(r => r.GetByProviderAccountIdAsync(AccountA, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task SetRequestPacing_AfterStoredIntervalLoaded_KeepsStoredFloor()
        {
            (RingMediaDownloadService service, _, _) = CreateServiceForStoredInterval(storedSeconds: 3);
            _ = await DownloadOnceAsync(service);

            // Each run calls SetRequestPacing; the restored spacing must survive it.
            service.SetRequestPacing(1, TimeSpan.FromSeconds(2));

            Assert.Equal(TimeSpan.FromSeconds(3), service.GetPacerForAccount(AccountA).MinInterval);
        }

        [Fact]
        public async Task SetRequestPacing_AfterIncidentFollowingStoredInterval_KeepsRaisedSpacing()
        {
            (RingMediaDownloadService service, _, _) = CreateServiceForStoredInterval(storedSeconds: 3);
            _ = await DownloadOnceAsync(service);
            await service.RecordThrottledAsync(AccountA, new ThrottledException(), CancellationToken.None);

            // The incident saved 4 seconds; the next run's SetRequestPacing must not drop it back to 3.
            service.SetRequestPacing(1, TimeSpan.FromSeconds(2));

            Assert.Equal(TimeSpan.FromSeconds(4), service.GetPacerForAccount(AccountA).MinInterval);
        }
    }
}
