using System.Collections.Concurrent;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Moq;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Hosting.BackgroundServices;
using VideoForensics.Providers.Common.Contracts;

using Xunit;

using Device = VideoForensics.Data.Common.Entities.Device;
using Location = VideoForensics.Data.Common.Entities.Location;

namespace VideoForensics.Hosting.Tests
{
    public class ScheduledSyncServiceTests
    {
        private static readonly DateTime Now = new(2026, 5, 4, 12, 0, 0, DateTimeKind.Utc);

        private sealed class ManualTimeProvider(DateTime utcNow) : TimeProvider
        {
            public override DateTimeOffset GetUtcNow()
            {
                return new DateTimeOffset(utcNow, TimeSpan.Zero);
            }
        }

        private sealed class CapturingLoggerProvider : ILoggerProvider
        {
            public ConcurrentQueue<string> Lines { get; } = new();

            public ILogger CreateLogger(string categoryName)
            {
                return new CapturingLogger(this);
            }

            public void Dispose()
            {
            }

            private sealed class CapturingLogger(CapturingLoggerProvider owner) : ILogger
            {
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
                    owner.Lines.Enqueue($"{logLevel}: {formatter(state, exception)}");
                }
            }
        }

        private sealed class Harness
        {
            public Mock<ISyncScheduleRepository> Schedules { get; } = new();
            public Mock<IProviderAccountRepository> Accounts { get; } = new();
            public Mock<IDeviceRepository> Devices { get; } = new();
            public Mock<ILocationRepository> Locations { get; } = new();
            public Mock<IProviderApiBudgetGuard> Budget { get; } = new();
            public Dictionary<string, Mock<IEventAndConfigService>> Events { get; } = new();
            public Dictionary<string, Mock<IProviderAuthService>> Auth { get; } = new();
            public CapturingLoggerProvider Logs { get; } = new();
            public string[] EnabledProviders { get; set; } = ["Ring"];

            private readonly ServiceCollection _services = new();
            private readonly List<SyncSchedule> _schedules = [];

