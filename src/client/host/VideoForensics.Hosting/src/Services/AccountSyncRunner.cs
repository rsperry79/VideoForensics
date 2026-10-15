using Microsoft.Extensions.Logging;
using VideoForensics.Core.Providers.Common.Contracts;
using VideoForensics.Data.Common.Contracts;

namespace VideoForensics.Hosting.Services
{
    /// <summary>Runs a manual sync for one account: restore its saved credentials, then pull.</summary>
    public interface IAccountSyncRunner
    {
        /// <summary>Restores the account's session and pulls its inventory and events. Failures are recorded on the account.</summary>
        Task RunAsync(Guid accountId, CancellationToken cancellationToken);

        /// <summary>
        /// First sync after an account is added: restores the session, discovers locations and devices, and pulls the
        /// provider's full event history (no start date). Failures are recorded on the account.
        /// </summary>
        Task RunInitialAsync(Guid accountId, CancellationToken cancellationToken);
    }

    /// <inheritdoc />
    public class AccountSyncRunner : IAccountSyncRunner
    {
        private readonly IProviderAuthService _authService;
        private readonly IEventPullService _eventPullService;
        private readonly IProviderAccountRepository _accounts;
        private readonly ILogger<AccountSyncRunner> _logger;

        /// <summary>Creates the runner.</summary>
        public AccountSyncRunner(
            IProviderAuthService authService,
            IEventPullService eventPullService,
            IProviderAccountRepository accounts,
            ILogger<AccountSyncRunner> logger)
        {
            _authService = authService;
            _eventPullService = eventPullService;
            _accounts = accounts;
            _logger = logger;
        }

        /// <inheritdoc />
        public Task RunAsync(Guid accountId, CancellationToken cancellationToken) =>
            SyncAsync(accountId, fullHistory: false, cancellationToken);

        /// <inheritdoc />
        public Task RunInitialAsync(Guid accountId, CancellationToken cancellationToken) =>
            SyncAsync(accountId, fullHistory: true, cancellationToken);

        private async Task SyncAsync(Guid accountId, bool fullHistory, CancellationToken cancellationToken)
        {
            // Provider sessions are in-memory: after a server restart the account must be restored from its saved
            // credentials before discovery or pulls can reach the provider. A failed restore is recorded, not swallowed.
            try
            {
                if (!await _authService.RestoreFromSavedCredentialsAsync(accountId, cancellationToken))
                {
                    await RecordErrorAsync(accountId, "Saved credentials could not be restored. Sign in to this account again.", cancellationToken);
                    return;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Restoring saved credentials failed before sync");
                await RecordErrorAsync(accountId, $"Restoring saved credentials failed: {ex.Message}", cancellationToken);
                return;
            }

            // The pull records its own failures on the account. A null start pulls the provider's full history.
            DateTime? from = fullHistory
                ? null
                : (await _accounts.GetAsync(accountId, cancellationToken))?.LastSuccessfulAuthUtc;
            await _eventPullService.PullAccountEventsAsync(accountId, from, cancellationToken);
        }

        private async Task RecordErrorAsync(Guid accountId, string message, CancellationToken cancellationToken)
        {
            var account = await _accounts.GetAsync(accountId, cancellationToken);
            if (account is null)
            {
                return;
            }

            account.LastErrorMessage = message;
            account.LastErrorUtc = DateTime.UtcNow;
            await _accounts.UpdateAsync(account, cancellationToken);
        }
    }
}
