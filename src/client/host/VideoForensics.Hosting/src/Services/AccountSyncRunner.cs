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
        /// Runs a manual sync of the given kind over the given window. A Default kind with no range is the legacy sync.
        /// Failures are recorded on the account.
        /// </summary>
        Task RunAsync(Guid accountId, SyncRunOptions options, CancellationToken cancellationToken);

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
        /// Sync type for a run row. "Full" when the config gate ran, "Events" when only event feeds ran; "Unknown" when
        /// the run never reached the pull (no totals), so its counts are not a real zero.
        /// </summary>
        internal static string SyncTypeFor(SyncRunTotals? totals) =>
            totals is null ? "Unknown" : totals.ConfigRan ? "Full" : "Events";

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
            string? errorMessage,
            SyncRunTotals? totals = null)
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
                    ErrorMessage = stored,
                    SyncType = SyncTypeFor(totals),
                    LocationsSynced = totals?.LocationsSynced,
                    DevicesSynced = totals?.DevicesSynced,
                    EventsSynced = totals?.EventsSynced,
                    EventsNotPulled = totals?.EventsNotPulled,
                    EventsAlreadyPresent = totals?.EventsAlreadyPresent,
                    ConfigAreasSynced = totals?.ConfigAreasSynced,
                    FeedsFailed = totals?.FeedsFailed
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
        private readonly ISyncRunTally? _syncRunTally;
        private readonly ISyncRunOptions? _syncRunOptions;

        /// <summary>
        /// Creates the runner. The tally, when registered, supplies the sync type and counts recorded on each run. The options
        /// holder, when registered, receives the sync kind and window before each pull so the providers can read them.
        /// </summary>
        public AccountSyncRunner(
            IProviderAuthService authService,
            IEventPullService eventPullService,
            IProviderAccountRepository accounts,
            IProviderSyncRunRepository syncRuns,
            ILogger<AccountSyncRunner> logger,
            ISyncRunTally? syncRunTally = null,
            ISyncRunOptions? syncRunOptions = null)
        {
            _authService = authService;
            _eventPullService = eventPullService;
            _accounts = accounts;
            _syncRuns = syncRuns;
            _logger = logger;
            _syncRunTally = syncRunTally;
            _syncRunOptions = syncRunOptions;
        }

        /// <summary>What a sync attempt produced: the failure message (null on success) and the counts when a pull ran.</summary>
        private sealed record SyncOutcome(string? Error, SyncRunTotals? Totals);

        /// <inheritdoc />
        public Task RunAsync(Guid accountId, CancellationToken cancellationToken) =>
            SyncAsync(accountId, fullHistory: false, options: null, cancellationToken);

        /// <inheritdoc />
        public Task RunAsync(Guid accountId, SyncRunOptions options, CancellationToken cancellationToken) =>
            SyncAsync(accountId, fullHistory: false, options, cancellationToken);

        /// <inheritdoc />
        public Task RunInitialAsync(Guid accountId, CancellationToken cancellationToken) =>
            SyncAsync(accountId, fullHistory: true, options: null, cancellationToken);

        /// <summary>
        /// Chooses the pull's start and the options the providers see. Initial and legacy syncs keep the start they always had
        /// (the last successful auth, or full history) and publish no range. An explicit sync's window comes from its range kind;
        /// with no prior successful sync, SinceLast looks back 30 days.
        /// </summary>
        internal static (DateTime? From, SyncRunOptions Published) ResolveWindow(SyncRunOptions? options, DateTime? lastSuccessUtc, bool fullHistory, DateTime nowUtc)
        {
            if (fullHistory || options is null || options.Kind == SyncRunKind.Default)
            {
                return (fullHistory ? null : lastSuccessUtc, new SyncRunOptions());
            }

            DateTime? start = options.RangeKind switch
            {
                SyncRangeKind.Days => nowUtc.AddDays(-(options.Days ?? 0)),
                SyncRangeKind.Custom => options.StartUtc,
                _ => lastSuccessUtc ?? nowUtc.AddDays(-30),
            };
            DateTime? end = options.RangeKind == SyncRangeKind.Custom ? options.EndUtc : null;
            return (start, options with { StartUtc = start, EndUtc = end });
        }

        private async Task SyncAsync(Guid accountId, bool fullHistory, SyncRunOptions? options, CancellationToken cancellationToken)
        {
            DateTime startedUtc = DateTime.UtcNow;
            SyncOutcome outcome;

            try
            {
                outcome = await RestoreAndPullAsync(accountId, fullHistory, options, cancellationToken);
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

            await ProviderSyncRunRecorder.RecordAsync(_syncRuns, _logger, accountId, startedUtc, outcome.Error, outcome.Totals);
        }

        /// <summary>
        /// Restores the session and pulls. The error is null when the sync succeeded, or the failure message when it did not.
        /// Failures that the pull records on the account are read back here so the run row reflects them.
        /// </summary>
        private async Task<SyncOutcome> RestoreAndPullAsync(Guid accountId, bool fullHistory, SyncRunOptions? options, CancellationToken cancellationToken)
        {
            // A missing account cannot be restored or pulled, so record that instead of a success.
            if (await _accounts.GetAsync(accountId, cancellationToken) is null)
            {
                _logger.LogWarning("Sync skipped: provider account not found");
                return new SyncOutcome(ProviderSyncRunRecorder.AccountNotFoundMessage, null);
            }

            // Provider sessions are in-memory: after a server restart the account must be restored from its saved
            // credentials before discovery or pulls can reach the provider. A failed restore is recorded, not swallowed.
            try
            {
                if (!await _authService.RestoreFromSavedCredentialsAsync(accountId, cancellationToken))
                {
                    await RecordErrorAsync(accountId, ProviderSyncRunRecorder.RestoreFailedMessage, cancellationToken);
                    return new SyncOutcome(ProviderSyncRunRecorder.RestoreFailedMessage, null);
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
                return new SyncOutcome(restoreError, null);
            }

            // Re-read after restore: the start date comes from the account as restored. A null start pulls full history.
            ProviderAccount? account = await _accounts.GetAsync(accountId, cancellationToken);
            if (account is null)
            {
                return new SyncOutcome(ProviderSyncRunRecorder.AccountNotFoundMessage, null);
            }

            // Reset so counts from an earlier run in the same DI scope never leak into this run's row.
            _syncRunTally?.Reset();

            (DateTime? from, SyncRunOptions published) = ResolveWindow(options, account.LastSuccessfulAuthUtc, fullHistory, DateTime.UtcNow);
            if (_syncRunOptions is not null)
            {
                _syncRunOptions.Current = published;
            }

            // The pull records its own failures on the account; it does not throw for them.
            await _eventPullService.PullAccountEventsAsync(accountId, from, cancellationToken);

            // A successful pull clears LastErrorMessage, so anything left here is a failure the pull recorded.
            return new SyncOutcome(
                ProviderSyncRunRecorder.OutcomeAfterPull(await _accounts.GetAsync(accountId, cancellationToken)),
                _syncRunTally?.Snapshot());
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
