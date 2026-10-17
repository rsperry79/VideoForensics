namespace VideoForensics.Hosting.Tests.Services
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using VideoForensics.Core.Providers.Common.Contracts;
    using VideoForensics.Data.Common.Contracts;
    using VideoForensics.Data.Common.Entities;
    using VideoForensics.Hosting.Contracts;
    using VideoForensics.Hosting.Services;
    using ProviderAccountEntity = VideoForensics.Data.Common.Entities.ProviderAccount;

    /// <summary>
    /// Snapshots and "All" syncs: the runner pulls (inventory, and events for All) and then fetches snapshots, records the snapshot
    /// count on the run row, and resolves the provider's own auth and pull services by the account's provider name.
    /// </summary>
    public class AccountSyncRunnerSnapshotTests
    {
        private static readonly DateTime LastAuth = new(2026, 10, 14, 18, 0, 0, DateTimeKind.Utc);

        private readonly Mock<IProviderAuthService> _auth = new();
        private readonly Mock<IEventPullService> _pull = new();
        private readonly Mock<IProviderAccountRepository> _accounts = new();
        private readonly Mock<IProviderSyncRunRepository> _runs = new();
        private readonly Mock<ISnapshotSyncService> _snapshots = new();
        private readonly SyncRunTally _tally = new();
        private readonly SyncRunOptionsHolder _holder = new();
        private readonly Guid _id = Guid.NewGuid();
        private readonly List<ProviderSyncRun> _recorded = new();
        private readonly ProviderAccountEntity _account;

        public AccountSyncRunnerSnapshotTests()
        {
            _account = new ProviderAccountEntity
            {
                Id = _id,
                UserId = Guid.NewGuid(),
                ProviderName = "Ring",
                LinkedUtc = DateTime.UtcNow,
                LastSuccessfulAuthUtc = LastAuth,
                IsActive = true
            };
            _accounts.Setup(r => r.GetAsync(_id, It.IsAny<CancellationToken>())).ReturnsAsync(_account);
            _auth.Setup(a => a.RestoreFromSavedCredentialsAsync(_id, It.IsAny<CancellationToken>())).ReturnsAsync(true);
            _runs.Setup(r => r.AddAsync(It.IsAny<ProviderSyncRun>(), It.IsAny<CancellationToken>()))
                .Callback<ProviderSyncRun, CancellationToken>((run, _) => _recorded.Add(run))
                .Returns(Task.CompletedTask);
        }

        private AccountSyncRunner Sut(IServiceProvider? services = null, bool withSnapshots = true) => new(
            _auth.Object,
            _pull.Object,
            _accounts.Object,
            _runs.Object,
            Mock.Of<ILogger<AccountSyncRunner>>(),
            _tally,
            _holder,
            services,
            withSnapshots ? _snapshots.Object : null);

        private void SnapshotsReturn(SnapshotSyncResult result) => _snapshots
            .Setup(s => s.SyncAccountSnapshotsAsync(_id, It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);

        private void VerifySnapshotsNeverRun() => _snapshots.Verify(
            s => s.SyncAccountSnapshotsAsync(It.IsAny<Guid>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()), Times.Never);

        [Fact]
        public async Task RunAsync_SnapshotsKind_FetchesSnapshotsForResolvedWindowAndRecordsCount()
        {
            var start = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);
            var end = new DateTime(2026, 9, 10, 0, 0, 0, DateTimeKind.Utc);
            SnapshotsReturn(new SnapshotSyncResult(DevicesAttempted: 3, DevicesSucceeded: 3, SnapshotsSaved: 3));

            await Sut().RunAsync(_id, new SyncRunOptions(SyncRunKind.Snapshots, SyncRangeKind.Custom, StartUtc: start, EndUtc: end), CancellationToken.None);

            _snapshots.Verify(s => s.SyncAccountSnapshotsAsync(_id, start, end, It.IsAny<CancellationToken>()), Times.Once);
            ProviderSyncRun run = Assert.Single(_recorded);
            Assert.True(run.Success);
            Assert.Equal("Snapshots", run.SyncType);
            Assert.Equal(3, run.SnapshotsSynced);
        }

        [Fact]
        public async Task RunAsync_SnapshotsKindWithFailedDevice_RecordsFailureAndCount()
        {
            SnapshotsReturn(new SnapshotSyncResult(DevicesAttempted: 2, DevicesSucceeded: 1, SnapshotsSaved: 1, DevicesFailed: 1, ErrorMessage: "1 of 2 device(s) failed: no snapshot"));

            await Sut().RunAsync(_id, new SyncRunOptions(SyncRunKind.Snapshots), CancellationToken.None);

            ProviderSyncRun run = Assert.Single(_recorded);
            Assert.False(run.Success);
            Assert.Contains("no snapshot", run.ErrorMessage);
            Assert.Equal(1, run.SnapshotsSynced);
        }

        [Fact]
        public async Task RunAsync_AllType_PullsThenFetchesSnapshotsAndRecordsBothCounts()
        {
            SnapshotsReturn(new SnapshotSyncResult(DevicesAttempted: 3, DevicesSucceeded: 3, SnapshotsSaved: 3));
            _pull.Setup(p => p.PullAccountEventsAsync(_id, It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
                .Callback(() => { _tally.SetInventory(1, 3); _tally.AddEventsPulled(40); })
                .Returns(Task.CompletedTask);

            await Sut().RunAsync(_id, new SyncRunOptions(IncludeSnapshots: true), CancellationToken.None);

            _pull.Verify(p => p.PullAccountEventsAsync(_id, It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()), Times.Once);
            ProviderSyncRun run = Assert.Single(_recorded);
            Assert.True(run.Success);
            Assert.Equal("Events", run.SyncType);
            Assert.Equal(40, run.EventsSynced);
            Assert.Equal(3, run.SnapshotsSynced);
        }

        [Fact]
        public async Task RunAsync_AllTypeWithDays_PullsAndSnapshotsOverTheDaysWindow()
        {
            SnapshotsReturn(new SnapshotSyncResult(DevicesAttempted: 1, DevicesSucceeded: 1, SnapshotsSaved: 1));
            DateTime? pulledFrom = null;
            _pull.Setup(p => p.PullAccountEventsAsync(_id, It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
                .Callback<Guid, DateTime?, CancellationToken>((_, from, _) => pulledFrom = from)
                .Returns(Task.CompletedTask);
            DateTime before = DateTime.UtcNow;

            await Sut().RunAsync(_id, new SyncRunOptions(RangeKind: SyncRangeKind.Days, Days: 30, IncludeSnapshots: true), CancellationToken.None);

            Assert.NotNull(pulledFrom);
            Assert.InRange(pulledFrom!.Value, before.AddDays(-30).AddSeconds(-1), DateTime.UtcNow.AddDays(-30));
            _snapshots.Verify(s => s.SyncAccountSnapshotsAsync(_id, pulledFrom, null, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task RunAsync_AllTypeSnapshotsFail_KeepsEventCountsAndRecordsFailure()
        {
            SnapshotsReturn(new SnapshotSyncResult(DevicesAttempted: 2, DevicesSucceeded: 1, SnapshotsSaved: 1, DevicesFailed: 1, ErrorMessage: "1 of 2 device(s) failed: no snapshot"));
            _pull.Setup(p => p.PullAccountEventsAsync(_id, It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
                .Callback(() => _tally.AddEventsPulled(7))
                .Returns(Task.CompletedTask);

            await Sut().RunAsync(_id, new SyncRunOptions(IncludeSnapshots: true), CancellationToken.None);

            ProviderSyncRun run = Assert.Single(_recorded);
            Assert.False(run.Success);
            Assert.Equal(7, run.EventsSynced);
            Assert.Equal(1, run.SnapshotsSynced);
        }

        [Fact]
        public async Task RunAsync_AllTypePullFails_StillFetchesSnapshotsAndKeepsBothErrors()
        {
            SnapshotsReturn(new SnapshotSyncResult(DevicesAttempted: 1, DevicesSucceeded: 0, SnapshotsSaved: 0, DevicesFailed: 1, ErrorMessage: "snapshots down"));
            _pull.Setup(p => p.PullAccountEventsAsync(_id, It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
                .Callback(() => _account.LastErrorMessage = "events down")
                .Returns(Task.CompletedTask);

            await Sut().RunAsync(_id, new SyncRunOptions(IncludeSnapshots: true), CancellationToken.None);

            ProviderSyncRun run = Assert.Single(_recorded);
            Assert.False(run.Success);
            Assert.Equal("events down; snapshots down", run.ErrorMessage);
        }

        [Fact]
        public async Task RunAsync_AllTypePullFailsSnapshotsSucceed_KeepsPullErrorAndSnapshotCount()
        {
            SnapshotsReturn(new SnapshotSyncResult(DevicesAttempted: 1, DevicesSucceeded: 1, SnapshotsSaved: 1));
            _pull.Setup(p => p.PullAccountEventsAsync(_id, It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
                .Callback(() => _account.LastErrorMessage = "events down")
                .Returns(Task.CompletedTask);

            await Sut().RunAsync(_id, new SyncRunOptions(IncludeSnapshots: true), CancellationToken.None);

            ProviderSyncRun run = Assert.Single(_recorded);
            Assert.Equal("events down", run.ErrorMessage);
            Assert.Equal(1, run.SnapshotsSynced);
        }

        [Theory]
        [InlineData(SyncRunKind.Events)]
        [InlineData(SyncRunKind.Config)]
        [InlineData(SyncRunKind.Default)]
        public async Task RunAsync_KindsWithoutSnapshots_DoNotFetchSnapshotsOrRecordACount(SyncRunKind kind)
        {
            await Sut().RunAsync(_id, new SyncRunOptions(kind), CancellationToken.None);

            VerifySnapshotsNeverRun();
            Assert.Null(Assert.Single(_recorded).SnapshotsSynced);
        }

        [Fact]
        public async Task RunAsync_LegacyAndInitialSyncs_DoNotFetchSnapshots()
        {
            await Sut().RunAsync(_id, CancellationToken.None);
            await Sut().RunInitialAsync(_id, CancellationToken.None);

            VerifySnapshotsNeverRun();
            Assert.All(_recorded, run => Assert.Null(run.SnapshotsSynced));
        }

        [Fact]
        public async Task RunAsync_SnapshotsKindWithoutSnapshotService_RunsThePullOnly()
        {
            await Sut(withSnapshots: false).RunAsync(_id, new SyncRunOptions(SyncRunKind.Snapshots), CancellationToken.None);

            _pull.Verify(p => p.PullAccountEventsAsync(_id, It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()), Times.Once);
            Assert.Null(Assert.Single(_recorded).SnapshotsSynced);
        }

        [Fact]
        public async Task RunAsync_RestoreFails_DoesNotFetchSnapshots()
        {
            _auth.Setup(a => a.RestoreFromSavedCredentialsAsync(_id, It.IsAny<CancellationToken>())).ReturnsAsync(false);

            await Sut().RunAsync(_id, new SyncRunOptions(SyncRunKind.Snapshots), CancellationToken.None);

            VerifySnapshotsNeverRun();
            Assert.Null(Assert.Single(_recorded).SnapshotsSynced);
        }

        [Fact]
        public async Task RunAsync_AccountOfAnotherProvider_UsesThatProvidersKeyedAuthAndPull()
        {
            _account.ProviderName = "Wyze";
            var keyedAuth = new Mock<IProviderAuthService>();
            keyedAuth.Setup(a => a.RestoreFromSavedCredentialsAsync(_id, It.IsAny<CancellationToken>())).ReturnsAsync(true);
            var keyedPull = new Mock<IEventPullService>();
            IServiceProvider services = new ServiceCollection()
                .AddKeyedScoped("Wyze", (_, _) => keyedAuth.Object)
                .AddKeyedScoped("Wyze", (_, _) => keyedPull.Object)
                .BuildServiceProvider();

            await Sut(services).RunAsync(_id, CancellationToken.None);

            keyedAuth.Verify(a => a.RestoreFromSavedCredentialsAsync(_id, It.IsAny<CancellationToken>()), Times.Once);
            keyedPull.Verify(p => p.PullAccountEventsAsync(_id, It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()), Times.Once);
            _auth.Verify(a => a.RestoreFromSavedCredentialsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
            _pull.Verify(p => p.PullAccountEventsAsync(It.IsAny<Guid>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task RunAsync_NoKeyedServicesForTheProvider_FallsBackToTheInjectedOnes()
        {
            IServiceProvider services = new ServiceCollection().BuildServiceProvider();

            await Sut(services).RunAsync(_id, CancellationToken.None);

            _auth.Verify(a => a.RestoreFromSavedCredentialsAsync(_id, It.IsAny<CancellationToken>()), Times.Once);
            _pull.Verify(p => p.PullAccountEventsAsync(_id, It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public void ResolveWindow_AllTypeWithSinceLast_KeepsTheLegacyStart()
        {
            DateTime now = new(2026, 10, 17, 12, 0, 0, DateTimeKind.Utc);

            (DateTime? from, SyncRunOptions published) = AccountSyncRunner.ResolveWindow(new SyncRunOptions(IncludeSnapshots: true), LastAuth, fullHistory: false, now);

            Assert.Equal(LastAuth, from);
            Assert.Equal(LastAuth, published.StartUtc);
        }

        [Fact]
        public void ResolveWindow_AllTypeWithNoPriorSync_StartsWhereTheLegacySyncDoes()
        {
            (DateTime? from, _) = AccountSyncRunner.ResolveWindow(new SyncRunOptions(IncludeSnapshots: true), null, fullHistory: false, DateTime.UtcNow);

            Assert.Null(from);
        }

        [Fact]
        public void ResolveWindow_DefaultKindWithoutSnapshots_StaysTheLegacyWindow()
        {
            (DateTime? from, SyncRunOptions published) = AccountSyncRunner.ResolveWindow(
                new SyncRunOptions(RangeKind: SyncRangeKind.Days, Days: 5), LastAuth, fullHistory: false, DateTime.UtcNow);

            Assert.Equal(LastAuth, from);
            Assert.Null(published.StartUtc);
        }

        [Fact]
        public void ResolveWindow_AllHistoryRange_LooksBackOneYear()
        {
            DateTime now = new(2026, 10, 17, 12, 0, 0, DateTimeKind.Utc);

            (DateTime? from, SyncRunOptions published) = AccountSyncRunner.ResolveWindow(
                new SyncRunOptions(SyncRunKind.Events, SyncRangeKind.All), LastAuth, fullHistory: false, now);

            Assert.Equal(now.AddYears(-1), from);
            Assert.Equal(now.AddYears(-1), published.StartUtc);
            Assert.Null(published.EndUtc);
        }

        [Theory]
        [InlineData(SyncRunKind.Snapshots, "Snapshots")]
        [InlineData(SyncRunKind.Events, "Events")]
        [InlineData(SyncRunKind.Default, "Events")]
        public void SyncTypeFor_ByKind(SyncRunKind kind, string expected)
        {
            Assert.Equal(expected, ProviderSyncRunRecorder.SyncTypeFor(new SyncRunTotals(false, 0, 0, 0, 0, 0), kind));
        }

        [Fact]
        public void SyncTypeFor_NoTotals_IsUnknownEvenForSnapshots()
        {
            Assert.Equal("Unknown", ProviderSyncRunRecorder.SyncTypeFor(null, SyncRunKind.Snapshots));
        }
    }
}
