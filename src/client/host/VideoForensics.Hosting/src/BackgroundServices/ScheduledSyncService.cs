using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Hosting.Contracts;
using VideoForensics.Hosting.Services;
using VideoForensics.Core.Providers.Common.Contracts;

namespace VideoForensics.Hosting.BackgroundServices
{
    /// <summary>
    /// Runs each enabled per-account <see cref="SyncSchedule"/>: when an account's event poll is due it pulls
    /// events and device config through that account's OWN provider (keyed <see cref="IEventAndConfigService"/> /
    /// <see cref="IProviderAuthService"/>, see AddVideoForensicsMultiProviderServices) and records the run.
    ///
    /// Accounts are processed strictly one at a time, never concurrently. Roughly ten parameterless
    /// GetSession() call sites remain in the provider layer, so two same-provider runs in parallel could race on
    /// the shared session; poll intervals are tens of minutes, so sequential processing costs nothing.
    ///
    /// Server-tier only (VideoForensics.WebApp, registered through AddVideoForensicsScheduledSync): a client host
    /// must never call a provider directly. Snapshot/RSSI scheduling is a later milestone; RSSI polling stays with
    /// <see cref="DeviceHealthSyncService"/>. Logs use the provider name ("Ring account"), never the account GUID.
    /// </summary>
    public class ScheduledSyncService : BackgroundService
    {
        /// <summary>Re-pulled overlap before the previous run so events landing during/just after it are not missed.</summary>
        private static readonly TimeSpan PullOverlap = TimeSpan.FromMinutes(10);

        private const int MinimumTickSeconds = 5;

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IOptionsMonitor<ScheduledTasksOptions> _options;
        private readonly ILogger<ScheduledSyncService> _logger;
        private readonly TimeProvider _time;

        /// <summary>How long a failed roll-up waits before its next attempt. Keeps a broken encoder from running every tick.</summary>
        internal static readonly TimeSpan RollupRetryBackoff = TimeSpan.FromMinutes(5);

        /// <summary>Per account: the time before which a failed roll-up is not retried.</summary>
        private readonly Dictionary<Guid, DateTimeOffset> _rollupRetryAfter = new();

        /// <summary>Creates the scheduler.</summary>
        public ScheduledSyncService(
            IServiceScopeFactory scopeFactory,
            IOptionsMonitor<ScheduledTasksOptions> options,
            ILogger<ScheduledSyncService> logger,
            TimeProvider time)
        {
            _scopeFactory = scopeFactory;
            _options = options;
            _logger = logger;
            _time = time;
        }

        /// <summary>Tick period for a configured value: seconds, floored at 5.</summary>
        internal static TimeSpan ResolveTickInterval(int tickSeconds)
        {
            return TimeSpan.FromSeconds(Math.Max(tickSeconds, MinimumTickSeconds));
        }

        /// <inheritdoc />
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var timer = new PeriodicTimer(ResolveTickInterval(_options.CurrentValue.TickSeconds), _time);

