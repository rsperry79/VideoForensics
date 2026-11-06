using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using VideoForensics.Core.Providers.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Client.Hosting.Contracts;

using ProviderAccountEntity = VideoForensics.Data.Common.Entities.ProviderAccount;

namespace VideoForensics.Client.Hosting.Tests.Services
{
    /// <summary>The manual sync honours the sync type and date range the account page asked for.</summary>
    public class AccountSyncRunnerRequestTests
    {
        private readonly Mock<IProviderAuthService> _auth = new();
        private readonly Mock<IEventPullService> _pull = new();
        private readonly Mock<IProviderAccountRepository> _accounts = new();
        private readonly Mock<IProviderSyncRunRepository> _runs = new();
        private readonly Mock<ISnapshotSyncService> _snapshots = new();
        private readonly Guid _id = Guid.NewGuid();
        private readonly List<ProviderSyncRun> _recorded = [];

        public AccountSyncRunnerRequestTests()
        {
            _ = _accounts.Setup(r => r.GetAsync(_id, It.IsAny<CancellationToken>())).ReturnsAsync(new ProviderAccountEntity
            {
                Id = _id,
                UserId = Guid.NewGuid(),
                ProviderName = "Ring",
                LinkedUtc = DateTime.UtcNow,
                LastSuccessfulAuthUtc = new DateTime(2026, 10, 14, 0, 0, 0, DateTimeKind.Utc),
                IsActive = true
            });
            _ = _auth.Setup(a => a.RestoreFromSavedCredentialsAsync(_id, It.IsAny<CancellationToken>())).ReturnsAsync(true);
            _ = _runs.Setup(r => r.AddAsync(It.IsAny<ProviderSyncRun>(), It.IsAny<CancellationToken>()))
                .Callback<ProviderSyncRun, CancellationToken>((run, _) => _recorded.Add(run))
                .Returns(Task.CompletedTask);
        }

        private static IServiceProvider Unkeyed(IProviderAuthService auth, IEventPullService pull)
        {
            return new ServiceCollection().AddScoped(_ => Mock.Of<IInventorySyncService>()).AddScoped(_ => auth).AddScoped(_ => pull).BuildServiceProvider();
        }

        private AccountSyncRunner Sut()
        {
            return new(Unkeyed(_auth.Object, _pull.Object), _accounts.Object, _runs.Object, _snapshots.Object, Mock.Of<ILogger<AccountSyncRunner>>(), new SyncActivityLog());
        }

        [Fact]
        public async Task RunAsync_EventsWithFromDate_PullsFromThatDate()
        {
            DateTime from = DateTime.UtcNow.AddDays(-5);

            await Sut().RunAsync(_id, new AccountSyncRequest(AccountSyncType.Events, from, null, false), CancellationToken.None);

            _pull.Verify(p => p.PullAccountEventsAsync(_id, from, It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()), Times.Once);
            Assert.Equal("Incremental", Assert.Single(_recorded).SyncType);
        }

        [Fact]
        public async Task RunAsync_AllHistory_PullsBackOneYearAndRecordsFull()
        {
            DateTime before = DateTime.UtcNow;
            await Sut().RunAsync(_id, new AccountSyncRequest(AccountSyncType.All, null, null, true), CancellationToken.None);
            DateTime after = DateTime.UtcNow;

            // The first weekly window starts a year back; only that window starts at this point.
            _pull.Verify(p => p.PullAccountEventsAsync(
                _id,
                It.Is<DateTime?>(from => from.HasValue && from.Value >= before.AddYears(-1) && from.Value <= after.AddYears(-1)),
                It.IsAny<DateTime?>(),
                It.IsAny<CancellationToken>()), Times.Once);
            Assert.Equal("Full", Assert.Single(_recorded).SyncType);
        }

        [Fact]
        public async Task RunAsync_Snapshots_SkipsEventPullAndRecordsSnapshots()
        {
            await Sut().RunAsync(_id, new AccountSyncRequest(AccountSyncType.Snapshots, DateTime.UtcNow.AddDays(-7), null, false), CancellationToken.None);

            _pull.Verify(p => p.PullAccountEventsAsync(It.IsAny<Guid>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()), Times.Never);
            Assert.Equal("Snapshots", Assert.Single(_recorded).SyncType);
        }
    }
}
