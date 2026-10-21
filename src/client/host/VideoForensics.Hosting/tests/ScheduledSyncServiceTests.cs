using System.Collections.Concurrent;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Moq;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Hosting.BackgroundServices;
using VideoForensics.Hosting.Contracts;
using VideoForensics.Core.Providers.Common.Contracts;

using VideoForensics.Hosting.Tests.Services;

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

            public Mock<IDeviceConfigRepository> DeviceConfigs { get; } = new();
            public Mock<ILocationRepository> Locations { get; } = new();
            public Mock<IProviderApiBudgetGuard> Budget { get; } = new();
            public Mock<IInitialSyncDispatcher> InitialSync { get; } = new();
            public Mock<IProviderSyncRunRepository> SyncRuns { get; } = new();
            public Mock<ISnapshotSyncService> SnapshotSync { get; } = new();
            public Mock<ISnapshotRollupService> SnapshotRollup { get; } = new();
            public List<ProviderSyncRun> Runs { get; } = [];
            public Dictionary<string, Mock<IEventAndConfigService>> Events { get; } = new();
            public Dictionary<string, Mock<IProviderAuthService>> Auth { get; } = new();
            public Dictionary<string, Mock<IMediaDownloadService>> Media { get; } = new();

            public Dictionary<string, Mock<IDeviceDiscoveryService>> Discovery { get; } = new();
            public CapturingLoggerProvider Logs { get; } = new();
            public string[] EnabledProviders { get; set; } = ["Ring"];

            /// <summary>Off by default, as in production: the first sync and its monthly backfill are switched off.</summary>
            public bool InitialSyncEnabled { get; set; }

            private readonly ServiceCollection _services = new();
            private readonly List<SyncSchedule> _schedules = [];
            public IReadOnlyList<SyncSchedule> ScheduleList => _schedules;

            public Harness()
            {
                _ = Budget.Setup(b => b.TryConsumeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
                _ = Schedules.Setup(r => r.ListEnabledAsync(It.IsAny<CancellationToken>())).ReturnsAsync(() => _schedules);
                _ = SyncRuns.Setup(r => r.AddAsync(It.IsAny<ProviderSyncRun>(), It.IsAny<CancellationToken>()))
                    .Callback<ProviderSyncRun, CancellationToken>((run, _) => Runs.Add(run))
                    .Returns(Task.CompletedTask);
            }

            public (SyncSchedule Schedule, ProviderAccount Account) AddAccount(
                string provider,
                bool active = true,
                DateTime? eventNext = null,
                DateTime? eventLast = null,
                int intervalMinutes = 30,
                bool registerKeyed = true,
                bool authResult = true,
                bool initialSyncCompleted = true,
                DateTime? snapshotNext = null)
            {
                var account = new ProviderAccount
                {
                    Id = Guid.NewGuid(),
                    ProviderName = provider,
                    IsActive = active,
                    LastSuccessfulAuthUtc = Now.AddDays(-1),
                    InitialSyncCompleted = initialSyncCompleted,
                    LastSyncCompletedUtc = Now.AddDays(-1),
                };
                var schedule = new SyncSchedule
                {
                    Id = Guid.NewGuid(),
                    ProviderAccountId = account.Id,
                    IsEnabled = true,
                    EventPollIntervalMinutes = intervalMinutes,
                    SnapshotRssiIntervalSeconds = 60,
                    SnapshotNextRunUtc = snapshotNext ?? Now.AddHours(1),
                    LastRollupUtc = Now.AddMinutes(-1),
                    EventNextRunUtc = eventNext,
                    EventLastRunUtc = eventLast
                };
                _schedules.Add(schedule);
                _ = Accounts.Setup(r => r.GetAsync(account.Id, It.IsAny<CancellationToken>())).ReturnsAsync(account);

                var location = new Location { Id = Guid.NewGuid(), ProviderLocationId = "loc-" + provider, Name = "Home" };
                var device = new Device { Id = Guid.NewGuid(), LocationId = location.Id, ProviderDeviceId = "dev-" + account.Id, Name = "Cam", Type = "camera" };
                _ = Locations.Setup(r => r.ListByProviderAccountIdAsync(account.Id, It.IsAny<CancellationToken>())).ReturnsAsync([location]);
                _ = Devices.Setup(r => r.GetByLocationIdAsync(location.Id, It.IsAny<CancellationToken>())).ReturnsAsync([device]);

                if (registerKeyed)
                {
                    if (!Events.ContainsKey(provider))
                    {
                        Events[provider] = new Mock<IEventAndConfigService>();
                        Auth[provider] = new Mock<IProviderAuthService>();
                    }

                    if (!Media.ContainsKey(provider))
                    {
                        Media[provider] = NewMedia();
                    }

                    if (!Discovery.ContainsKey(provider))
                    {
                        Discovery[provider] = new Mock<IDeviceDiscoveryService>();
                    }

                    // Provider discovery returns the same location and device the test seeds, so the pull has work to do.
                    DiscoveryStub.Wire(account.Id, location, [device], Discovery[provider], Locations, Devices);

                    _ = Auth[provider].Setup(a => a.RestoreFromSavedCredentialsAsync(account.Id, It.IsAny<CancellationToken>())).ReturnsAsync(authResult);
                }

                return (schedule, account);
            }

            // The scheduled pull reads events through the provider's media download service; a clean empty result keeps the run successful.
            private static Mock<IMediaDownloadService> NewMedia()
            {
                var media = new Mock<IMediaDownloadService>();
                _ = media
                    .Setup(m => m.DownloadVideosAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new DownloadResult(true));
                return media;
            }

            // The pull walks its window in 30-day batches, so each pull is identified by its first batch, which starts exactly at the window start.
            // A first run starts at the account's LastSuccessfulAuthUtc, which AddAccount sets to one day before Now.
            public static System.Linq.Expressions.Expression<Func<IMediaDownloadService, Task<DownloadResult>>> Pulled(Guid accountId, DateTime? from = null)
            {
                string deviceId = "dev-" + accountId;
                DateTime start = from ?? Now.AddDays(-1);
                return m => m.DownloadVideosAsync(deviceId, It.IsAny<string>(), start, It.IsAny<DateTime>(), It.IsAny<string?>(), It.IsAny<CancellationToken>());
            }

            public ScheduledSyncService Build()
            {
                _ = _services.AddSingleton(Schedules.Object);
                _ = _services.AddSingleton(Accounts.Object);
                _ = _services.AddSingleton(Devices.Object);
                _ = _services.AddSingleton(DeviceConfigs.Object);
                _ = _services.AddSingleton(Locations.Object);
                _ = _services.AddSingleton(Budget.Object);
                _ = _services.AddSingleton(SyncRuns.Object);
                _ = _services.AddSingleton(InitialSync.Object);
                _ = _services.AddLogging(b => b.SetMinimumLevel(LogLevel.Trace).AddProvider(Logs));
                foreach ((string provider, Mock<IEventAndConfigService> events) in Events)
                {
                    _ = _services.AddKeyedSingleton(provider, events.Object);
                    _ = _services.AddKeyedSingleton(provider, Auth[provider].Object);
                    if (!Media.ContainsKey(provider))
                    {
                        Media[provider] = NewMedia();
                    }

                    _ = _services.AddKeyedSingleton(provider, Media[provider].Object);
                    if (!Discovery.ContainsKey(provider))
                    {
                        Discovery[provider] = new Mock<IDeviceDiscoveryService>();
                    }

                    _ = _services.AddKeyedSingleton(provider, Discovery[provider].Object);
                }

                _ = _services.AddScoped(_ => SnapshotSync.Object);
                _ = _services.AddScoped(_ => SnapshotRollup.Object);
                ServiceProvider sp = _services.BuildServiceProvider();
                var options = new Mock<IOptionsMonitor<ScheduledTasksOptions>>();
                _ = options.Setup(o => o.CurrentValue).Returns(() => new ScheduledTasksOptions { EnabledProviders = EnabledProviders, InitialSyncEnabled = InitialSyncEnabled });
                return new ScheduledSyncService(
                    sp.GetRequiredService<IServiceScopeFactory>(),
                    options.Object,
                    sp.GetRequiredService<ILogger<ScheduledSyncService>>(),
                    new ManualTimeProvider(Now));
            }

            public void VerifyNotPulled(string provider)
            {
                Media[provider].Verify(m => m.DownloadVideosAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
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

            h.Media["Ring"].Verify(Harness.Pulled(account.Id), Times.Once);
            h.Schedules.Verify(r => r.RecordEventRunAsync(account.Id, Now, Now.AddMinutes(30), It.IsAny<CancellationToken>()), Times.Once);
            h.Budget.Verify(b => b.RecordCallAsync("Ring", It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task RunOneTickAsync_InitialSyncIncomplete_RunsInitialSyncInsteadOfPull()
        {
            var h = new Harness { InitialSyncEnabled = true };
            (_, ProviderAccount account) = h.AddAccount("Ring", eventNext: null, initialSyncCompleted: false);
            ScheduledSyncService service = h.Build();

            await service.RunOneTickAsync(CancellationToken.None);

            h.InitialSync.Verify(d => d.RunInitialSyncAsync(account.Id, It.IsAny<CancellationToken>()), Times.Once);
            h.Media["Ring"].Verify(Harness.Pulled(account.Id), Times.Never);
            h.Schedules.Verify(r => r.RecordEventRunAsync(account.Id, Now, Now.AddMinutes(30), It.IsAny<CancellationToken>()), Times.Once);
        }

        /// <summary>With the first sync switched off, an account with no completed sync gets the normal incremental pull, not the first sync or the monthly backfill.</summary>
        [Fact]
        public async Task RunOneTickAsync_InitialSyncDisabled_DoesNotRunFirstSyncOrMonthlyBackfill()
        {
            var h = new Harness();
            (_, ProviderAccount account) = h.AddAccount("Ring", eventNext: null, initialSyncCompleted: false);
            ScheduledSyncService service = h.Build();

            await service.RunOneTickAsync(CancellationToken.None);

            h.InitialSync.Verify(d => d.RunInitialSyncAsync(account.Id, It.IsAny<CancellationToken>()), Times.Never);
            h.Schedules.Verify(r => r.RecordBackfillCursorAsync(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
            h.Schedules.Verify(r => r.RecordEventRunAsync(account.Id, Now, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
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
        public async Task RunOneTickAsync_RollupDue_RollsUpSinceOneIntervalAgoAndRecordsRollup()
        {
            var h = new Harness();
            (_, ProviderAccount account) = h.AddAccount("Ring");
            h.Auth["Ring"].Setup(a => a.RestoreFromSavedCredentialsAsync(account.Id, It.IsAny<CancellationToken>())).ReturnsAsync(true);
            h.SnapshotRollup.Setup(s => s.RollUpAccountAsync(account.Id, Now.AddMinutes(-15), Now, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new SnapshotRollupResult(DevicesAttempted: 1, DevicesRolledUp: 1, ErrorMessage: null));
            ScheduledSyncService service = h.Build();

            // The harness schedule has no LastRollupUtc, so the roll-up is due and starts one interval back.
            h.Schedules.Setup(r => r.ListEnabledAsync(It.IsAny<CancellationToken>())).ReturnsAsync(h.ScheduleList.Select(s => { s.LastRollupUtc = null; return s; }).ToList());
            await service.RunOneTickAsync(CancellationToken.None);

            h.SnapshotRollup.Verify(s => s.RollUpAccountAsync(account.Id, Now.AddMinutes(-15), Now, It.IsAny<CancellationToken>()), Times.Once);
            h.Schedules.Verify(r => r.RecordRollupAsync(account.Id, Now, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task RunOneTickAsync_RollupFails_DoesNotRecordRollup()
        {
            var h = new Harness();
            (_, ProviderAccount account) = h.AddAccount("Ring");
            h.Auth["Ring"].Setup(a => a.RestoreFromSavedCredentialsAsync(account.Id, It.IsAny<CancellationToken>())).ReturnsAsync(true);
            h.SnapshotRollup.Setup(s => s.RollUpAccountAsync(account.Id, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new SnapshotRollupResult(DevicesAttempted: 1, DevicesRolledUp: 0, ErrorMessage: "Cam: ffmpeg failed"));
            h.Schedules.Setup(r => r.ListEnabledAsync(It.IsAny<CancellationToken>())).ReturnsAsync(h.ScheduleList.Select(s => { s.LastRollupUtc = null; return s; }).ToList());
            ScheduledSyncService service = h.Build();

            await service.RunOneTickAsync(CancellationToken.None);

            h.Schedules.Verify(r => r.RecordRollupAsync(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task RunOneTickAsync_RollupFailed_IsNotRetriedWithinBackoff()
        {
            var h = new Harness();
            (_, ProviderAccount account) = h.AddAccount("Ring");
            h.Auth["Ring"].Setup(a => a.RestoreFromSavedCredentialsAsync(account.Id, It.IsAny<CancellationToken>())).ReturnsAsync(true);
            h.SnapshotRollup.Setup(s => s.RollUpAccountAsync(account.Id, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new SnapshotRollupResult(DevicesAttempted: 1, DevicesRolledUp: 0, ErrorMessage: "Cam: ffmpeg failed"));
            h.Schedules.Setup(r => r.ListEnabledAsync(It.IsAny<CancellationToken>())).ReturnsAsync(h.ScheduleList.Select(s => { s.LastRollupUtc = null; return s; }).ToList());
            ScheduledSyncService service = h.Build();

            // Two ticks at the same time: the second falls inside the retry backoff, so the roll-up runs once.
            await service.RunOneTickAsync(CancellationToken.None);
            await service.RunOneTickAsync(CancellationToken.None);

            h.SnapshotRollup.Verify(s => s.RollUpAccountAsync(account.Id, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task RunOneTickAsync_SnapshotDue_RunsSnapshotSyncAndRecordsNextSnapshotRun()
        {
            var h = new Harness();
            (_, ProviderAccount account) = h.AddAccount("Ring", snapshotNext: Now.AddMinutes(-1));
            h.Auth["Ring"].Setup(a => a.RestoreFromSavedCredentialsAsync(account.Id, It.IsAny<CancellationToken>())).ReturnsAsync(true);
            h.SnapshotSync.Setup(s => s.SyncAccountSnapshotsAsync(account.Id, null, null, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new SnapshotSyncResult(DevicesAttempted: 1, DevicesSucceeded: 1, SnapshotsSaved: 1, DevicesFailed: 0, ErrorMessage: null));
            ScheduledSyncService service = h.Build();

            await service.RunOneTickAsync(CancellationToken.None);

            h.SnapshotSync.Verify(s => s.SyncAccountSnapshotsAsync(account.Id, null, null, It.IsAny<CancellationToken>()), Times.Once);
            h.Schedules.Verify(r => r.RecordSnapshotRunAsync(account.Id, Now, Now.AddSeconds(60), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task RunOneTickAsync_SnapshotNotDue_DoesNotRunSnapshotSync()
        {
            var h = new Harness();
            (_, ProviderAccount account) = h.AddAccount("Ring", snapshotNext: Now.AddMinutes(5));
            ScheduledSyncService service = h.Build();

            await service.RunOneTickAsync(CancellationToken.None);

            h.SnapshotSync.Verify(s => s.SyncAccountSnapshotsAsync(It.IsAny<Guid>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()), Times.Never);
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

            h.Media["Ring"].Verify(Harness.Pulled(account.Id, Now.AddDays(-1)), Times.Once);
        }

        [Fact]
        public async Task RunOneTickAsync_PreviousRunExists_PullsFromLastRunMinusTenMinuteOverlap()
        {
            var h = new Harness();
            DateTime last = Now.AddMinutes(-30);
            (_, ProviderAccount account) = h.AddAccount("Ring", eventNext: Now.AddMinutes(-1), eventLast: last);
            ScheduledSyncService service = h.Build();

            await service.RunOneTickAsync(CancellationToken.None);

            h.Media["Ring"].Verify(Harness.Pulled(account.Id, last.AddMinutes(-10)), Times.Once);
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
            Assert.Contains(h.Logs.Lines, l => l.StartsWith("Debug"));
        }

        [Fact]
        public async Task RunOneTickAsync_RemovedAccountStillFlaggedActive_IsSkipped()
        {
            var h = new Harness();
            (_, ProviderAccount account) = h.AddAccount("Ring");
            account.RemovedUtc = Now.AddDays(-1);
            ScheduledSyncService service = h.Build();

            await service.RunOneTickAsync(CancellationToken.None);

            h.VerifyNotPulled("Ring");
            h.VerifyNothingRecorded();
            Assert.Contains(h.Logs.Lines, l => l.Contains("account was removed"));
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
            h.Discovery["ring"] = h.Discovery["Ring"];
            h.Media["ring"] = h.Media["Ring"];
            _ = h.Media.Remove("Ring");
            _ = h.Events.Remove("Ring");
            _ = h.Auth.Remove("Ring");
            _ = h.Discovery.Remove("Ring");
            ScheduledSyncService service = h.Build();

            await service.RunOneTickAsync(CancellationToken.None);

            h.Media["ring"].Verify(Harness.Pulled(account.Id), Times.Once);
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

            h.Media["Ring"].Verify(Harness.Pulled(ring.Id), Times.Once);
            h.Media["Ring"].Verify(Harness.Pulled(wyze.Id), Times.Never);
            h.Media["Wyze"].Verify(Harness.Pulled(wyze.Id), Times.Once);
            h.Media["Wyze"].Verify(Harness.Pulled(ring.Id), Times.Never);
            h.Budget.Verify(b => b.TryConsumeAsync("Ring", It.IsAny<CancellationToken>()), Times.Once);
            h.Budget.Verify(b => b.TryConsumeAsync("Wyze", It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task RunOneTickAsync_TwoAccounts_RunsSequentiallySecondStartsAfterFirstCompletes()
        {
            var h = new Harness { EnabledProviders = ["Ring", "Wyze"] };
            _ = h.AddAccount("Ring");
            (_, ProviderAccount wyze) = h.AddAccount("Wyze");
            var firstStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var releaseFirst = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _ = h.Media["Ring"]
                .Setup(m => m.DownloadVideosAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                .Returns(async () =>
                {
                    firstStarted.TrySetResult();
                    await releaseFirst.Task;
                    return new DownloadResult(true);
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

            h.Media["Wyze"].Verify(Harness.Pulled(wyze.Id), Times.Once);
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
            h.Media["Wyze"].Verify(Harness.Pulled(wyze.Id), Times.Once);
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
            Assert.Contains(h.Logs.Lines, l => l.StartsWith("Debug") && l.Contains("budget"));
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
            Assert.Empty(h.Runs);
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
        [Fact]
        public async Task RunOneTickAsync_ScheduledPullSucceeds_RecordsLastSyncCompleted()
        {
            var h = new Harness();
            (_, ProviderAccount account) = h.AddAccount("Ring");
            ScheduledSyncService service = h.Build();

            await service.RunOneTickAsync(CancellationToken.None);

            h.Accounts.Verify(r => r.UpdateAsync(It.Is<ProviderAccount>(a => a.Id == account.Id && a.LastSyncCompletedUtc == Now), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        }


        [Fact]
        public async Task RunOneTickAsync_ScheduledPullSucceeds_WritesSuccessRun()
        {
            var h = new Harness();
            (_, ProviderAccount account) = h.AddAccount("Ring");
            ScheduledSyncService service = h.Build();

            await service.RunOneTickAsync(CancellationToken.None);

            ProviderSyncRun run = Assert.Single(h.Runs);
            Assert.Equal(account.Id, run.ProviderAccountId);
            Assert.True(run.Success);
            Assert.Null(run.ErrorMessage);
            Assert.NotNull(run.CompletedUtc);
        }

        [Fact]
        public async Task RunOneTickAsync_ScheduledPullSucceeds_RecordsSyncTypeFromPullStartDate()
        {
            var h = new Harness();
            (_, ProviderAccount account) = h.AddAccount("Ring");
            ScheduledSyncService service = h.Build();

            await service.RunOneTickAsync(CancellationToken.None);

            ProviderSyncRun run = Assert.Single(h.Runs);
            Assert.Equal(account.LastSuccessfulAuthUtc is null ? "Full" : "Incremental", run.SyncType);
            Assert.Equal(0, run.EventsSynced);
        }

        [Fact]
        public async Task RunOneTickAsync_AuthRestoreReturnsFalse_LeavesSyncTypeNull()
        {
            var h = new Harness();
            _ = h.AddAccount("Ring", authResult: false);
            ScheduledSyncService service = h.Build();

            await service.RunOneTickAsync(CancellationToken.None);

            Assert.Null(Assert.Single(h.Runs).SyncType);
        }

        [Fact]
        public async Task RunOneTickAsync_PullRecordsAccountError_WritesFailureRunWithAccountMessage()
        {
            var h = new Harness();
            (_, ProviderAccount account) = h.AddAccount("Ring");
            // The pull catches discovery failures and records them on the account instead of throwing.
            _ = h.Discovery["Ring"].Setup(d => d.GetLocationsAsync(account.Id, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("discovery boom"));
            ScheduledSyncService service = h.Build();

            await service.RunOneTickAsync(CancellationToken.None);

            ProviderSyncRun run = Assert.Single(h.Runs);
            Assert.False(run.Success);
            Assert.Equal("discovery boom", run.ErrorMessage);
            Assert.NotNull(run.CompletedUtc);
        }

        [Fact]
        public async Task RunOneTickAsync_AttemptThrows_WritesFailureRunWithExceptionMessage()
        {
            var h = new Harness();
            (_, ProviderAccount account) = h.AddAccount("Ring");
            _ = h.Budget.Setup(b => b.RecordCallAsync("Ring", It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("budget store down"));
            ScheduledSyncService service = h.Build();

            await service.RunOneTickAsync(CancellationToken.None);

            ProviderSyncRun run = Assert.Single(h.Runs);
            Assert.Equal(account.Id, run.ProviderAccountId);
            Assert.False(run.Success);
            Assert.Equal("budget store down", run.ErrorMessage);
        }

        [Fact]
        public async Task RunOneTickAsync_AuthRestoreReturnsFalse_WritesFailureRunWithRestoreMessage()
        {
            var h = new Harness();
            _ = h.AddAccount("Ring", authResult: false);
            ScheduledSyncService service = h.Build();

            await service.RunOneTickAsync(CancellationToken.None);

            ProviderSyncRun run = Assert.Single(h.Runs);
            Assert.False(run.Success);
            Assert.Equal("Saved credentials could not be restored. Sign in to this account again.", run.ErrorMessage);
        }

        [Fact]
        public async Task RunOneTickAsync_MissingAccount_WritesFailureRunAccountNotFoundAndBacksOff()
        {
            var h = new Harness();
            (SyncSchedule schedule, ProviderAccount account) = h.AddAccount("Ring");
            _ = h.Accounts.Setup(r => r.GetAsync(account.Id, It.IsAny<CancellationToken>())).ReturnsAsync((ProviderAccount?)null);
            ScheduledSyncService service = h.Build();

            await service.RunOneTickAsync(CancellationToken.None);

            ProviderSyncRun run = Assert.Single(h.Runs);
            Assert.Equal(schedule.ProviderAccountId, run.ProviderAccountId);
            Assert.False(run.Success);
            Assert.Equal("Account not found", run.ErrorMessage);
            // The schedule stays enabled, so it must back off a full interval instead of recording a failure every tick.
            h.Schedules.Verify(r => r.RecordEventRunAsync(account.Id, Now, Now.AddMinutes(30), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task RunOneTickAsync_RunWriteFails_LogsErrorWithoutGuidAndStillRecordsSchedule()
        {
            var h = new Harness();
            (_, ProviderAccount account) = h.AddAccount("Ring");
            _ = h.SyncRuns.Setup(r => r.AddAsync(It.IsAny<ProviderSyncRun>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("database unavailable"));
            ScheduledSyncService service = h.Build();

            // Recording is best-effort: a failed write must not fail the tick or the schedule update.
            await service.RunOneTickAsync(CancellationToken.None);

            Assert.Contains(h.Logs.Lines, l => l.StartsWith("Error") && l.Contains("sync run"));
            Assert.DoesNotContain(h.Logs.Lines, l => l.Contains(account.Id.ToString()));
            h.Schedules.Verify(r => r.RecordEventRunAsync(account.Id, Now, Now.AddMinutes(30), It.IsAny<CancellationToken>()), Times.Once);
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