            try
            {
                // Wait for the first tick before the first pass so a cold start does not race database initialization.
                while (await timer.WaitForNextTickAsync(stoppingToken))
                {
                    try
                    {
                        await RunOneTickAsync(stoppingToken);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Scheduled sync tick failed; will retry on the next tick");
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Normal shutdown.
            }
        }

        /// <summary>Internal for direct testability (VideoForensics.Hosting.Tests) without driving the BackgroundService lifecycle/timer.</summary>
        internal async Task RunOneTickAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            using IServiceScope scope = _scopeFactory.CreateScope();
            IServiceProvider sp = scope.ServiceProvider;

            ISyncScheduleRepository scheduleRepository = sp.GetRequiredService<ISyncScheduleRepository>();
            IReadOnlyList<SyncSchedule> schedules = await scheduleRepository.ListEnabledAsync(ct);
            if (schedules.Count == 0)
            {
                return;
            }

            string[] enabledProviders = _options.CurrentValue.EnabledProviders ?? [];

            foreach (SyncSchedule schedule in schedules)
            {
                ct.ThrowIfCancellationRequested();

                string providerName = "unknown provider";
                try
                {
                    ProviderAccount? account = await sp.GetRequiredService<IProviderAccountRepository>().GetAsync(schedule.ProviderAccountId, ct);
                    if (account == null)
                    {
                        _logger.LogWarning("Scheduled sync skipped: provider account for an enabled schedule no longer exists");
                        if (ScheduleCalculator.IsEventDue(schedule, _time.GetUtcNow().UtcDateTime))
                        {
                            await RecordMissingAccountAttemptAsync(sp, scheduleRepository, schedule, ct);
                        }

                        continue;
                    }

                    providerName = account.ProviderName;
                    if (account.RemovedUtc != null)
                    {
                        // Defensive: retiring an account deletes its schedule, so this only fires on a leftover row.
                        _logger.LogDebug("Scheduled sync skipped: {ProviderName} account was removed", providerName);
                        continue;
                    }

                    if (!account.IsActive)
                    {
                        _logger.LogDebug("Scheduled sync skipped: {ProviderName} account is inactive", providerName);
                        continue;
                    }

                    // Keyed services are registered under the configured spelling, so resolve with that key.
                    string? key = enabledProviders.FirstOrDefault(p => string.Equals(p, providerName, StringComparison.OrdinalIgnoreCase));
                    if (key == null)
                    {
                        _logger.LogDebug("Scheduled sync skipped: {ProviderName} is not in ScheduledTasks:EnabledProviders", providerName);
                        continue;
                    }

                    IEventAndConfigService? eventService = sp.GetKeyedService<IEventAndConfigService>(key);
                    IProviderAuthService? authService = sp.GetKeyedService<IProviderAuthService>(key);
                    IMediaDownloadService? mediaService = sp.GetKeyedService<IMediaDownloadService>(key);
                    if (eventService == null || authService == null || mediaService == null)
                    {
                        _logger.LogWarning("Scheduled sync skipped: no keyed provider services are registered for {ProviderName}", providerName);
                        continue;
                    }

                    DateTime nowUtc = _time.GetUtcNow().UtcDateTime;
                    if (ScheduleCalculator.IsSnapshotDue(schedule, nowUtc))
                    {
                        await RunSnapshotSyncAsync(sp, scheduleRepository, schedule, account, authService, ct);
                    }

                    if (ScheduleCalculator.IsRollupDue(schedule, nowUtc))
                    {
                        await RunRollupAsync(sp, scheduleRepository, schedule, account, ct);
                    }

                    if (!ScheduleCalculator.IsEventDue(schedule, nowUtc))
                    {
                        continue;
                    }

                    await RunEventSyncAsync(sp, scheduleRepository, schedule, account, eventService, authService, sp.GetRequiredKeyedService<IDeviceDiscoveryService>(key), mediaService, ct);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    // One account's failure must never stop the others.
                    _logger.LogError(ex, "Scheduled sync failed for {ProviderName} account", providerName);
                }
            }
        }

        /// <summary>
        /// Rolls the account's snapshots since the last roll-up into MP4 loops and records the roll-up only when every device
        /// succeeded. A failed roll-up is retried on a later tick with a window that still starts at the last good roll-up.
        /// </summary>
        private async Task RunRollupAsync(
            IServiceProvider sp,
            ISyncScheduleRepository scheduleRepository,
            SyncSchedule schedule,
            ProviderAccount account,
            CancellationToken ct)
        {
            DateTimeOffset now = _time.GetUtcNow();
            if (_rollupRetryAfter.TryGetValue(account.Id, out DateTimeOffset retryAfter) && now < retryAfter)
            {
                return;
            }

            DateTime toUtc = _time.GetUtcNow().UtcDateTime;
            DateTime fromUtc = schedule.LastRollupUtc ?? toUtc.AddMinutes(-schedule.SnapshotRollupIntervalMinutes);
            SnapshotRollupResult result = await sp.GetRequiredService<ISnapshotRollupService>().RollUpAccountAsync(account.Id, fromUtc, toUtc, ct);
            if (result.ErrorMessage is not null)
            {
                _logger.LogWarning("Scheduled snapshot roll-up for {ProviderName} account finished with failures: {Error}", account.ProviderName, result.ErrorMessage);
                _rollupRetryAfter[account.Id] = now + RollupRetryBackoff;
                return;
            }

            // Uses CancellationToken.None so a completed roll-up is recorded even when shutdown cancelled the tick.
            _ = _rollupRetryAfter.Remove(account.Id);
            await scheduleRepository.RecordRollupAsync(account.Id, toUtc, CancellationToken.None);
        }

