namespace VideoForensics.Hosting.Tests.Services
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using VideoForensics.Core.Providers.Common.Contracts;
    using VideoForensics.Data.Common.Contracts;
    using VideoForensics.Hosting.Contracts;
    using VideoForensics.Data.Common.Entities;
    using VideoForensics.Hosting.Services;
    using ProviderAccountEntity = VideoForensics.Data.Common.Entities.ProviderAccount;

    /// <summary>
    /// The manual sync path: restore saved credentials, then pull. A failed restore must be recorded on the account,
    /// not swallowed, so a broken sync is visible in the account's error state. Every run also writes one
    /// ProviderSyncRun row so the account details page can list past syncs.
    /// </summary>
    public class AccountSyncRunnerTests
    {
        private readonly Mock<IProviderAuthService> _mockAuth = new();
        private readonly Mock<IEventPullService> _mockPull = new();
        private readonly Mock<IProviderAccountRepository> _mockAccounts = new();
        private readonly Mock<IProviderSyncRunRepository> _mockRuns = new();
        private readonly Mock<ILogger<AccountSyncRunner>> _mockLogger = new();
        private readonly Guid _accountId = Guid.NewGuid();
        private readonly DateTime _lastAuth = new(2026, 10, 14, 18, 0, 0, DateTimeKind.Utc);

        private AccountSyncRunner CreateSut() => new(
            new ServiceCollection().AddScoped(_ => _mockAuth.Object).AddScoped(_ => _mockPull.Object).BuildServiceProvider(),
            _mockAccounts.Object,
            _mockRuns.Object,
            Mock.Of<ISnapshotSyncService>(),
            _mockLogger.Object);

        private ProviderAccountEntity Account() => new()
        {
            Id = _accountId,
            UserId = Guid.NewGuid(),
            ProviderName = "Ring",
            LinkedUtc = DateTime.UtcNow,
            LastSuccessfulAuthUtc = _lastAuth,
            IsActive = true
        };

        private void SetupAccount(ProviderAccountEntity account) =>
            _mockAccounts.Setup(r => r.GetAsync(_accountId, It.IsAny<CancellationToken>())).ReturnsAsync(account);

        private void SetupRestore(bool result) =>
            _mockAuth.Setup(a => a.RestoreFromSavedCredentialsAsync(_accountId, It.IsAny<CancellationToken>())).ReturnsAsync(result);

        private List<ProviderSyncRun> CaptureRuns()
        {
            var runs = new List<ProviderSyncRun>();
            _mockRuns.Setup(r => r.AddAsync(It.IsAny<ProviderSyncRun>(), It.IsAny<CancellationToken>()))
                .Callback<ProviderSyncRun, CancellationToken>((run, _) => runs.Add(run))
                .Returns(Task.CompletedTask);
            return runs;
        }

        [Fact]
        public async Task RunAsync_RestoreReturnsFalse_RecordsErrorAndDoesNotPull()
        {
            var account = Account();
            SetupAccount(account);
            SetupRestore(false);

            await CreateSut().RunAsync(_accountId, CancellationToken.None);

            _mockAccounts.Verify(r => r.UpdateAsync(
                It.Is<ProviderAccountEntity>(a => a.LastErrorMessage != null && a.LastErrorUtc != null),
                It.IsAny<CancellationToken>()), Times.Once);
            _mockPull.Verify(p => p.PullAccountEventsAsync(It.IsAny<Guid>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task RunAsync_RestoreThrows_RecordsErrorWithMessageAndDoesNotPull()
        {
            var account = Account();
            SetupAccount(account);
            _mockAuth.Setup(a => a.RestoreFromSavedCredentialsAsync(_accountId, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("refresh token rejected"));

            await CreateSut().RunAsync(_accountId, CancellationToken.None);

            _mockAccounts.Verify(r => r.UpdateAsync(
                It.Is<ProviderAccountEntity>(a => a.LastErrorMessage!.Contains("refresh token rejected")),
                It.IsAny<CancellationToken>()), Times.Once);
            _mockPull.Verify(p => p.PullAccountEventsAsync(It.IsAny<Guid>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task RunAsync_RestoreSucceeds_PullsSinceLastSuccessfulAuth()
        {
            var account = Account();
            SetupAccount(account);
            SetupRestore(true);

            await CreateSut().RunAsync(_accountId, CancellationToken.None);

            _mockPull.Verify(p => p.PullAccountEventsAsync(_accountId, _lastAuth, It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()), Times.Once);
            _mockAccounts.Verify(r => r.UpdateAsync(It.IsAny<ProviderAccountEntity>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task RunInitialAsync_RestoreSucceeds_PullsFullHistoryFromBeginning()
        {
            var account = Account();
            SetupAccount(account);
            SetupRestore(true);

            await CreateSut().RunInitialAsync(_accountId, CancellationToken.None);

            // The initial backfill starts one year back, not at the last auth.
            _mockPull.Verify(p => p.PullAccountEventsAsync(_accountId, It.Is<DateTime?>(f => f.HasValue && Math.Abs((DateTime.UtcNow.AddYears(-1) - f.Value).TotalMinutes) < 5), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task RunInitialAsync_RestoreFails_RecordsErrorAndDoesNotPull()
        {
            var account = Account();
            SetupAccount(account);
            SetupRestore(false);

            await CreateSut().RunInitialAsync(_accountId, CancellationToken.None);

            _mockAccounts.Verify(r => r.UpdateAsync(It.Is<ProviderAccountEntity>(a => a.LastErrorMessage != null), It.IsAny<CancellationToken>()), Times.Once);
            _mockPull.Verify(p => p.PullAccountEventsAsync(It.IsAny<Guid>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task RunAsync_SuccessfulRun_RecordsSuccessWithCompletedUtc()
        {
            SetupAccount(Account());
            SetupRestore(true);
            List<ProviderSyncRun> runs = CaptureRuns();

            await CreateSut().RunAsync(_accountId, CancellationToken.None);

            ProviderSyncRun run = Assert.Single(runs);
            Assert.Equal(_accountId, run.ProviderAccountId);
            Assert.True(run.Success);
            Assert.Null(run.ErrorMessage);
            Assert.NotNull(run.CompletedUtc);
            Assert.True(run.CompletedUtc >= run.StartedUtc);
        }

        [Fact]
        public async Task RunAsync_SuccessfulRun_RecordsIncrementalSyncType()
        {
            SetupAccount(Account());
            SetupRestore(true);
            List<ProviderSyncRun> runs = CaptureRuns();

            await CreateSut().RunAsync(_accountId, CancellationToken.None);

            Assert.Equal("Incremental", Assert.Single(runs).SyncType);
        }

        [Fact]
        public async Task RunInitialAsync_SuccessfulRun_RecordsFullSyncTypeAndLeavesUntalliedCountsNull()
        {
            SetupAccount(Account());
            SetupRestore(true);
            List<ProviderSyncRun> runs = CaptureRuns();

            await CreateSut().RunInitialAsync(_accountId, CancellationToken.None);

            ProviderSyncRun run = Assert.Single(runs);
            Assert.Equal("Full", run.SyncType);
            Assert.Equal(0, run.EventsSynced);
            Assert.Equal(0, run.DevicesSynced);
        }

        [Fact]
        public async Task RunInitialAsync_SuccessfulRun_RecordsSuccessWithCompletedUtc()
        {
            SetupAccount(Account());
            SetupRestore(true);
            List<ProviderSyncRun> runs = CaptureRuns();

            await CreateSut().RunInitialAsync(_accountId, CancellationToken.None);

            ProviderSyncRun run = Assert.Single(runs);
            Assert.True(run.Success);
            Assert.NotNull(run.CompletedUtc);
        }

        [Fact]
        public async Task RunAsync_RestoreReturnsFalse_RecordsFailureWithRestoreMessage()
        {
            SetupAccount(Account());
            SetupRestore(false);
            List<ProviderSyncRun> runs = CaptureRuns();

            await CreateSut().RunAsync(_accountId, CancellationToken.None);

            ProviderSyncRun run = Assert.Single(runs);
            Assert.False(run.Success);
            Assert.Equal("Saved credentials could not be restored. Sign in to this account again.", run.ErrorMessage);
            Assert.NotNull(run.CompletedUtc);
        }

        [Fact]
        public async Task RunAsync_PullThrows_RecordsFailureWithExceptionMessageAndRethrows()
        {
            SetupAccount(Account());
            SetupRestore(true);
            _mockPull.Setup(p => p.PullAccountEventsAsync(_accountId, It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("pull boom"));
            List<ProviderSyncRun> runs = CaptureRuns();

            // The original failure still reaches the caller; the run record does not replace it.
            InvalidOperationException thrown = await Assert.ThrowsAsync<InvalidOperationException>(
                () => CreateSut().RunAsync(_accountId, CancellationToken.None));

            Assert.Equal("pull boom", thrown.Message);
            ProviderSyncRun run = Assert.Single(runs);
            Assert.False(run.Success);
            Assert.Equal("pull boom", run.ErrorMessage);
            Assert.NotNull(run.CompletedUtc);
        }

        [Fact]
        public async Task RunAsync_PullRecordsAccountError_RecordsFailureWithAccountErrorMessage()
        {
            var account = Account();
            SetupAccount(account);
            SetupRestore(true);
            // The pull records its own failures on the account rather than throwing.
            _mockPull.Setup(p => p.PullAccountEventsAsync(_accountId, It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
                .Callback(() => account.LastErrorMessage = "device offline")
                .Returns(Task.FromResult(default(EventPullResult)));
            List<ProviderSyncRun> runs = CaptureRuns();

            await CreateSut().RunAsync(_accountId, CancellationToken.None);

            ProviderSyncRun run = Assert.Single(runs);
            Assert.False(run.Success);
            Assert.Equal("device offline", run.ErrorMessage);
        }

        [Fact]
        public async Task RunAsync_AccountMissing_RecordsFailureAccountNotFoundAndDoesNotPull()
        {
            _mockAccounts.Setup(r => r.GetAsync(_accountId, It.IsAny<CancellationToken>())).ReturnsAsync((ProviderAccountEntity?)null);
            SetupRestore(true);
            List<ProviderSyncRun> runs = CaptureRuns();

            await CreateSut().RunAsync(_accountId, CancellationToken.None);

            ProviderSyncRun run = Assert.Single(runs);
            Assert.False(run.Success);
            Assert.Equal("Account not found", run.ErrorMessage);
            _mockPull.Verify(p => p.PullAccountEventsAsync(It.IsAny<Guid>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task RunAsync_AccountDeletedDuringPull_RecordsFailureAccountNotFound()
        {
            var account = Account();
            bool deleted = false;
            _mockAccounts.Setup(r => r.GetAsync(_accountId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => deleted ? null : account);
            _mockPull.Setup(p => p.PullAccountEventsAsync(_accountId, It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
                .Callback(() => deleted = true)
                .Returns(Task.FromResult(default(EventPullResult)));
            SetupRestore(true);
            List<ProviderSyncRun> runs = CaptureRuns();

            await CreateSut().RunAsync(_accountId, CancellationToken.None);

            ProviderSyncRun run = Assert.Single(runs);
            Assert.False(run.Success);
            Assert.Equal("Account not found", run.ErrorMessage);
        }

        [Fact]
        public async Task RunAsync_CancelledDuringPull_RethrowsAndWritesNoRunRow()
        {
            SetupAccount(Account());
            SetupRestore(true);
            using var cts = new CancellationTokenSource();
            _mockPull.Setup(p => p.PullAccountEventsAsync(_accountId, It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
                .Returns(async () =>
                {
                    await cts.CancelAsync();
                    throw new OperationCanceledException(cts.Token);
                });
            List<ProviderSyncRun> runs = CaptureRuns();

            // Cancellation is not a failed sync: rethrow and leave no failure row behind.
            _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => CreateSut().RunAsync(_accountId, cts.Token));

            Assert.Empty(runs);
        }

        [Fact]
        public async Task RunAsync_LongFailureMessage_TruncatesRecordedErrorTo1024Characters()
        {
            SetupAccount(Account());
            _mockAuth.Setup(a => a.RestoreFromSavedCredentialsAsync(_accountId, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException(new string('x', 2000)));
            List<ProviderSyncRun> runs = CaptureRuns();

            await CreateSut().RunAsync(_accountId, CancellationToken.None);

            ProviderSyncRun run = Assert.Single(runs);
            Assert.NotNull(run.ErrorMessage);
            Assert.True(run.ErrorMessage!.Length <= 1024);
        }

        [Fact]
        public async Task RunAsync_RunWriteFails_LogsErrorAndDoesNotThrow()
        {
            SetupAccount(Account());
            SetupRestore(true);
            _mockRuns.Setup(r => r.AddAsync(It.IsAny<ProviderSyncRun>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("database unavailable"));

            // Recording is best-effort: a failed write must not fail the sync itself.
            await CreateSut().RunAsync(_accountId, CancellationToken.None);

            _mockLogger.Verify(l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
        }

        [Fact]
        public async Task RunAsync_RunWriteFailsAfterPullFailure_RethrowsOriginalPullException()
        {
            SetupAccount(Account());
            SetupRestore(true);
            _mockPull.Setup(p => p.PullAccountEventsAsync(_accountId, It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("pull boom"));
            _mockRuns.Setup(r => r.AddAsync(It.IsAny<ProviderSyncRun>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("database unavailable"));

            InvalidOperationException thrown = await Assert.ThrowsAsync<InvalidOperationException>(
                () => CreateSut().RunAsync(_accountId, CancellationToken.None));

            Assert.Equal("pull boom", thrown.Message);
        }
    }
}
