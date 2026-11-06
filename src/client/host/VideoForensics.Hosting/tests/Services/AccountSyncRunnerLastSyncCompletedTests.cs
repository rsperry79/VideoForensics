using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using VideoForensics.Core.Providers.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Client.Hosting.Contracts;

using ProviderAccountEntity = VideoForensics.Data.Common.Entities.ProviderAccount;

namespace VideoForensics.Client.Hosting.Tests.Services
{
    /// <summary>
    /// A successful sync records when it completed, for the account's Last Download display. It must never touch
    /// LastDownloadTimeUtc: that field is the legacy video-download watermark, and moving it past videos that were never
    /// downloaded would make the next video download skip them.
    /// </summary>
    public class AccountSyncRunnerLastSyncCompletedTests
    {
        private readonly Mock<IProviderAuthService> _mockAuth = new();
        private readonly Mock<IEventPullService> _mockPull = new();
        private readonly Mock<IProviderAccountRepository> _mockAccounts = new();
        private readonly Mock<IProviderSyncRunRepository> _mockRuns = new();
        private readonly Mock<ILogger<AccountSyncRunner>> _mockLogger = new();
        private readonly SyncActivityLog _activity = new();
        private readonly Guid _accountId = Guid.NewGuid();
        private readonly DateTime _legacyWatermark = new(2026, 10, 10, 9, 0, 0, DateTimeKind.Utc);

        private AccountSyncRunner CreateSut()
        {
            return new(
            new ServiceCollection().AddScoped(_ => Mock.Of<IInventorySyncService>()).AddScoped(_ => _mockAuth.Object).AddScoped(_ => _mockPull.Object).BuildServiceProvider(),
            _mockAccounts.Object,
            _mockRuns.Object,
            Mock.Of<ISnapshotSyncService>(),
            _mockLogger.Object,
            _activity);
        }

        private ProviderAccountEntity Account()
        {
            return new()
            {
                Id = _accountId,
                UserId = Guid.NewGuid(),
                ProviderName = "Ring",
                LinkedUtc = DateTime.UtcNow,
                LastSuccessfulAuthUtc = new DateTime(2026, 10, 14, 18, 0, 0, DateTimeKind.Utc),
                LastDownloadTimeUtc = _legacyWatermark,
                IsActive = true
            };
        }

        private void SetupRestoreOk()
        {
            _ = _mockAuth.Setup(a => a.RestoreFromSavedCredentialsAsync(_accountId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
            _ = _mockRuns.Setup(r => r.AddAsync(It.IsAny<ProviderSyncRun>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        }

        [Fact]
        public async Task RunAsync_SuccessfulSync_RecordsLastSyncCompletedAndLeavesLastDownloadUnchanged()
        {
            ProviderAccountEntity account = Account();
            _ = _mockAccounts.Setup(r => r.GetAsync(_accountId, It.IsAny<CancellationToken>())).ReturnsAsync(account);
            SetupRestoreOk();
            _ = _mockPull.Setup(p => p.PullAccountEventsAsync(_accountId, It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new EventPullResult(DevicesSynced: 1, LocationsSynced: 1, EventsSynced: 4, ConfigAreasSynced: 1));

            DateTime before = DateTime.UtcNow;
            await CreateSut().RunAsync(_accountId, CancellationToken.None);

            _ = Assert.NotNull(account.LastSyncCompletedUtc);
            Assert.True(account.LastSyncCompletedUtc >= before);
            Assert.Equal(_legacyWatermark, account.LastDownloadTimeUtc);
        }

        [Fact]
        public async Task RunAsync_PullRecordsError_DoesNotRecordLastSyncCompleted()
        {
            ProviderAccountEntity account = Account();
            _ = _mockAccounts.Setup(r => r.GetAsync(_accountId, It.IsAny<CancellationToken>())).ReturnsAsync(account);
            SetupRestoreOk();
            _ = _mockPull.Setup(p => p.PullAccountEventsAsync(_accountId, It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
                .Callback(() => account.LastErrorMessage = "feed failed")
                .ReturnsAsync(new EventPullResult(DevicesSynced: 1, LocationsSynced: 1, EventsSynced: 0, ConfigAreasSynced: 0, FeedsFailed: 1));

            await CreateSut().RunAsync(_accountId, CancellationToken.None);

            Assert.Null(account.LastSyncCompletedUtc);
            Assert.Equal(_legacyWatermark, account.LastDownloadTimeUtc);
        }
    }
}