            public Harness()
            {
                _ = Budget.Setup(b => b.TryConsumeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
                _ = Schedules.Setup(r => r.ListEnabledAsync(It.IsAny<CancellationToken>())).ReturnsAsync(() => _schedules);
            }

            public (SyncSchedule Schedule, ProviderAccount Account) AddAccount(
                string provider,
                bool active = true,
                DateTime? eventNext = null,
                DateTime? eventLast = null,
                int intervalMinutes = 30,
                bool registerKeyed = true,
                bool authResult = true)
            {
                var account = new ProviderAccount
                {
                    Id = Guid.NewGuid(),
                    ProviderName = provider,
                    IsActive = active,
                    LastSuccessfulAuthUtc = Now.AddDays(-1)
                };
                var schedule = new SyncSchedule
                {
                    Id = Guid.NewGuid(),
                    ProviderAccountId = account.Id,
                    IsEnabled = true,
                    EventPollIntervalMinutes = intervalMinutes,
                    SnapshotRssiIntervalMinutes = 60,
                    EventNextRunUtc = eventNext,
                    EventLastRunUtc = eventLast
                };
                _schedules.Add(schedule);
                _ = Accounts.Setup(r => r.GetAsync(account.Id, It.IsAny<CancellationToken>())).ReturnsAsync(account);

                var location = new Location { Id = Guid.NewGuid(), ProviderLocationId = "loc-" + provider, Name = "Home" };
                var device = new Device { Id = Guid.NewGuid(), LocationId = location.Id, ProviderDeviceId = "dev-" + account.Id, Name = "Cam", Type = "camera" };
#pragma warning disable CS0618
                _ = Locations.Setup(r => r.GetByProviderAccountIdAsync(account.Id, It.IsAny<CancellationToken>())).ReturnsAsync([location]);
#pragma warning restore CS0618
                _ = Devices.Setup(r => r.GetByLocationIdAsync(location.Id, It.IsAny<CancellationToken>())).ReturnsAsync([device]);

                if (registerKeyed)
                {
                    if (!Events.ContainsKey(provider))
                    {
                        Events[provider] = new Mock<IEventAndConfigService>();
                        Auth[provider] = new Mock<IProviderAuthService>();
                    }

                    _ = Auth[provider].Setup(a => a.RestoreFromSavedCredentialsAsync(account.Id, It.IsAny<CancellationToken>())).ReturnsAsync(authResult);
                }

                return (schedule, account);
            }

            public ScheduledSyncService Build()
            {
                _ = _services.AddSingleton(Schedules.Object);
                _ = _services.AddSingleton(Accounts.Object);
                _ = _services.AddSingleton(Devices.Object);
                _ = _services.AddSingleton(Locations.Object);
                _ = _services.AddSingleton(Budget.Object);
                _ = _services.AddLogging(b => b.SetMinimumLevel(LogLevel.Trace).AddProvider(Logs));
                foreach ((string provider, Mock<IEventAndConfigService> events) in Events)
                {
                    _ = _services.AddKeyedSingleton(provider, events.Object);
                    _ = _services.AddKeyedSingleton(provider, Auth[provider].Object);
                }

                ServiceProvider sp = _services.BuildServiceProvider();
                var options = new Mock<IOptionsMonitor<ScheduledTasksOptions>>();
                _ = options.Setup(o => o.CurrentValue).Returns(() => new ScheduledTasksOptions { EnabledProviders = EnabledProviders });
                return new ScheduledSyncService(
                    sp.GetRequiredService<IServiceScopeFactory>(),
                    options.Object,
                    sp.GetRequiredService<ILogger<ScheduledSyncService>>(),
                    new ManualTimeProvider(Now));
            }

            public void VerifyNotPulled(string provider)
            {
                Events[provider].Verify(e => e.GetEventsAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
            }

            public void VerifyNothingRecorded()
            {
                Schedules.Verify(r => r.RecordEventRunAsync(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
            }
        }

        [Fact]
        public async Task RunOneTickAsync_DueAccount_PullsOnceWithKeyedServiceAndRecordsNextRun()
        {
            var h = new Harness();
            (_, ProviderAccount account) = h.AddAccount("Ring", eventNext: null, intervalMinutes: 30);
            ScheduledSyncService service = h.Build();

            await service.RunOneTickAsync(CancellationToken.None);

            h.Events["Ring"].Verify(e => e.GetEventsAsync(account.Id, It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), null, It.IsAny<CancellationToken>()), Times.Once);
            h.Schedules.Verify(r => r.RecordEventRunAsync(account.Id, Now, Now.AddMinutes(30), It.IsAny<CancellationToken>()), Times.Once);
            h.Budget.Verify(b => b.RecordCallAsync("Ring", It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task RunOneTickAsync_DueByPastNextRun_Pulls()
        {
            var h = new Harness();
            (_, ProviderAccount account) = h.AddAccount("Ring", eventNext: Now.AddSeconds(-1));
            ScheduledSyncService service = h.Build();

            await service.RunOneTickAsync(CancellationToken.None);

            h.Schedules.Verify(r => r.RecordEventRunAsync(account.Id, Now, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task RunOneTickAsync_NotDueAccount_IsUntouched()
        {
            var h = new Harness();
            _ = h.AddAccount("Ring", eventNext: Now.AddMinutes(5));
            ScheduledSyncService service = h.Build();

            await service.RunOneTickAsync(CancellationToken.None);

            h.VerifyNotPulled("Ring");
            h.VerifyNothingRecorded();
            h.Budget.Verify(b => b.TryConsumeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
            h.Auth["Ring"].Verify(a => a.RestoreFromSavedCredentialsAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task RunOneTickAsync_FirstRun_PullsFromAccountLastSuccessfulAuth()
        {
            var h = new Harness();
            (_, ProviderAccount account) = h.AddAccount("Ring", eventLast: null);
            ScheduledSyncService service = h.Build();

            await service.RunOneTickAsync(CancellationToken.None);

            h.Events["Ring"].Verify(e => e.GetEventsAsync(account.Id, It.IsAny<string>(), Now.AddDays(-1), It.IsAny<DateTime>(), null, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task RunOneTickAsync_PreviousRunExists_PullsFromLastRunMinusTenMinuteOverlap()
        {
            var h = new Harness();
            DateTime last = Now.AddMinutes(-30);
            (_, ProviderAccount account) = h.AddAccount("Ring", eventNext: Now.AddMinutes(-1), eventLast: last);
            ScheduledSyncService service = h.Build();

            await service.RunOneTickAsync(CancellationToken.None);

            h.Events["Ring"].Verify(e => e.GetEventsAsync(account.Id, It.IsAny<string>(), last.AddMinutes(-10), It.IsAny<DateTime>(), null, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task RunOneTickAsync_IntervalBelowMinimum_RecordsNextRunAtMinimum()
        {
            var h = new Harness();
            (_, ProviderAccount account) = h.AddAccount("Ring", intervalMinutes: 0);
            ScheduledSyncService service = h.Build();

            await service.RunOneTickAsync(CancellationToken.None);

            h.Schedules.Verify(r => r.RecordEventRunAsync(account.Id, Now, Now.AddMinutes(SyncSchedule.MinimumPollIntervalMinutes), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task RunOneTickAsync_InactiveAccount_IsSkipped()
        {
            var h = new Harness();
            _ = h.AddAccount("Ring", active: false);
            ScheduledSyncService service = h.Build();

            await service.RunOneTickAsync(CancellationToken.None);

            h.VerifyNotPulled("Ring");
            h.VerifyNothingRecorded();
            Assert.Contains(h.Logs.Lines, l => l.StartsWith("Warning"));
        }

        [Fact]
        public async Task RunOneTickAsync_MissingAccount_IsSkipped()
        {
            var h = new Harness();
            (_, ProviderAccount account) = h.AddAccount("Ring");
            _ = h.Accounts.Setup(r => r.GetAsync(account.Id, It.IsAny<CancellationToken>())).ReturnsAsync((ProviderAccount?)null);
            ScheduledSyncService service = h.Build();

            await service.RunOneTickAsync(CancellationToken.None);

            h.VerifyNotPulled("Ring");
            h.VerifyNothingRecorded();
            Assert.Contains(h.Logs.Lines, l => l.StartsWith("Warning"));
        }

        [Fact]
        public async Task RunOneTickAsync_ProviderNotInEnabledProviders_IsSkipped()
        {
            var h = new Harness { EnabledProviders = ["Wyze"] };
            _ = h.AddAccount("Ring");
            ScheduledSyncService service = h.Build();

            await service.RunOneTickAsync(CancellationToken.None);

            h.VerifyNotPulled("Ring");
            h.VerifyNothingRecorded();
        }

        [Fact]
        public async Task RunOneTickAsync_EnabledProviderDifferentCase_StillPullsUsingConfiguredKey()
        {
            var h = new Harness { EnabledProviders = ["ring"] };
            (_, ProviderAccount account) = h.AddAccount("Ring");
            // Keyed services are registered under the configured spelling ("ring").
            h.Events["ring"] = h.Events["Ring"];
            h.Auth["ring"] = h.Auth["Ring"];
            _ = h.Events.Remove("Ring");
            _ = h.Auth.Remove("Ring");
            ScheduledSyncService service = h.Build();

            await service.RunOneTickAsync(CancellationToken.None);

            h.Events["ring"].Verify(e => e.GetEventsAsync(account.Id, It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), null, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task RunOneTickAsync_KeyedServiceNotRegistered_IsSkippedWithWarning()
        {
            var h = new Harness { EnabledProviders = ["Ring"] };
            _ = h.AddAccount("Ring", registerKeyed: false);
            ScheduledSyncService service = h.Build();

            await service.RunOneTickAsync(CancellationToken.None);

            h.VerifyNothingRecorded();
            Assert.Contains(h.Logs.Lines, l => l.StartsWith("Warning"));
        }

        [Fact]
        public async Task RunOneTickAsync_TwoProviders_EachUsesItsOwnKeyedService()
        {
            var h = new Harness { EnabledProviders = ["Ring", "Wyze"] };
            (_, ProviderAccount ring) = h.AddAccount("Ring");
            (_, ProviderAccount wyze) = h.AddAccount("Wyze");
            ScheduledSyncService service = h.Build();

            await service.RunOneTickAsync(CancellationToken.None);

            h.Events["Ring"].Verify(e => e.GetEventsAsync(ring.Id, It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), null, It.IsAny<CancellationToken>()), Times.Once);
            h.Events["Ring"].Verify(e => e.GetEventsAsync(wyze.Id, It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), null, It.IsAny<CancellationToken>()), Times.Never);
            h.Events["Wyze"].Verify(e => e.GetEventsAsync(wyze.Id, It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), null, It.IsAny<CancellationToken>()), Times.Once);
            h.Events["Wyze"].Verify(e => e.GetEventsAsync(ring.Id, It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), null, It.IsAny<CancellationToken>()), Times.Never);
            h.Budget.Verify(b => b.TryConsumeAsync("Ring", It.IsAny<CancellationToken>()), Times.Once);
            h.Budget.Verify(b => b.TryConsumeAsync("Wyze", It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task RunOneTickAsync_TwoAccounts_RunsSequentiallySecondStartsAfterFirstCompletes()
        {
            var h = new Harness { EnabledProviders = ["Ring", "Wyze"] };
            _ = h.AddAccount("Ring");
            _ = h.AddAccount("Wyze");
            var firstStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var releaseFirst = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _ = h.Events["Ring"]
                .Setup(e => e.GetEventsAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                .Returns(async () =>
                {
                    firstStarted.SetResult();
                    await releaseFirst.Task;
                    return (IReadOnlyList<DeviceEvent>)[];
                });
            ScheduledSyncService service = h.Build();

            Task tick = service.RunOneTickAsync(CancellationToken.None);
            await firstStarted.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await Task.Delay(200);

            // First pull is still blocked: the second account must not have started.
            h.VerifyNotPulled("Wyze");
            Assert.False(tick.IsCompleted);

            releaseFirst.SetResult();
            await tick.WaitAsync(TimeSpan.FromSeconds(10));

            h.Events["Wyze"].Verify(e => e.GetEventsAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), null, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task RunOneTickAsync_OneAccountThrows_LogsErrorWithoutGuidRecordsRunAndContinues()
        {
            var h = new Harness { EnabledProviders = ["Ring", "Wyze"] };
            (_, ProviderAccount ring) = h.AddAccount("Ring");
            (_, ProviderAccount wyze) = h.AddAccount("Wyze");
            _ = h.Auth["Ring"].Setup(a => a.RestoreFromSavedCredentialsAsync(ring.Id, It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("boom"));
            ScheduledSyncService service = h.Build();

            await service.RunOneTickAsync(CancellationToken.None);

            Assert.Contains(h.Logs.Lines, l => l.StartsWith("Error") && l.Contains("Ring"));
            Assert.DoesNotContain(h.Logs.Lines, l => l.Contains(ring.Id.ToString()) || l.Contains(wyze.Id.ToString()));
            h.Schedules.Verify(r => r.RecordEventRunAsync(ring.Id, Now, Now.AddMinutes(30), It.IsAny<CancellationToken>()), Times.Once);
            h.Schedules.Verify(r => r.RecordEventRunAsync(wyze.Id, Now, Now.AddMinutes(30), It.IsAny<CancellationToken>()), Times.Once);
            h.Events["Wyze"].Verify(e => e.GetEventsAsync(wyze.Id, It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), null, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task RunOneTickAsync_BudgetRefused_DoesNotPullOrRecord()
        {
            var h = new Harness();
            _ = h.AddAccount("Ring");
            _ = h.Budget.Setup(b => b.TryConsumeAsync("Ring", It.IsAny<CancellationToken>())).ReturnsAsync(false);
            ScheduledSyncService service = h.Build();

            await service.RunOneTickAsync(CancellationToken.None);

            h.VerifyNotPulled("Ring");
            h.VerifyNothingRecorded();
            h.Auth["Ring"].Verify(a => a.RestoreFromSavedCredentialsAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>()), Times.Never);
            Assert.Contains(h.Logs.Lines, l => l.StartsWith("Information") && l.Contains("budget"));
        }

        [Fact]
        public async Task RunOneTickAsync_AuthRestoreReturnsFalse_DoesNotPullButRecordsAttempt()
        {
            var h = new Harness();
            (_, ProviderAccount account) = h.AddAccount("Ring", authResult: false);
            ScheduledSyncService service = h.Build();

            await service.RunOneTickAsync(CancellationToken.None);

            h.VerifyNotPulled("Ring");
            h.Schedules.Verify(r => r.RecordEventRunAsync(account.Id, Now, Now.AddMinutes(30), It.IsAny<CancellationToken>()), Times.Once);
            Assert.Contains(h.Logs.Lines, l => l.StartsWith("Warning"));
            Assert.DoesNotContain(h.Logs.Lines, l => l.Contains(account.Id.ToString()));
        }

        [Fact]
        public async Task RunOneTickAsync_AlreadyCancelled_ThrowsOperationCanceled()
        {
            var h = new Harness();
            _ = h.AddAccount("Ring");
            ScheduledSyncService service = h.Build();
            using var cts = new CancellationTokenSource();
            await cts.CancelAsync();

            _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.RunOneTickAsync(cts.Token));

            h.VerifyNothingRecorded();
        }

        [Fact]
        public async Task RunOneTickAsync_CancelledDuringAccountRun_PropagatesAndDoesNotRecordOrContinue()
        {
            var h = new Harness { EnabledProviders = ["Ring", "Wyze"] };
            (_, ProviderAccount ring) = h.AddAccount("Ring");
            _ = h.AddAccount("Wyze");
            using var cts = new CancellationTokenSource();
            _ = h.Auth["Ring"].Setup(a => a.RestoreFromSavedCredentialsAsync(ring.Id, It.IsAny<CancellationToken>()))
                .Returns(async () =>
                {
                    await cts.CancelAsync();
                    throw new OperationCanceledException(cts.Token);
                });
            ScheduledSyncService service = h.Build();

            _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.RunOneTickAsync(cts.Token));

            h.VerifyNothingRecorded();
            h.VerifyNotPulled("Wyze");
        }

        [Fact]
        public async Task RunOneTickAsync_SuccessfulPull_NeverLogsAccountGuid()
        {
            var h = new Harness();
            (_, ProviderAccount account) = h.AddAccount("Ring");
            ScheduledSyncService service = h.Build();

            await service.RunOneTickAsync(CancellationToken.None);

            Assert.NotEmpty(h.Logs.Lines);
            Assert.DoesNotContain(h.Logs.Lines, l => l.Contains(account.Id.ToString()));
            Assert.Contains(h.Logs.Lines, l => l.StartsWith("Information") && l.Contains("Ring"));
        }

        [Theory]
        [InlineData(0, 5)]
        [InlineData(-1, 5)]
        [InlineData(4, 5)]
        [InlineData(5, 5)]
        [InlineData(60, 60)]
        public void ResolveTickInterval_Seconds_ClampsToFiveSecondFloor(int configured, int expectedSeconds)
        {
            Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), ScheduledSyncService.ResolveTickInterval(configured));
        }
    }
}