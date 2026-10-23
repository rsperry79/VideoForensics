namespace VideoForensics.Hosting.Tests.Services
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using VideoForensics.Hosting.Contracts;
    using VideoForensics.Core.Providers.Common.Contracts;
    using VideoForensics.Data.Common.Entities;
    using ProviderAccountEntity = VideoForensics.Data.Common.Entities.ProviderAccount;

    /// <summary>The sync run row carries the counts the event pull reported.</summary>
    public class AccountSyncRunnerCountsTests
    {
        private readonly Mock<IProviderAuthService> _auth = new();
        private readonly Mock<IEventPullService> _pull = new();
        private readonly Mock<IProviderAccountRepository> _accounts = new();
        private readonly Mock<IProviderSyncRunRepository> _runs = new();
        private readonly Mock<ISnapshotSyncService> _snapshots = new();
        private readonly Guid _id = Guid.NewGuid();
        private readonly List<ProviderSyncRun> _recorded = new();
        private readonly ProviderAccountEntity _account;

        public AccountSyncRunnerCountsTests()
        {
            _account = new ProviderAccountEntity
            {
                Id = _id,
                UserId = Guid.NewGuid(),
                ProviderName = "Ring",
                LinkedUtc = DateTime.UtcNow,
                IsActive = true
            };
            _accounts.Setup(r => r.GetAsync(_id, It.IsAny<CancellationToken>())).ReturnsAsync(_account);
            _auth.Setup(a => a.RestoreFromSavedCredentialsAsync(_id, It.IsAny<CancellationToken>())).ReturnsAsync(true);
            _runs.Setup(r => r.AddAsync(It.IsAny<ProviderSyncRun>(), It.IsAny<CancellationToken>()))
                .Callback<ProviderSyncRun, CancellationToken>((run, _) => _recorded.Add(run))
                .Returns(Task.CompletedTask);
        }

        private static IServiceProvider Unkeyed(IProviderAuthService auth, IEventPullService pull) =>
            new ServiceCollection().AddScoped(_ => auth).AddScoped(_ => pull).BuildServiceProvider();

        private AccountSyncRunner Sut() => new(Unkeyed(_auth.Object, _pull.Object), _accounts.Object, _runs.Object, _snapshots.Object, Mock.Of<ILogger<AccountSyncRunner>>(), new SyncActivityLog());

        [Fact]
        public async Task RunAsync_PullReportsCounts_RecordsCountsOnRun()
        {
            _pull.Setup(p => p.PullAccountEventsAsync(_id, It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new EventPullResult(DevicesSynced: 3, LocationsSynced: 2, EventsSynced: 40, ConfigAreasSynced: 3, FeedsFailed: 0));

            await Sut().RunAsync(_id, CancellationToken.None);

            ProviderSyncRun run = Assert.Single(_recorded);
            Assert.True(run.Success);
            Assert.Equal(3, run.DevicesSynced);
            Assert.Equal(2, run.LocationsSynced);
            Assert.Equal(40, run.EventsSynced);
            Assert.Equal(3, run.ConfigAreasSynced);
            Assert.Equal(0, run.FeedsFailed);
        }

        [Fact]
        public async Task RunAsync_PullFailsPartially_RecordsPartialCountsAndFeedsFailed()
        {
            _pull.Setup(p => p.PullAccountEventsAsync(_id, It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
                .Callback(() => _account.LastErrorMessage = "Device Back Yard: boom")
                .ReturnsAsync(new EventPullResult(DevicesSynced: 2, LocationsSynced: 1, EventsSynced: 5, ConfigAreasSynced: 1, FeedsFailed: 1));

            await Sut().RunAsync(_id, CancellationToken.None);

            ProviderSyncRun run = Assert.Single(_recorded);
            Assert.False(run.Success);
            Assert.Equal(5, run.EventsSynced);
            Assert.Equal(1, run.FeedsFailed);
        }

        [Fact]
        public async Task RunAsync_RestoreFails_RecordsNoCounts()
        {
            _auth.Setup(a => a.RestoreFromSavedCredentialsAsync(_id, It.IsAny<CancellationToken>())).ReturnsAsync(false);

            await Sut().RunAsync(_id, CancellationToken.None);

            ProviderSyncRun run = Assert.Single(_recorded);
            Assert.Null(run.EventsSynced);
            Assert.Null(run.FeedsFailed);
        }

        [Fact]
        public async Task RunAsync_RequestHasToDate_PassesToDateToPull()
        {
            var from = DateTime.UtcNow.AddDays(-5);
            var to = DateTime.UtcNow.AddDays(-1);

            await Sut().RunAsync(_id, new AccountSyncRequest(AccountSyncType.Events, from, to, false), CancellationToken.None);

            _pull.Verify(p => p.PullAccountEventsAsync(_id, from, to, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task RunAsync_RequestHasNoToDate_PassesNullToPull()
        {
            // All history backfills a month at a time from a year ago, so the first window is a year back and the next is a month on.
            await Sut().RunAsync(_id, new AccountSyncRequest(AccountSyncType.Events, null, null, true), CancellationToken.None);

            _pull.Verify(p => p.PullAccountEventsAsync(_id, It.Is<DateTime?>(f => f.HasValue && Math.Abs((DateTime.UtcNow.AddYears(-1) - f.Value).TotalMinutes) < 5), It.Is<DateTime?>(t => t.HasValue), It.IsAny<CancellationToken>()), Times.Once);
            _pull.Verify(p => p.PullAccountEventsAsync(_id, It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()), Times.Exactly(12));
        }

        [Fact]
        public async Task RunAsync_SnapshotsType_SyncsSnapshotsForWindowAndSkipsEventPull()
        {
            var from = DateTime.UtcNow.AddDays(-5);
            var to = DateTime.UtcNow.AddDays(-1);
            _snapshots.Setup(s => s.SyncAccountSnapshotsAsync(_id, from, to, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new SnapshotSyncResult(DevicesAttempted: 3, DevicesSucceeded: 3, SnapshotsSaved: 3, DevicesFailed: 0, ErrorMessage: null));

            await Sut().RunAsync(_id, new AccountSyncRequest(AccountSyncType.Snapshots, from, to, false), CancellationToken.None);

            _snapshots.Verify(s => s.SyncAccountSnapshotsAsync(_id, from, to, It.IsAny<CancellationToken>()), Times.Once);
            _pull.Verify(p => p.PullAccountEventsAsync(It.IsAny<Guid>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()), Times.Never);
            ProviderSyncRun run = Assert.Single(_recorded);
            Assert.True(run.Success);
            Assert.Equal("Snapshots", run.SyncType);
            Assert.Equal(3, run.SnapshotsSynced);
            Assert.Null(run.DevicesSynced);
            Assert.Null(run.FeedsFailed);
        }

        [Fact]
        public async Task RunAsync_SnapshotsTypeWithFailedDevice_RecordsFailureAndCounts()
        {
            _snapshots.Setup(s => s.SyncAccountSnapshotsAsync(_id, It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new SnapshotSyncResult(DevicesAttempted: 2, DevicesSucceeded: 1, SnapshotsSaved: 1, DevicesFailed: 1, ErrorMessage: "1 of 2 device(s) failed: no snapshot"));

            await Sut().RunAsync(_id, new AccountSyncRequest(AccountSyncType.Snapshots, null, null, false), CancellationToken.None);

            ProviderSyncRun run = Assert.Single(_recorded);
            Assert.False(run.Success);
            Assert.Equal("1 of 2 device(s) failed: no snapshot", run.ErrorMessage);
            Assert.Equal(1, run.SnapshotsSynced);
            Assert.Null(run.DevicesSynced);
            Assert.Null(run.FeedsFailed);
        }

        [Fact]
        public async Task RunAsync_AllType_PullsEventsThenSnapshotsAndRecordsBothCounts()
        {
            var from = DateTime.UtcNow.AddDays(-5);
            var to = DateTime.UtcNow.AddDays(-1);
            var order = new List<string>();
            _pull.Setup(p => p.PullAccountEventsAsync(_id, from, to, It.IsAny<CancellationToken>()))
                .Callback(() => order.Add("events"))
                .ReturnsAsync(new EventPullResult(DevicesSynced: 3, LocationsSynced: 2, EventsSynced: 40, ConfigAreasSynced: 3, FeedsFailed: 1));
            _snapshots.Setup(s => s.SyncAccountSnapshotsAsync(_id, from, to, It.IsAny<CancellationToken>()))
                .Callback(() => order.Add("snapshots"))
                .ReturnsAsync(new SnapshotSyncResult(DevicesAttempted: 3, DevicesSucceeded: 3, SnapshotsSaved: 3, DevicesFailed: 0, ErrorMessage: null));

            await Sut().RunAsync(_id, new AccountSyncRequest(AccountSyncType.All, from, to, false), CancellationToken.None);

            Assert.Equal(new[] { "events", "snapshots" }, order);
            ProviderSyncRun run = Assert.Single(_recorded);
            Assert.True(run.Success);
            Assert.Equal("Incremental", run.SyncType);
            Assert.Equal(3, run.DevicesSynced);
            Assert.Equal(40, run.EventsSynced);
            Assert.Equal(1, run.FeedsFailed);
            Assert.Equal(3, run.SnapshotsSynced);
        }

        [Fact]
        public async Task RunAsync_AllTypeAllHistory_RecordsFullWithBothCounts()
        {
            _pull.Setup(p => p.PullAccountEventsAsync(_id, It.Is<DateTime?>(f => f.HasValue && Math.Abs((DateTime.UtcNow.AddMonths(-1) - f.Value).TotalMinutes) < 5), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new EventPullResult(DevicesSynced: 2, EventsSynced: 9));
            _snapshots.Setup(s => s.SyncAccountSnapshotsAsync(_id, It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new SnapshotSyncResult(DevicesAttempted: 2, DevicesSucceeded: 2, SnapshotsSaved: 2));

            await Sut().RunAsync(_id, new AccountSyncRequest(AccountSyncType.All, null, null, true), CancellationToken.None);

            ProviderSyncRun run = Assert.Single(_recorded);
            Assert.Equal("Full", run.SyncType);
            Assert.Equal(9, run.EventsSynced);
            Assert.Equal(2, run.SnapshotsSynced);
        }

        [Fact]
        public async Task RunAsync_AllTypeSnapshotsFail_RecordsFailureAndKeepsEventCounts()
        {
            _pull.Setup(p => p.PullAccountEventsAsync(_id, It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new EventPullResult(DevicesSynced: 2, EventsSynced: 9));
            _snapshots.Setup(s => s.SyncAccountSnapshotsAsync(_id, It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new SnapshotSyncResult(DevicesAttempted: 2, DevicesSucceeded: 1, SnapshotsSaved: 1, DevicesFailed: 1, ErrorMessage: "1 of 2 device(s) failed: no snapshot"));

            await Sut().RunAsync(_id, new AccountSyncRequest(AccountSyncType.All, null, null, false), CancellationToken.None);

            ProviderSyncRun run = Assert.Single(_recorded);
            Assert.False(run.Success);
            Assert.Contains("no snapshot", run.ErrorMessage);
            Assert.Equal(9, run.EventsSynced);
            Assert.Equal(1, run.SnapshotsSynced);
        }

        [Fact]
        public async Task RunAsync_AllTypeEventPullFails_StillSyncsSnapshotsAndKeepsEventError()
        {
            _pull.Setup(p => p.PullAccountEventsAsync(_id, It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
                .Callback(() => _account.LastErrorMessage = "Device Back Yard: boom")
                .ReturnsAsync(new EventPullResult(DevicesSynced: 1, EventsSynced: 2, FeedsFailed: 1));
            _snapshots.Setup(s => s.SyncAccountSnapshotsAsync(_id, It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new SnapshotSyncResult(DevicesAttempted: 1, DevicesSucceeded: 1, SnapshotsSaved: 1));

            await Sut().RunAsync(_id, new AccountSyncRequest(AccountSyncType.All, null, null, false), CancellationToken.None);

            ProviderSyncRun run = Assert.Single(_recorded);
            Assert.False(run.Success);
            Assert.Contains("Back Yard", run.ErrorMessage);
            Assert.Equal(1, run.SnapshotsSynced);
        }

        [Fact]
        public async Task RunAsync_EventsType_RecordsNoSnapshotCount()
        {
            _pull.Setup(p => p.PullAccountEventsAsync(_id, It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new EventPullResult(DevicesSynced: 1, EventsSynced: 2));

            await Sut().RunAsync(_id, new AccountSyncRequest(AccountSyncType.Events, null, null, false), CancellationToken.None);

            Assert.Null(Assert.Single(_recorded).SnapshotsSynced);
        }

        [Fact]
        public async Task RunAsync_EventsType_DoesNotSyncSnapshots()
        {
            await Sut().RunAsync(_id, new AccountSyncRequest(AccountSyncType.Events, null, null, false), CancellationToken.None);

            _snapshots.Verify(s => s.SyncAccountSnapshotsAsync(It.IsAny<Guid>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task RunAsync_TwoProvidersRegisteredKeyed_RestoresAndPullsThroughTheAccountsProvider()
        {
            _account.ProviderName = "Wyze";
            var ringAuth = new Mock<IProviderAuthService>();
            var ringPull = new Mock<IEventPullService>();
            var wyzeAuth = new Mock<IProviderAuthService>();
            var wyzePull = new Mock<IEventPullService>();
            wyzeAuth.Setup(a => a.RestoreFromSavedCredentialsAsync(_id, It.IsAny<CancellationToken>())).ReturnsAsync(true);

            // Mirrors AddVideoForensicsMultiProviderServices: keyed per provider, with the unkeyed default being whichever registered last.
            var services = new ServiceCollection();
            services.AddKeyedScoped("Wyze", (_, _) => wyzeAuth.Object);
            services.AddKeyedScoped("Wyze", (_, _) => wyzePull.Object);
            services.AddKeyedScoped("Ring", (_, _) => ringAuth.Object);
            services.AddKeyedScoped("Ring", (_, _) => ringPull.Object);
            services.AddScoped(_ => ringAuth.Object);
            services.AddScoped(_ => ringPull.Object);
            var sut = new AccountSyncRunner(services.BuildServiceProvider(), _accounts.Object, _runs.Object, _snapshots.Object, Mock.Of<ILogger<AccountSyncRunner>>(), new SyncActivityLog());

            await sut.RunAsync(_id, CancellationToken.None);

            wyzeAuth.Verify(a => a.RestoreFromSavedCredentialsAsync(_id, It.IsAny<CancellationToken>()), Times.Once);
            wyzePull.Verify(p => p.PullAccountEventsAsync(_id, It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()), Times.Once);
            ringAuth.Verify(a => a.RestoreFromSavedCredentialsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
            ringPull.Verify(p => p.PullAccountEventsAsync(It.IsAny<Guid>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task RunAsync_NoKeyedServiceForProvider_FallsBackToUnkeyedServices()
        {
            _account.ProviderName = "Uniview";

            await Sut().RunAsync(_id, CancellationToken.None);

            _auth.Verify(a => a.RestoreFromSavedCredentialsAsync(_id, It.IsAny<CancellationToken>()), Times.Once);
            _pull.Verify(p => p.PullAccountEventsAsync(_id, It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}
