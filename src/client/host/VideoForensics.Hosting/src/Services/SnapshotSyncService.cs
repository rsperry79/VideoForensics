using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using VideoForensics.Client.Hosting.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Client.Hosting.Services
{
    /// <inheritdoc />
    public class SnapshotSyncService : ISnapshotSyncService
    {
        private readonly IServiceProvider _services;
        private readonly IProviderAccountRepository _accounts;
        private readonly ILogger<SnapshotSyncService> _logger;

        /// <summary>Creates the service.</summary>
        public SnapshotSyncService(
            IServiceProvider services,
            IProviderAccountRepository accounts,
            ILogger<SnapshotSyncService> logger)
        {
            _services = services;
            _accounts = accounts;
            _logger = logger;
        }

        /// <inheritdoc />
        public async Task<SnapshotCycle.TickCounts> RunCycleAsync(CancellationToken cancellationToken)
        {
            // The cycle is a process-lifetime singleton (its pauses and holds must survive ticks), so it is resolved here
            // rather than injected: this service is scoped, and the timer that calls it has no scope-bound state to share.
            SnapshotCycle cycle = _services.GetRequiredService<SnapshotCycle>();
            var counts = new SnapshotCycle.TickCounts();
            IReadOnlyList<ProviderAccount> accounts = await _accounts.ListActiveAsync(cancellationToken);
            _logger.LogDebug("Snapshot capture tick started: {AccountCount} active accounts", accounts.Count);

            foreach (ProviderAccount account in accounts)
            {
                if (account.RemovedUtc != null || !account.IsActive
                    || !string.Equals(account.ProviderName, SnapshotCycle.RingProviderName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    await cycle.CaptureAccountAsync(_services, account, counts, cancellationToken);
                    // The time-lapse horizon reads this: a pass that threw does not move it.
                    cycle.MarkPassCompleted(account.Id);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    // One account's failure must never stop the others.
                    cycle.HoldAccountAfterError(account.Id);
                    counts.AccountFailed = true;
                    _logger.LogError("Snapshot capture failed for {ProviderName} account: {Message} ({ErrorType})", account.ProviderName, ex.Message, ex.GetType().Name);
                }
            }

            return counts;
        }
    }
}