        /// <summary>
        /// Pulls the latest snapshot for each device of a due account and records the next snapshot run. The provider
        /// session is restored first, as the event pull does; a failed restore still records the run so it is not retried every tick.
        /// </summary>
        private async Task RunSnapshotSyncAsync(
            IServiceProvider sp,
            ISyncScheduleRepository scheduleRepository,
            SyncSchedule schedule,
            ProviderAccount account,
            IProviderAuthService authService,
            CancellationToken ct)
        {
            DateTime now = _time.GetUtcNow().UtcDateTime;
            try
            {
                if (await authService.RestoreFromSavedCredentialsAsync(account.Id, ct))
                {
                    SnapshotSyncResult result = await sp.GetRequiredService<ISnapshotSyncService>()
                        .SyncAccountSnapshotsAsync(account.Id, fromUtc: null, toUtc: null, ct);
                    if (result.ErrorMessage is not null)
                    {
                        _logger.LogWarning("Scheduled snapshot sync for {ProviderName} account finished with failures: {Error}", account.ProviderName, result.ErrorMessage);
                    }
                }
                else
                {
                    _logger.LogWarning("Scheduled snapshot sync skipped: saved credentials could not be restored for {ProviderName} account", account.ProviderName);
                }
            }
            finally
            {
                // Uses CancellationToken.None so the run is recorded even when shutdown cancelled the pull.
                await scheduleRepository.RecordSnapshotRunAsync(account.Id, now, ScheduleCalculator.NextSnapshotRunUtc(schedule, now), CancellationToken.None);
            }
        }

