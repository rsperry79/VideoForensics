using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using VideoForensics.Core.Providers.Common.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Hosting.Contracts;

namespace VideoForensics.Hosting.Services
{
    /// <summary>What a manual sync should cover.</summary>
    public enum AccountSyncType
    {
        /// <summary>Events and snapshots.</summary>
        All,

        /// <summary>Events only.</summary>
        Events,

        /// <summary>Snapshots only.</summary>
        Snapshots
    }

    /// <summary>A manual sync request: what to sync and over which period.</summary>
    /// <param name="Type">Which data to sync.</param>
    /// <param name="FromUtc">Start of the period; null with <paramref name="AllHistory"/> means the provider's whole history.</param>
    /// <param name="ToUtc">End of the period, when the user picked a custom range.</param>
    /// <param name="AllHistory">True to cover the provider's full history.</param>
    public sealed record AccountSyncRequest(AccountSyncType Type, DateTime? FromUtc, DateTime? ToUtc, bool AllHistory);

    /// <summary>Runs a manual sync for one account: restore its saved credentials, then pull.</summary>
    public interface IAccountSyncRunner
    {
        /// <summary>Like <see cref="RunAsync(Guid, CancellationToken)"/> but honours the requested sync type and period.</summary>
        Task RunAsync(Guid accountId, AccountSyncRequest request, CancellationToken cancellationToken);

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
        /// <summary>SyncType recorded when the pull covered the provider's whole event history (no start date).</summary>
        internal const string FullSyncType = "Full";

        /// <summary>SyncType recorded when the pull covered events since a start date.</summary>
        internal const string IncrementalSyncType = "Incremental";

        /// <summary>SyncType recorded for a snapshots-only sync.</summary>
        internal const string SnapshotsSyncType = "Snapshots";

        /// <summary>Maps the pull's start date to a sync type: no start date means the full history was pulled.</summary>
        internal static string SyncTypeFor(DateTime? fromTimestampUtc) =>
            fromTimestampUtc is null ? FullSyncType : IncrementalSyncType;

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
        /// Sync type for a run row. "Snapshots" for a snapshots-only run, "Full" when the config gate ran, "Events" when only
        /// event feeds ran; "Unknown" when the run never reached the pull (no totals), so its counts are not a real zero.
        /// </summary>
        internal static string SyncTypeFor(SyncRunTotals? totals, SyncRunKind kind = SyncRunKind.Default) =>
            totals is null ? "Unknown"
            : kind == SyncRunKind.Snapshots ? "Snapshots"
            : totals.ConfigRan ? "Full" : "Events";

        /// <summary>
        /// True when the sync fetches snapshots after the pull: a Snapshots sync, or an "All" sync (a Default kind that
        /// asked for snapshots). The legacy and initial syncs never fetch snapshots.
        /// </summary>
        internal static bool IncludesSnapshots(SyncRunOptions? options) =>
            options is { Kind: SyncRunKind.Snapshots } or { Kind: SyncRunKind.Default, IncludeSnapshots: true };

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
            SyncRunTotals? totals = null,
            SyncRunKind kind = SyncRunKind.Default)
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
                    SyncType = SyncTypeFor(totals, kind),
                    LocationsSynced = totals?.LocationsSynced,
                    DevicesSynced = totals?.DevicesSynced,
                    EventsSynced = totals?.EventsSynced,
                    EventsNotPulled = totals?.EventsNotPulled,
                    EventsAlreadyPresent = totals?.EventsAlreadyPresent,
                    ConfigAreasSynced = totals?.ConfigAreasSynced,
                    FeedsFailed = totals?.FeedsFailed,
                    SnapshotsSynced = totals?.SnapshotsSynced
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
        private readonly IServiceProvider _services;
        private readonly IProviderAccountRepository _accounts;
        private readonly IProviderSyncRunRepository _syncRuns;
        private readonly ISnapshotSyncService _snapshotSync;
        private readonly ILogger<AccountSyncRunner> _logger;
        private readonly ISyncRunTally? _syncRunTally;
        private readonly ISyncRunOptions? _syncRunOptions;
        private readonly IServiceProvider? _services;
        private readonly ISnapshotSyncService? _snapshotSync;

