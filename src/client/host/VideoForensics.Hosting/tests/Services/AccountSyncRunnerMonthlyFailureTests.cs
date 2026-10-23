using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using VideoForensics.Core.Providers.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Client.Hosting.Contracts;

using ProviderAccountEntity = VideoForensics.Data.Common.Entities.ProviderAccount;

namespace VideoForensics.Client.Hosting.Tests.Services
{
    /// <summary>
    /// A failed backfill month must not be hidden by a later clean month: the sync stays failed and the account is not marked complete.
    /// </summary>
    public class AccountSyncRunnerMonthlyFailureTests
    {
        private readonly Mock<IProviderAuthService> _mockAuth = new();
        private readonly Mock<IEventPullService> _mockPull = new();
        private readonly Mock<IProviderAccountRepository> _mockAccounts = new();
        private readonly Mock<IProviderSyncRunRepository> _mockRuns = new();
        private readonly Mock<ILogger<AccountSyncRunner>> _mockLogger = new();
        private readonly SyncActivityLog _activity = new();
        private readonly Guid _accountId = Guid.NewGuid();

        private AccountSyncRunner CreateSut()
        {
            return new(
            new ServiceCollection().AddScoped(_ => _mockAuth.Object).AddScoped(_ => _mockPull.Object).BuildServiceProvider(),
            _mockAccounts.Object,
            _mockRuns.Object,
            Mock.Of<ISnapshotSyncService>(),
            _mockLogger.Object,
            _activity);
        }

        [Fact]
        public async Task RunInitialAsync_MonthFailsThenLaterMonthSucceeds_RecordsFailureAndDoesNotMarkComplete()
        {
            var account = new ProviderAccountEntity { Id = _accountId, UserId = Guid.NewGuid(), ProviderName = "Ring", LinkedUtc = DateTime.UtcNow, IsActive = true };
            _ = _mockAccounts.Setup(r => r.GetAsync(_accountId, It.IsAny<CancellationToken>())).ReturnsAsync(account);
            _ = _mockAuth.Setup(a => a.RestoreFromSavedCredentialsAsync(_accountId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
            var runs = new List<ProviderSyncRun>();
            _ = _mockRuns.Setup(r => r.AddAsync(It.IsAny<ProviderSyncRun>(), It.IsAny<CancellationToken>()))
                .Callback<ProviderSyncRun, CancellationToken>((run, _) => runs.Add(run))
                .Returns(Task.CompletedTask);

            int calls = 0;
            _ = _mockPull.Setup(p => p.PullAccountEventsAsync(_accountId, It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() =>
                {
                    calls++;
                    // The third month fails and records its error; the fourth succeeds and clears it, as the real pull does.
                    account.LastErrorMessage = calls == 3 ? "feed failed" : null;
                    return new EventPullResult(DevicesSynced: 1, LocationsSynced: 1, EventsSynced: 0, ConfigAreasSynced: 0, FeedsFailed: calls == 3 ? 1 : 0);
                });

            await CreateSut().RunInitialAsync(_accountId, CancellationToken.None);

            Assert.Equal(12, calls);
            ProviderSyncRun run = Assert.Single(runs);
            Assert.False(run.Success);
            Assert.Equal("feed failed", run.ErrorMessage);
            Assert.Null(account.LastSyncCompletedUtc);
        }
    }
}