        private async Task RunEventSyncAsync(
            IServiceProvider sp,
            ISyncScheduleRepository scheduleRepository,
            SyncSchedule schedule,
            ProviderAccount account,
            IEventAndConfigService eventService,
            IProviderAuthService authService,
            IDeviceDiscoveryService deviceDiscovery,
            IMediaDownloadService mediaService,
            CancellationToken ct)
        {
            string providerName = account.ProviderName;
            Guid accountId = account.Id;

            // Provider API budget: refusal is not an attempt - leave the schedule untouched and retry next tick.
            IProviderApiBudgetGuard budgetGuard = sp.GetRequiredService<IProviderApiBudgetGuard>();
            if (!await budgetGuard.TryConsumeAsync(providerName, ct))
            {
                _logger.LogDebug("Scheduled sync deferred for {ProviderName} account: provider API budget exceeded for this window", providerName);
                return;
            }

            // First sync not finished: run it in place of the incremental pull, so its media validation step still happens.
            // Failures stay on the account and the schedule still advances, so a broken first sync retries once per interval.
            IInitialSyncDispatcher? initialSync = sp.GetService<IInitialSyncDispatcher>();
            if (!account.InitialSyncCompleted && initialSync is not null)
            {
                try
                {
                    await initialSync.RunInitialSyncAsync(accountId, ct);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Scheduled first sync failed for {ProviderName} account", providerName);
                }

                DateTime attemptedUtc = _time.GetUtcNow().UtcDateTime;
                await scheduleRepository.RecordEventRunAsync(accountId, attemptedUtc, ScheduleCalculator.NextEventRunUtc(schedule, attemptedUtc), ct);
                return;
            }

            DateTime startedUtc = _time.GetUtcNow().UtcDateTime;
            string? outcome;
            EventPullResult? counts = null;
            string? syncType = null; // set once the pull's start date is known; stays null if the sync never reached the pull

            try
            {
                // Per-account credentials; the sync-now endpoint relies on an already-restored session, the scheduler cannot.
                if (!await authService.RestoreFromSavedCredentialsAsync(accountId, ct))
                {
                    _logger.LogWarning("Scheduled sync could not restore saved credentials for {ProviderName} account; will retry after the next interval", providerName);
                    outcome = ProviderSyncRunRecorder.RestoreFailedMessage;
                }
                else
                {
                    DateTime? from = AccountSyncRunner.OneMonthFloor(schedule.EventLastRunUtc is { } last
                        ? last - PullOverlap
                        : account.LastSuccessfulAuthUtc, _time.GetUtcNow().UtcDateTime); // first run: same as the manual sync-now endpoint

                    syncType = ProviderSyncRunRecorder.SyncTypeFor(from);

                    // EventPullService is bound to ONE event service and ONE media download service for its provider, so build it per account around that provider's keyed services.
                    var pull = new EventPullService(
                        eventService,
                        sp.GetRequiredService<IDeviceRepository>(),
                        sp.GetRequiredService<ILocationRepository>(),
                        sp.GetRequiredService<IProviderAccountRepository>(),
                        deviceDiscovery,
                        sp.GetRequiredService<ILoggerFactory>().CreateLogger<EventPullService>(),
                        mediaService,
                        deviceConfigRepository: sp.GetRequiredService<IDeviceConfigRepository>());

                    // No completed sync yet: backfill forward a month at a time from a year ago, as the initial sync does.
                    counts = account.LastSyncCompletedUtc is null
                        ? await PullBackfillMonthlyAsync(pull, accountId, ct)
                        : await pull.PullAccountEventsAsync(accountId, from, toTimestampUtc: null, ct);
                    await budgetGuard.RecordCallAsync(providerName, ct);
                    _logger.LogInformation("Scheduled event sync completed for {ProviderName} account", providerName);

                    // Same rule as the manual sync: the pull reports failures by recording them on the account.
                    outcome = ProviderSyncRunRecorder.OutcomeAfterPull(
                        await sp.GetRequiredService<IProviderAccountRepository>().GetAsync(accountId, ct));
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Scheduled event sync failed for {ProviderName} account", providerName);
                outcome = ex.Message;
            }

            // A clean scheduled pull is a completed sync, the same as a manual one, so the Sync tab's Last Sync Completed is current.
            if (outcome is null)
            {
                await MarkSyncCompletedAsync(sp.GetRequiredService<IProviderAccountRepository>(), accountId, _time.GetUtcNow().UtcDateTime, ct);
            }

            await ProviderSyncRunRecorder.RecordAsync(sp.GetRequiredService<IProviderSyncRunRepository>(), _logger, accountId, startedUtc, outcome, syncType, counts);

            // Success or failure, count it as an attempt so a broken account backs off a full interval instead of hammering the provider.
            DateTime now = _time.GetUtcNow().UtcDateTime;
            await scheduleRepository.RecordEventRunAsync(accountId, now, ScheduleCalculator.NextEventRunUtc(schedule, now), ct);
        }
        /// <summary>Backfills one month per pull, oldest first, from a year ago to now. Discovery counts take the largest month; events and failed feeds are summed.</summary>
        private static async Task<EventPullResult> PullBackfillMonthlyAsync(IEventPullService pull, Guid accountId, CancellationToken ct)
        {
            int devices = 0, locations = 0, events = 0, config = 0, failed = 0;
            foreach ((DateTime from, DateTime to) in AccountSyncRunner.MonthlyWindows(DateTime.UtcNow))
            {
                ct.ThrowIfCancellationRequested();
                EventPullResult month = await pull.PullAccountEventsAsync(accountId, from, to, ct);
                devices = Math.Max(devices, month.DevicesSynced);
                locations = Math.Max(locations, month.LocationsSynced);
                config = Math.Max(config, month.ConfigAreasSynced);
                events += month.EventsSynced;
                failed += month.FeedsFailed;
            }

            return new EventPullResult(devices, locations, events, config, failed);
        }

        /// <summary>Stores when a clean sync finished. Best-effort: a failed write is logged and never fails the sync.</summary>
        private async Task MarkSyncCompletedAsync(IProviderAccountRepository accounts, Guid accountId, DateTime completedUtc, CancellationToken ct)
        {
            try
            {
                ProviderAccount? account = await accounts.GetAsync(accountId, CancellationToken.None);
                if (account is null)
                {
                    return;
                }

                account.LastSyncCompletedUtc = completedUtc;
                await accounts.UpdateAsync(account, CancellationToken.None);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Could not record the completed scheduled sync");
            }
        }


        /// <summary>
        /// An enabled schedule whose account was deleted: record the failed attempt and back off a full interval, so the
        /// schedule does not write a failure row on every tick.
        /// </summary>
        private async Task RecordMissingAccountAttemptAsync(IServiceProvider sp, ISyncScheduleRepository scheduleRepository, SyncSchedule schedule, CancellationToken ct)
        {
            DateTime now = _time.GetUtcNow().UtcDateTime;
            await ProviderSyncRunRecorder.RecordAsync(
                sp.GetRequiredService<IProviderSyncRunRepository>(),
                _logger,
                schedule.ProviderAccountId,
                now,
                ProviderSyncRunRecorder.AccountNotFoundMessage);
            await scheduleRepository.RecordEventRunAsync(schedule.ProviderAccountId, now, ScheduleCalculator.NextEventRunUtc(schedule, now), ct);
        }
    }
}