        /// <summary>
        /// Creates the runner. The tally, when registered, supplies the sync type and counts recorded on each run. The options
        /// holder, when registered, receives the sync kind and window before each pull so the providers can read them.
        /// When <paramref name="services"/> is given, the auth and event pull services are resolved by the account's provider
        /// name (keyed), with the injected ones as the fallback. A snapshot service, when registered, runs for Snapshots and
        /// "All" syncs.
        /// </summary>
        public AccountSyncRunner(
            IServiceProvider services,
            IProviderAccountRepository accounts,
            IProviderSyncRunRepository syncRuns,
            ILogger<AccountSyncRunner> logger,
            ISyncRunTally? syncRunTally = null,
            ISyncRunOptions? syncRunOptions = null,
            IServiceProvider? services = null,
            ISnapshotSyncService? snapshotSync = null)
        {
            _services = services;
            _accounts = accounts;
            _syncRuns = syncRuns;
            _snapshotSync = snapshotSync;
            _logger = logger;
            _syncRunTally = syncRunTally;
            _syncRunOptions = syncRunOptions;
            _services = services;
            _snapshotSync = snapshotSync;
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
        public Task RunAsync(Guid accountId, AccountSyncRequest request, CancellationToken cancellationToken) =>
            SyncAsync(accountId, request.AllHistory, cancellationToken, request);

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
            if (fullHistory || options is null || (options.Kind == SyncRunKind.Default && !options.IncludeSnapshots))
            {
                return (fullHistory ? null : lastSuccessUtc, new SyncRunOptions());
            }

            DateTime? start = options.RangeKind switch
            {
                SyncRangeKind.Days => nowUtc.AddDays(-(options.Days ?? 0)),
                SyncRangeKind.Custom => options.StartUtc,
                // The provider's retained history, bounded by the one-year initial-backfill horizon.
                SyncRangeKind.All => nowUtc.AddYears(-1),
                // An "All" sync (Default kind) keeps the legacy start; the other kinds look back 30 days when there is no prior sync.
                _ => options.Kind == SyncRunKind.Default ? lastSuccessUtc : lastSuccessUtc ?? nowUtc.AddDays(-30),
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
                await ProviderSyncRunRecorder.RecordAsync(_syncRuns, _logger, accountId, startedUtc, ex.Message, syncType);
                throw;
            }

            await ProviderSyncRunRecorder.RecordAsync(_syncRuns, _logger, accountId, startedUtc, outcome.Error, outcome.Totals, options?.Kind ?? SyncRunKind.Default);
        }

        /// <summary>
        /// Restores the session and pulls. The error is null when the sync succeeded, or the failure message when it did not.
        /// Failures that the pull records on the account are read back here so the run row reflects them.
        /// </summary>
        private async Task<SyncOutcome> RestoreAndPullAsync(Guid accountId, bool fullHistory, SyncRunOptions? options, CancellationToken cancellationToken)
        {
            // A missing account cannot be restored or pulled, so record that instead of a success.
            ProviderAccount? initial = await _accounts.GetAsync(accountId, cancellationToken);
            if (initial is null)
            {
                _logger.LogWarning("Sync skipped: provider account not found");
                return new SyncOutcome(ProviderSyncRunRecorder.AccountNotFoundMessage, null);
            }

            // Provider services are chosen by the account's provider name: the unkeyed service is only whichever provider
            // registered last, so with several providers it can belong to the wrong one. Same keyed lookup as the snapshot
            // sync and the scheduled sync; the injected service stays the fallback when none are keyed.
            IProviderAuthService authService = _services?.GetKeyedService<IProviderAuthService>(initial.ProviderName) ?? _authService;
            IEventPullService eventPullService = _services?.GetKeyedService<IEventPullService>(initial.ProviderName) ?? _eventPullService;

            // Provider sessions are in-memory: after a server restart the account must be restored from its saved
            // credentials before discovery or pulls can reach the provider. A failed restore is recorded, not swallowed.
            try
            {
                if (!await authService.RestoreFromSavedCredentialsAsync(accountId, cancellationToken))
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

            // Snapshots-only: fetch snapshots for the account's devices instead of pulling events.
            if (request?.Type == AccountSyncType.Snapshots)
            {
                SnapshotSyncResult snapshots = await _snapshotSync.SyncAccountSnapshotsAsync(accountId, request.FromUtc, request.ToUtc, cancellationToken);

                // Event counts stay null: a snapshots-only run pulled no events, so DevicesSynced/FeedsFailed would mislead.
                return new SyncOutcome(snapshots.ErrorMessage, null, snapshots.DevicesSucceeded);
            }

            // An explicit period wins; otherwise since the last successful auth. A null end date means now.
            DateTime? from = fullHistory ? null : request?.FromUtc ?? account.LastSuccessfulAuthUtc;
            DateTime? to = request?.ToUtc;

            // The pull records its own failures on the account; it does not throw for them.
            await eventPullService.PullAccountEventsAsync(accountId, from, cancellationToken);

            // A successful pull clears LastErrorMessage, so anything left here is a failure the pull recorded.
            string? error = ProviderSyncRunRecorder.OutcomeAfterPull(await _accounts.GetAsync(accountId, cancellationToken));
            SyncRunTotals? totals = _syncRunTally?.Snapshot();

            if (!ProviderSyncRunRecorder.IncludesSnapshots(options) || _snapshotSync is null)
            {
                return new SyncOutcome(error, totals);
            }

            // Snapshots run even when the pull failed, so one failure never hides the other; errors are joined.
            // Note Ring fetches a fresh snapshot per device here, and ignores the window.
            SnapshotSyncResult snapshots = await _snapshotSync.SyncAccountSnapshotsAsync(accountId, published.StartUtc, published.EndUtc, cancellationToken);
            string? combined = error is null ? snapshots.ErrorMessage
                : snapshots.ErrorMessage is null ? error
                : $"{error}; {snapshots.ErrorMessage}";
            totals = (totals ?? new SyncRunTotals(false, 0, 0, 0, 0, 0)) with { SnapshotsSynced = snapshots.SnapshotsSaved };
            return new SyncOutcome(combined, totals);
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
