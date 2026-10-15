using Microsoft.Extensions.Logging;
using VideoForensics.Core.Providers.Common.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;

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

    /// <summary>
    /// Sync-outcome rules shared by manual and scheduled syncs, so every sync writes its ProviderSyncRun the same way.
    /// </summary>
    internal static class ProviderSyncRunRecorder
    {
        /// <summary>Maximum stored length of ProviderSyncRun.ErrorMessage (matches the column's max length).</summary>
        internal const int MaxErrorMessageLength = 1024;

        /// <summary>Recorded when the account no longer exists, so a missing account is never reported as a success.</summary>
        internal const string AccountNotFoundMessage = "Account not found";

        /// <summary>Recorded when the saved session cannot be restored before a sync.</summary>
        internal const string RestoreFailedMessage = "Saved credentials could not be restored. Sign in to this account again.";

        /// <summary>
        /// Outcome of a pull: null when the account is present and the pull left no error on it, otherwise the error
        /// the pull recorded. A missing account is a failure, never a success.
        /// </summary>
        internal static string? OutcomeAfterPull(ProviderAccount? account) =>
            account is null ? AccountNotFoundMessage : account.LastErrorMessage;

        /// <summary>
        /// Writes one completed run row. Success is true only when <paramref name="errorMessage"/> is null.
        /// Best-effort: a failed write is logged and swallowed so it can never hide the sync's own outcome.
        /// Uses CancellationToken.None so the row still lands during shutdown. Logs carry no account GUID.
        /// </summary>
        internal static async Task RecordAsync(
            IProviderSyncRunRepository syncRuns,
            ILogger logger,
            Guid accountId,
            DateTime startedUtc,
            string? errorMessage)
        {
            try
            {
                string? stored = errorMessage is { Length: > MaxErrorMessageLength } message
                    ? message[..MaxErrorMessageLength]
                    : errorMessage;

                await syncRuns.AddAsync(new ProviderSyncRun
                {
                    Id = Guid.NewGuid(),
                    ProviderAccountId = accountId,
                    StartedUtc = startedUtc,
                    CompletedUtc = DateTime.UtcNow,
                    Success = errorMessage is null,
                    ErrorMessage = stored
                }, CancellationToken.None);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to record sync run");
            }
        }
    }

    /// <inheritdoc />
    public class AccountSyncRunner : IAccountSyncRunner
    {
        private readonly IProviderAuthService _authService;
        private readonly IEventPullService _eventPullService;
        private readonly IProviderAccountRepository _accounts;
        private readonly IProviderSyncRunRepository _syncRuns;
        private readonly ILogger<AccountSyncRunner> _logger;

        /// <summary>Creates the runner.</summary>
        public AccountSyncRunner(
            IProviderAuthService authService,
            IEventPullService eventPullService,
            IProviderAccountRepository accounts,
            IProviderSyncRunRepository syncRuns,
            ILogger<AccountSyncRunner> logger)
        {
            _authService = authService;
            _eventPullService = eventPullService;
            _accounts = accounts;
            _syncRuns = syncRuns;
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
            DateTime startedUtc = DateTime.UtcNow;
            string? outcome;

            try
            {
                outcome = await RestoreAndPullAsync(accountId, fullHistory, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Cancellation is not a failed sync, so no failure row. Rethrow for the caller.
                throw;
            }
            catch (Exception ex)
            {
                // Record the failure, then rethrow the original exception; recording never replaces it.
                await ProviderSyncRunRecorder.RecordAsync(_syncRuns, _logger, accountId, startedUtc, ex.Message);
                throw;
            }

            await ProviderSyncRunRecorder.RecordAsync(_syncRuns, _logger, accountId, startedUtc, outcome);
        }

        /// <summary>
        /// Restores the session and pulls. Returns null when the sync succeeded, or the failure message when it did not.
        /// Failures that the pull records on the account are read back here so the run row reflects them.
        /// </summary>
        private async Task<string?> RestoreAndPullAsync(Guid accountId, bool fullHistory, CancellationToken cancellationToken)
        {
            // A missing account cannot be restored or pulled, so record that instead of a success.
            if (await _accounts.GetAsync(accountId, cancellationToken) is null)
            {
                _logger.LogWarning("Sync skipped: provider account not found");
                return ProviderSyncRunRecorder.AccountNotFoundMessage;
            }

            // Provider sessions are in-memory: after a server restart the account must be restored from its saved
            // credentials before discovery or pulls can reach the provider. A failed restore is recorded, not swallowed.
            try
            {
                if (!await _authService.RestoreFromSavedCredentialsAsync(accountId, cancellationToken))
                {
                    await RecordErrorAsync(accountId, ProviderSyncRunRecorder.RestoreFailedMessage, cancellationToken);
                    return ProviderSyncRunRecorder.RestoreFailedMessage;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Restoring saved credentials failed before sync");
                string restoreError = $"Restoring saved credentials failed: {ex.Message}";
                await RecordErrorAsync(accountId, restoreError, cancellationToken);
                return restoreError;
            }

            // Re-read after restore: the start date comes from the account as restored. A null start pulls full history.
            ProviderAccount? account = await _accounts.GetAsync(accountId, cancellationToken);
            if (account is null)
            {
                return ProviderSyncRunRecorder.AccountNotFoundMessage;
            }

            // The pull records its own failures on the account; it does not throw for them.
            await _eventPullService.PullAccountEventsAsync(accountId, fullHistory ? null : account.LastSuccessfulAuthUtc, cancellationToken);

            // A successful pull clears LastErrorMessage, so anything left here is a failure the pull recorded.
            return ProviderSyncRunRecorder.OutcomeAfterPull(await _accounts.GetAsync(accountId, cancellationToken));
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
