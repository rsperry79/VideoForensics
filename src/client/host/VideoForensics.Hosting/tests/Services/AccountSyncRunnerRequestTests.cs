namespace VideoForensics.Hosting.Tests.Services
{
    using Microsoft.Extensions.Logging;
    using VideoForensics.Core.Providers.Common.Contracts;
    using VideoForensics.Data.Common.Contracts;
    using VideoForensics.Data.Common.Entities;
    using VideoForensics.Hosting.Services;
    using ProviderAccountEntity = VideoForensics.Data.Common.Entities.ProviderAccount;

    /// <summary>The manual sync honours the sync type and date range the account page asked for.</summary>
    public class AccountSyncRunnerRequestTests
    {
        private readonly Mock<IProviderAuthService> _auth = new();
        private readonly Mock<IEventPullService> _pull = new();
        private readonly Mock<IProviderAccountRepository> _accounts = new();
        private readonly Mock<IProviderSyncRunRepository> _runs = new();
        private readonly Guid _id = Guid.NewGuid();
        private readonly List<ProviderSyncRun> _recorded = new();

        public AccountSyncRunnerRequestTests()
        {
            _accounts.Setup(r => r.GetAsync(_id, It.IsAny<CancellationToken>())).ReturnsAsync(new ProviderAccountEntity
            {
                Id = _id,
                UserId = Guid.NewGuid(),
                ProviderName = "Ring",
                LinkedUtc = DateTime.UtcNow,
                LastSuccessfulAuthUtc = new DateTime(2026, 10, 14, 0, 0, 0, DateTimeKind.Utc),
                IsActive = true
            });
            _auth.Setup(a => a.RestoreFromSavedCredentialsAsync(_id, It.IsAny<CancellationToken>())).ReturnsAsync(true);
            _runs.Setup(r => r.AddAsync(It.IsAny<ProviderSyncRun>(), It.IsAny<CancellationToken>()))
                .Callback<ProviderSyncRun, CancellationToken>((run, _) => _recorded.Add(run))
                .Returns(Task.CompletedTask);
        }

        private AccountSyncRunner Sut() => new(_auth.Object, _pull.Object, _accounts.Object, _runs.Object, Mock.Of<ILogger<AccountSyncRunner>>());

        [Fact]
        public async Task RunAsync_EventsWithFromDate_PullsFromThatDate()
        {
            var from = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);

            await Sut().RunAsync(_id, new AccountSyncRequest(AccountSyncType.Events, from, null, false), CancellationToken.None);

            _pull.Verify(p => p.PullAccountEventsAsync(_id, from, It.IsAny<CancellationToken>()), Times.Once);
            Assert.Equal("Incremental", Assert.Single(_recorded).SyncType);
        }

        [Fact]
        public async Task RunAsync_AllHistory_PullsFromBeginningAndRecordsFull()
        {
            await Sut().RunAsync(_id, new AccountSyncRequest(AccountSyncType.All, null, null, true), CancellationToken.None);

            _pull.Verify(p => p.PullAccountEventsAsync(_id, null, It.IsAny<CancellationToken>()), Times.Once);
            Assert.Equal("Full", Assert.Single(_recorded).SyncType);
        }

        [Fact]
        public async Task RunAsync_Snapshots_SkipsEventPullAndRecordsSnapshots()
        {
            await Sut().RunAsync(_id, new AccountSyncRequest(AccountSyncType.Snapshots, DateTime.UtcNow.AddDays(-7), null, false), CancellationToken.None);

            _pull.Verify(p => p.PullAccountEventsAsync(It.IsAny<Guid>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()), Times.Never);
            Assert.Equal("Snapshots", Assert.Single(_recorded).SyncType);
        }
    }
}
