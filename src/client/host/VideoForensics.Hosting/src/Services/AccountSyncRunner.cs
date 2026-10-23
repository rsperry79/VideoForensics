using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using VideoForensics.Client.Common.Contracts;
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
        internal static string SyncTypeFor(DateTime? fromTimestampUtc)
        {
            return fromTimestampUtc is null ? FullSyncType : IncrementalSyncType;
        }

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
        internal static string? OutcomeAfterPull(ProviderAccount? account)
        {
            return account is null ? AccountNotFoundMessage : account.LastErrorMessage;
        }

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
            string? syncType = null,
            EventPullResult? counts = null,
            int? snapshotsSynced = null)
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
                    SyncType = syncType,
                    // Counts stay null when the sync never reached the pull (restore failure, missing account).
                    DevicesSynced = counts?.DevicesSynced,
                    LocationsSynced = counts?.LocationsSynced,
                    EventsSynced = counts?.EventsSynced,
                    ConfigAreasSynced = counts?.ConfigAreasSynced,
                    FeedsFailed = counts?.FeedsFailed,
                    // Only snapshot-bearing runs (Snapshots, All) set this; event-only runs leave it null.
                    SnapshotsSynced = snapshotsSynced
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
        private readonly ISyncActivityLog _activity;

        /// <summary>Creates the runner.</summary>
        public AccountSyncRunner(
            IServiceProvider services,
            IProviderAccountRepository accounts,
            IProviderSyncRunRepository syncRuns,
            ISnapshotSyncService snapshotSync,
            ILogger<AccountSyncRunner> logger,
            ISyncActivityLog activity)
        {
            _services = services;
            _accounts = accounts;
            _syncRuns = syncRuns;
            _snapshotSync = snapshotSync;
            _logger = logger;
            _activity = activity;
        }

        /// <inheritdoc />
        public Task RunAsync(Guid accountId, CancellationToken cancellationToken)
        {
            return SyncAsync(accountId, fullHistory: false, cancellationToken);
        }

        /// <inheritdoc />
        public Task RunAsync(Guid accountId, AccountSyncRequest request, CancellationToken cancellationToken)
        {
            return SyncAsync(accountId, request.AllHistory, cancellationToken, request);
        }

        /// <inheritdoc />
        public Task RunInitialAsync(Guid accountId, CancellationToken cancellationToken)
        {
            return SyncAsync(accountId, fullHistory: true, cancellationToken);
        }

        private async Task SyncAsync(Guid accountId, bool fullHistory, CancellationToken cancellationToken, AccountSyncRequest? request = null)
        {
            DateTime startedUtc = DateTime.UtcNow;
            SyncOutcome outcome;
            string syncType = request?.Type == AccountSyncType.Snapshots
                ? ProviderSyncRunRecorder.SnapshotsSyncType
                : fullHistory ? ProviderSyncRunRecorder.FullSyncType : ProviderSyncRunRecorder.IncrementalSyncType;

            // Console lines name the provider only, never the account GUID.
            string provider = (await _accounts.GetAsync(accountId, CancellationToken.None))?.ProviderName ?? "Provider";
            _activity.Append($"{provider}: {SyncLabel(fullHistory, request)} sync started");

            try
            {
                outcome = await RestoreAndPullAsync(accountId, fullHistory, request, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Cancellation is not a failed sync, so no failure row. Rethrow for the caller.
                throw;
            }
            catch (Exception ex)
            {
                // Record the failure, then rethrow the original exception; recording never replaces it.
                _activity.Append($"{provider}: sync failed: {ex.Message}");
                await ProviderSyncRunRecorder.RecordAsync(_syncRuns, _logger, accountId, startedUtc, ex.Message, syncType);
                throw;
            }

            _activity.Append(outcome.Error is null
                ? $"{provider}: sync finished"
                : $"{provider}: sync failed: {outcome.Error}");
            // Only a clean sync moves the display timestamp. LastDownloadTimeUtc is never written here (see MarkSyncCompletedAsync).
            if (outcome.Error is null)
            {
                await MarkSyncCompletedAsync(accountId, DateTime.UtcNow);
            }

            await ProviderSyncRunRecorder.RecordAsync(_syncRuns, _logger, accountId, startedUtc, outcome.Error, syncType, outcome.Counts, outcome.SnapshotsSynced);
        }

        /// <summary>Short name of the sync for the console: Everything, Events, Snapshots, or Initial for a first sync.</summary>
        private static string SyncLabel(bool fullHistory, AccountSyncRequest? request)
        {
            return request?.Type switch
            {
                AccountSyncType.Snapshots => "Snapshots",
                AccountSyncType.All => "Everything",
                AccountSyncType.Events => "Events",
                _ => fullHistory ? "Initial" : "Events",
            };
        }

        /// <summary>A sync's failure message (null on success) and the counts its pull reported (null if it never pulled).</summary>
        private readonly record struct SyncOutcome(string? Error, EventPullResult? Counts = null, int? SnapshotsSynced = null)
        {
            public static implicit operator SyncOutcome(string? error)
            {
                return new(error);
            }
        }

        /// <summary>
        /// Restores the session and pulls. Returns a null error when the sync succeeded, or the failure message when it did not.
        /// Failures that the pull records on the account are read back here so the run row reflects them.
        /// </summary>
        private async Task<SyncOutcome> RestoreAndPullAsync(Guid accountId, bool fullHistory, AccountSyncRequest? request, CancellationToken cancellationToken)
        {
            // A missing account cannot be restored or pulled, so record that instead of a success.
            ProviderAccount? initial = await _accounts.GetAsync(accountId, cancellationToken);
            if (initial is null)
            {
                _logger.LogWarning("Sync skipped: provider account not found");
                return ProviderSyncRunRecorder.AccountNotFoundMessage;
            }

            // Provider services are chosen by the account's provider name: the unkeyed service is only whichever provider
            // registered last, so with several providers it can belong to the wrong one. Same keyed lookup as
            // SnapshotSyncService and the scheduled sync; the unkeyed service stays the fallback when none are keyed.
            IProviderAuthService authService = _services.GetKeyedService<IProviderAuthService>(initial.ProviderName)
                ?? _services.GetRequiredService<IProviderAuthService>();
            IEventPullService eventPullService = _services.GetKeyedService<IEventPullService>(initial.ProviderName)
                ?? _services.GetRequiredService<IEventPullService>();

            // Provider sessions are in-memory: after a server restart the account must be restored from its saved
            // credentials before discovery or pulls can reach the provider. A failed restore is recorded, not swallowed.
            try
            {
                if (!await authService.RestoreFromSavedCredentialsAsync(accountId, cancellationToken))
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

            // Full history (the initial backfill, or "All" with no range) is capped at one year back.
            DateTime? snapshotStart = request is { AllHistory: true } ? DateTime.UtcNow.AddYears(-1) : request?.FromUtc;

            // Snapshots-only: fetch snapshots for the account's devices instead of pulling events.
            if (request?.Type == AccountSyncType.Snapshots)
            {
                SnapshotSyncResult snapshots = await _snapshotSync.SyncAccountSnapshotsAsync(accountId, snapshotStart, request.ToUtc, cancellationToken);
                _activity.Append($"{initial.ProviderName}: snapshots for {snapshots.DevicesSucceeded} device(s)");

                // Event counts stay null: a snapshots-only run pulled no events, so DevicesSynced/FeedsFailed would mislead.
                return new SyncOutcome(snapshots.ErrorMessage, null, snapshots.DevicesSucceeded);
            }

            // An explicit period wins; otherwise since the last successful auth. A null end date means now.
            DateTime? historyStart = fullHistory ? DateTime.UtcNow.AddYears(-1) : null;
            DateTime? requestedFrom = historyStart ?? request?.FromUtc ?? account.LastSuccessfulAuthUtc;
            if (AccountSyncRunner.WasLimited(requestedFrom, DateTime.UtcNow))
            {
                _activity.Append($"{initial.ProviderName}: history limited to one month per pull");
                _logger.LogInformation("Sync start limited to one month back for {ProviderName} account", initial.ProviderName);
            }

            DateTime? from = AccountSyncRunner.OneMonthFloor(requestedFrom, DateTime.UtcNow);
            DateTime? to = request?.ToUtc;

            // The pull records its own failures on the account; it does not throw for them.
            // The first sync backfills a year in one-month windows, oldest first, so a long history is pulled in bounded calls.
            string? monthlyError = null;
            EventPullResult counts;
            if (fullHistory)
            {
                (counts, monthlyError) = await PullMonthlyAsync(eventPullService, accountId, cancellationToken);
            }
            else
            {
                counts = await eventPullService.PullAccountEventsAsync(accountId, from, to, cancellationToken);
            }

            LogPullCounts(initial.ProviderName, counts);

            // A successful pull clears LastErrorMessage, so anything left here is a failure the pull recorded.
            string? eventError = monthlyError ?? ProviderSyncRunRecorder.OutcomeAfterPull(await _accounts.GetAsync(accountId, cancellationToken));

            if (request?.Type != AccountSyncType.All)
            {
                return new SyncOutcome(eventError, counts);
            }

            // "All" covers events and snapshots in one run row. Snapshots run even when the event pull failed, so one
            // failure never hides the other; errors are joined. Note Ring fetches a fresh snapshot per device here.
            SnapshotSyncResult allSnapshots = await _snapshotSync.SyncAccountSnapshotsAsync(accountId, snapshotStart, request.ToUtc, cancellationToken);
            string? error = eventError is null ? allSnapshots.ErrorMessage
                : allSnapshots.ErrorMessage is null ? eventError
                : $"{eventError}; {allSnapshots.ErrorMessage}";
            return new SyncOutcome(error, counts, allSnapshots.DevicesSucceeded);
        }
        /// <summary>The end of the backfill window that starts at <paramref name="windowStart"/>: one month later, or now if that is sooner.</summary>
        internal static DateTime NextBackfillEnd(DateTime windowStart, DateTime nowUtc)
        {
            DateTime end = windowStart.AddMonths(1);
            return end > nowUtc ? nowUtc : end;
        }

        /// <summary>True when a requested start is older than one month, so the pull will be cut short. Callers report it.</summary>
        internal static bool WasLimited(DateTime? requested, DateTime nowUtc)
        {
            return requested is { } start && start < nowUtc.AddMonths(-1);
        }

        /// <summary>
        /// No single pull reaches further back than one month: a start older than that, or no start at all, becomes one month before now.
        /// The monthly backfill windows are already one month each, so they do not go through this.
        /// </summary>
        internal static DateTime OneMonthFloor(DateTime? from, DateTime nowUtc)
        {
            DateTime floor = nowUtc.AddMonths(-1);
            return from is { } start && start > floor ? start : floor;
        }

        /// <summary>
        /// The windows of an initial backfill: one month each, from a year ago up to now, oldest first. The last window ends at now.
        /// </summary>
        internal static IReadOnlyList<(DateTime From, DateTime To)> MonthlyWindows(DateTime nowUtc)
        {
            var windows = new List<(DateTime From, DateTime To)>();
            DateTime start = nowUtc.AddYears(-1);
            while (start < nowUtc)
            {
                DateTime end = start.AddMonths(1);
                if (end > nowUtc)
                {
                    end = nowUtc;
                }

                windows.Add((start, end));
                start = end;
            }

            return windows;
        }

        /// <summary>
        /// Pulls the backfill one month at a time. Discovery repeats every month, so devices and locations take the largest
        /// count rather than a sum; events and failed feeds are summed across the months.
        /// </summary>
        private async Task<(EventPullResult Counts, string? Error)> PullMonthlyAsync(IEventPullService eventPullService, Guid accountId, CancellationToken cancellationToken)
        {
            int devices = 0, locations = 0, events = 0, config = 0, failed = 0;
            string? firstError = null;
            foreach ((DateTime from, DateTime to) in MonthlyWindows(DateTime.UtcNow))
            {
                cancellationToken.ThrowIfCancellationRequested();
                EventPullResult month = await eventPullService.PullAccountEventsAsync(accountId, from, to, cancellationToken);
                devices = Math.Max(devices, month.DevicesSynced);
                locations = Math.Max(locations, month.LocationsSynced);
                config = Math.Max(config, month.ConfigAreasSynced);
                events += month.EventsSynced;
                failed += month.FeedsFailed;

                // A failed month records its error on the account; a later clean month clears it. Keep the first one so it is not lost.
                firstError ??= ProviderSyncRunRecorder.OutcomeAfterPull(await _accounts.GetAsync(accountId, cancellationToken));
            }

            return (new EventPullResult(devices, locations, events, config, failed), firstError);
        }

        /// <summary>Console lines for what the pull found: discovery, events, and configuration, with any failed feeds.</summary>
        private void LogPullCounts(string provider, EventPullResult counts)
        {
            _activity.Append($"{provider}: discovered {counts.LocationsSynced} location(s), {counts.DevicesSynced} device(s)");
            _activity.Append($"{provider}: {counts.EventsSynced} event(s) synced");
            _activity.Append($"{provider}: config read for {counts.ConfigAreasSynced} device(s)");
            if (counts.FeedsFailed > 0)
            {
                _activity.Append($"{provider}: {counts.FeedsFailed} event feed(s) failed");
            }
        }

        /// <summary>
        /// Stores when the clean sync finished, for the Last Download display. Best-effort: a failed write is logged and
        /// swallowed so it can never turn a successful sync into a failure. Uses CancellationToken.None so it lands during shutdown.
        /// </summary>
        private async Task MarkSyncCompletedAsync(Guid accountId, DateTime completedUtc)
        {
            try
            {
                ProviderAccount? account = await _accounts.GetAsync(accountId, CancellationToken.None);
                if (account is null)
                {
                    return;
                }

                account.LastSyncCompletedUtc = completedUtc;
                await _accounts.UpdateAsync(account, CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to record sync completion");
            }
        }

        private async Task RecordErrorAsync(Guid accountId, string message, CancellationToken cancellationToken)
        {
            ProviderAccount? account = await _accounts.GetAsync(accountId, cancellationToken);
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
