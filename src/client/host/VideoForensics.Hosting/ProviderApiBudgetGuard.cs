using Microsoft.Extensions.Logging;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Hosting
{
    public class ProviderApiBudgetGuard : IProviderApiBudgetGuard
    {
        // Deliberately conservative - well below any known Ring per-account rate limit, so this
        // guard trips before the real one does, giving a chance to back off and log instead of
        // finding out only after Ring itself rejects requests.
        private const int BudgetCeiling = 200;
        private static readonly TimeSpan BudgetWindow = TimeSpan.FromMinutes(15);
        private static readonly TimeSpan RetentionWindow = TimeSpan.FromDays(1);

        private readonly IProviderApiCallLogRepository _callLog;
        private readonly ILogger<ProviderApiBudgetGuard> _logger;

        public ProviderApiBudgetGuard(IProviderApiCallLogRepository callLog, ILogger<ProviderApiBudgetGuard> logger)
        {
            _callLog = callLog;
            _logger = logger;
        }

        public async Task<bool> TryConsumeAsync(string providerName, CancellationToken ct)
        {
            int recentCount = await _callLog.CountRecentCallsAsync(providerName, BudgetWindow, ct);
            if (recentCount >= BudgetCeiling)
            {
                _logger.LogWarning("Provider API budget exceeded for {ProviderName}: {Count} calls in the last {Window}. Backing off.",
                    providerName, recentCount, BudgetWindow);
                return false;
            }

            // Anomalous-volume detection: more than half the budget used before the window is even
            // half over is worth a heads-up independent of whether Ring has rejected anything yet.
            if (recentCount >= BudgetCeiling / 2)
            {
                _logger.LogWarning("Provider API call volume for {ProviderName} is unusually high: {Count}/{Ceiling} in the current window.",
                    providerName, recentCount, BudgetCeiling);
            }

            return true;
        }

        public Task RecordCallAsync(string providerName, CancellationToken ct)
        {
            return _callLog.RecordCallAsync(providerName, ct);
        }

        public async Task RecordRateLimitHitAsync(string providerName, ISecurityAuditLogger auditLog, CancellationToken ct)
        {
            await auditLog.LogAsync(SecurityAuditEventTypes.ProviderRateLimitHit, null, null, null,
                $"Provider={providerName}", isUrgent: true, ct);
        }

        /// <summary>Opportunistic cleanup - safe to call frequently, only actually deletes when there's stale data to remove.</summary>
        public Task PruneAsync(CancellationToken ct)
        {
            return _callLog.PruneOlderThanAsync(RetentionWindow, ct);
        }
    }
}
