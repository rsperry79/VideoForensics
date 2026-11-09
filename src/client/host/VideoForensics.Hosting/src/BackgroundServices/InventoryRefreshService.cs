using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Client.Hosting.Services;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Client.Hosting.BackgroundServices
{
    /// <summary>
    /// Refreshes the provider inventory (locations, devices and device configuration) for each account with an enabled schedule.
    /// Runs on its own timer, not inside the scheduler tick: a slow inventory (about four minutes on the server) would otherwise
    /// exceed the tick watchdog and starve the event pull and the time-lapse check. The event pull reads the devices stored here.
    ///
    /// Server-tier only, registered through AddVideoForensicsScheduledSync next to <see cref="ScheduledSyncService"/>. Logs use the
    /// provider name, never the account GUID.
    /// </summary>
    public class InventoryRefreshService : BackgroundService
    {
        /// <summary>How often the inventory is refreshed. Fixed, not configurable.</summary>
        internal static readonly TimeSpan InventoryRefreshInterval = TimeSpan.FromMinutes(60);

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IOptionsMonitor<ScheduledTasksOptions> _options;
        private readonly ILogger<InventoryRefreshService> _logger;
        private readonly TimeProvider _time;
        private readonly ThreadHeartbeats? _heartbeats;

        /// <summary>Creates the refresher.</summary>
        public InventoryRefreshService(
            IServiceScopeFactory scopeFactory,
            IOptionsMonitor<ScheduledTasksOptions> options,
            ILogger<InventoryRefreshService> logger,
            TimeProvider time,
            ThreadHeartbeats? heartbeats = null)
        {
            _scopeFactory = scopeFactory;
            _options = options;
            _logger = logger;
            _time = time;
            _heartbeats = heartbeats;
        }

        /// <inheritdoc />
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                // Runs once at startup, then once per interval.
                while (true)
                {
                    await RunRecordedRefreshAsync(stoppingToken);
                    await Task.Delay(InventoryRefreshInterval, _time, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Normal shutdown.
            }
        }

        /// <summary>Runs one refresh and records it: a beat when it starts, a run when it completes, or an error label when it fails.</summary>
        private async Task RunRecordedRefreshAsync(CancellationToken ct)
        {
            string name = nameof(InventoryRefreshService);
            _heartbeats?.Beat(name, _time.GetUtcNow(), InventoryRefreshInterval);
            try
            {
                int failedAccounts = await RefreshOnceAsync(ct);
                _heartbeats?.RecordFailedItems(name, failedAccounts);
                _heartbeats?.RecordRun(name, _time.GetUtcNow());
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _heartbeats?.RecordError(name, _time.GetUtcNow(), "refresh failed");
                _logger.LogError("Inventory refresh failed; will retry after the next interval: {Message} ({ErrorType})", ex.Message, ex.GetType().Name);
            }
        }

        /// <summary>
        /// Refreshes the inventory of each account with an enabled schedule, one at a time. The filters match the scheduler's: an
        /// active, not removed account whose provider is in ScheduledTasks:EnabledProviders. One account's failure does not stop the others.
        /// </summary>
        /// <returns>How many accounts failed in this pass.</returns>
        internal async Task<int> RefreshOnceAsync(CancellationToken ct)
        {
            using IServiceScope scope = _scopeFactory.CreateScope();
            IServiceProvider sp = scope.ServiceProvider;

            IReadOnlyList<SyncSchedule> schedules = await sp.GetRequiredService<ISyncScheduleRepository>().ListEnabledAsync(ct);
            IProviderAccountRepository accounts = sp.GetRequiredService<IProviderAccountRepository>();
            IProviderApiBudgetGuard budgetGuard = sp.GetRequiredService<IProviderApiBudgetGuard>();
            string[] enabledProviders = _options.CurrentValue.EnabledProviders ?? [];
            int failed = 0;

            foreach (SyncSchedule schedule in schedules)
            {
                ct.ThrowIfCancellationRequested();
                string providerName = "unknown provider";
                try
                {
                    ProviderAccount? account = await accounts.GetAsync(schedule.ProviderAccountId, ct);
                    if (account is null || account.RemovedUtc != null || !account.IsActive)
                    {
                        continue;
                    }

                    providerName = account.ProviderName;
                    string? key = enabledProviders.FirstOrDefault(p => string.Equals(p, providerName, StringComparison.OrdinalIgnoreCase));
                    if (key == null)
                    {
                        continue;
                    }

                    IInventorySyncService? inventorySync = sp.GetKeyedService<IInventorySyncService>(key);
                    if (inventorySync == null)
                    {
                        _logger.LogWarning("Inventory refresh skipped: no keyed inventory service is registered for {ProviderName}", providerName);
                        continue;
                    }

                    // Discovery calls count against the provider API budget, as they did inside the scheduled run.
                    if (!await budgetGuard.TryConsumeAsync(providerName, ct))
                    {
                        _logger.LogDebug("Inventory refresh deferred for {ProviderName} account: provider API budget exceeded for this window", providerName);
                        continue;
                    }

                    InventorySyncResult inventory = await inventorySync.SyncAsync(account.Id, ct);
                    if (inventory.DeviceConfigFailures > 0)
                    {
                        _logger.LogWarning("Device configuration could not be read for {ProviderName} account: {Count} device(s); the inventory was synced", providerName, inventory.DeviceConfigFailures);
                    }
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    failed++;
                    _logger.LogError("Inventory refresh failed for {ProviderName} account: {Message} ({ErrorType})", providerName, ex.Message, ex.GetType().Name);
                }
            }

            return failed;
        }
    }
}
