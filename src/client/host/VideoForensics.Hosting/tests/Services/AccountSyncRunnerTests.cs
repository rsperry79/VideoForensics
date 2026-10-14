namespace VideoForensics.Hosting.Tests.Services
{
    using VideoForensics.Core.Providers.Common.Contracts;
    using VideoForensics.Hosting.Services;
    using ProviderAccountEntity = VideoForensics.Data.Common.Entities.ProviderAccount;

    /// <summary>
    /// The manual sync path: restore saved credentials, then pull. A failed restore must be recorded on the account,
    /// not swallowed, so a broken sync is visible in the account's error state.
    /// </summary>
    public class AccountSyncRunnerTests
    {
        private readonly Mock<IProviderAuthService> _mockAuth = new();
        private readonly Mock<IEventPullService> _mockPull = new();
        private readonly Mock<IProviderAccountRepository> _mockAccounts = new();
        private readonly Guid _accountId = Guid.NewGuid();
        private readonly DateTime _lastAuth = new(2026, 10, 14, 18, 0, 0, DateTimeKind.Utc);

        private AccountSyncRunner CreateSut() => new(
            _mockAuth.Object,
            _mockPull.Object,
            _mockAccounts.Object,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<AccountSyncRunner>.Instance);

        private ProviderAccountEntity Account() => new()
        {
            Id = _accountId,
            UserId = Guid.NewGuid(),
            ProviderName = "Ring",
            LinkedUtc = DateTime.UtcNow,
            LastSuccessfulAuthUtc = _lastAuth,
            IsActive = true
        };

        [Fact]
        public async Task RunAsync_RestoreReturnsFalse_RecordsErrorAndDoesNotPull()
        {
            var account = Account();
            _mockAccounts.Setup(r => r.GetAsync(_accountId, It.IsAny<CancellationToken>())).ReturnsAsync(account);
            _mockAuth.Setup(a => a.RestoreFromSavedCredentialsAsync(_accountId, It.IsAny<CancellationToken>())).ReturnsAsync(false);

            await CreateSut().RunAsync(_accountId, CancellationToken.None);

            _mockAccounts.Verify(r => r.UpdateAsync(
                It.Is<ProviderAccountEntity>(a => a.LastErrorMessage != null && a.LastErrorUtc != null),
                It.IsAny<CancellationToken>()), Times.Once);
            _mockPull.Verify(p => p.PullAccountEventsAsync(It.IsAny<Guid>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task RunAsync_RestoreThrows_RecordsErrorWithMessageAndDoesNotPull()
        {
            var account = Account();
            _mockAccounts.Setup(r => r.GetAsync(_accountId, It.IsAny<CancellationToken>())).ReturnsAsync(account);
            _mockAuth.Setup(a => a.RestoreFromSavedCredentialsAsync(_accountId, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("refresh token rejected"));

            await CreateSut().RunAsync(_accountId, CancellationToken.None);

            _mockAccounts.Verify(r => r.UpdateAsync(
                It.Is<ProviderAccountEntity>(a => a.LastErrorMessage!.Contains("refresh token rejected")),
                It.IsAny<CancellationToken>()), Times.Once);
            _mockPull.Verify(p => p.PullAccountEventsAsync(It.IsAny<Guid>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task RunAsync_RestoreSucceeds_PullsSinceLastSuccessfulAuth()
        {
            var account = Account();
            _mockAccounts.Setup(r => r.GetAsync(_accountId, It.IsAny<CancellationToken>())).ReturnsAsync(account);
            _mockAuth.Setup(a => a.RestoreFromSavedCredentialsAsync(_accountId, It.IsAny<CancellationToken>())).ReturnsAsync(true);

            await CreateSut().RunAsync(_accountId, CancellationToken.None);

            _mockPull.Verify(p => p.PullAccountEventsAsync(_accountId, _lastAuth, It.IsAny<CancellationToken>()), Times.Once);
            _mockAccounts.Verify(r => r.UpdateAsync(It.IsAny<ProviderAccountEntity>(), It.IsAny<CancellationToken>()), Times.Never);
        }
    }
}
