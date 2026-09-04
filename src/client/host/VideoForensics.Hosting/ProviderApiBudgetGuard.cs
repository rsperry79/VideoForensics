using Microsoft.Extensions.Logging;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Hosting
{
    /// <summary>
    /// Self-imposed, cross-host-shared ceiling on provider (Ring) API call volume, so this
    /// application is never the reason a Ring account gets rate-limited or locked out - motivated by
    /// a specific threat (plan §5.12): an attacker could flood the Ring API to blind
    /// DeviceHealthSyncService/jamming detection at the same moment they're physically jamming a
    /// camera's signal. Deliberately conservative, well below Ring's actual limit.
    /// </summary>
    public interface IProviderApiBudgetGuard
    {
        /// <summary>
        /// Checks whether a call is currently within budget, WITHOUT recording it - callers that
        /// end up not actually making the call (e.g. because a different provider/health source in
        /// the same tick already used the budget) should not call RecordCallAsync for it.
        /// </summary>
        Task<bool> TryConsumeAsync(string providerName, CancellationToken ct);

        /// <summary>Records that an outbound provider API call actually happened, for future budget checks.</summary>
        Task RecordCallAsync(string providerName, CancellationToken ct);

        /// <summary>
        /// Escalates a real rate-limit rejection from the provider (reusing existing
        /// IsRateLimitError detection, e.g. in RingMediaDownloadService) as an urgent security
        /// event - "Ring just rejected us" is exactly the "your monitoring may currently be blind"
        /// signal the whole notification pipeline exists for (plan §5.12).
        /// </summary>
        Task RecordRateLimitHitAsync(string providerName, ISecurityAuditLogger auditLog, CancellationToken ct);
    }

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
            var recentCount = await _callLog.CountRecentCallsAsync(providerName, BudgetWindow, ct);
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

        public Task RecordCallAsync(string providerName, CancellationToken ct) =>
            _callLog.RecordCallAsync(providerName, ct);

        public async Task RecordRateLimitHitAsync(string providerName, ISecurityAuditLogger auditLog, CancellationToken ct)
        {
            await auditLog.LogAsync(SecurityAuditEventTypes.ProviderRateLimitHit, null, null, null,
                $"Provider={providerName}", isUrgent: true, ct);
        }

        /// <summary>Opportunistic cleanup - safe to call frequently, only actually deletes when there's stale data to remove.</summary>
        public Task PruneAsync(CancellationToken ct) => _callLog.PruneOlderThanAsync(RetentionWindow, ct);
    }
}
