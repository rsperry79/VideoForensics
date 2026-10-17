using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
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
                    if (!account.IsActive)
                    {
                        _logger.LogWarning("Scheduled sync skipped: {ProviderName} account is inactive", providerName);
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

                    if (!ScheduleCalculator.IsEventDue(schedule, _time.GetUtcNow().UtcDateTime))
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
                _logger.LogInformation("Scheduled sync deferred for {ProviderName} account: provider API budget exceeded for this window", providerName);
                return;
            }

            DateTime startedUtc = _time.GetUtcNow().UtcDateTime;
            string? outcome;
            SyncRunTotals? totals = null;

            // Same DI scope as every account in this tick, so reset before each pull to keep one account's counts out of the next.
            ISyncRunTally? tally = sp.GetService<ISyncRunTally>();

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
                    DateTime? from = schedule.EventLastRunUtc is { } last
                        ? last - PullOverlap
                        : account.LastSuccessfulAuthUtc; // first run: same as the manual sync-now endpoint

                    // EventPullService is bound to ONE event service and ONE media service (which does the actual event/media pull), so build it per account around its provider's keyed services.
                    var pull = new EventPullService(
                        eventService,
                        sp.GetRequiredService<IDeviceRepository>(),
                        sp.GetRequiredService<ILocationRepository>(),
                        sp.GetRequiredService<IProviderAccountRepository>(),
                        deviceDiscovery,
                        sp.GetRequiredService<ILoggerFactory>().CreateLogger<EventPullService>(),
                        tally,
                        sp.GetService<ISyncRunOptions>(),
                        mediaService);

                    tally?.Reset();
                    await pull.PullAccountEventsAsync(accountId, from, ct);
                    totals = tally?.Snapshot();
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

            await ProviderSyncRunRecorder.RecordAsync(sp.GetRequiredService<IProviderSyncRunRepository>(), _logger, accountId, startedUtc, outcome, totals);

            // Success or failure, count it as an attempt so a broken account backs off a full interval instead of hammering the provider.
            DateTime now = _time.GetUtcNow().UtcDateTime;
            await scheduleRepository.RecordEventRunAsync(accountId, now, ScheduleCalculator.NextEventRunUtc(schedule, now), ct);
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