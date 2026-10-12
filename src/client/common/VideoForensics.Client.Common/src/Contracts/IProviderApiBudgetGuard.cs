namespace VideoForensics.Client.Common.Contracts
{
    /// <summary>
    /// Self-imposed, cross-host-shared ceiling on provider API call volume, ensuring the application never causes
    /// rate-limiting or account lockout on provider accounts. Implementations maintain per-provider
    /// call budgets and reject calls when budget is exhausted.
    /// </summary>
    public interface IProviderApiBudgetGuard
    {
        /// <summary>
        /// Checks whether a call to the given provider is currently within budget, WITHOUT recording it.
        /// Callers that end up not actually making the call (e.g. because a different provider already
        /// used the budget in the same tick) should not call RecordCallAsync for it.
        /// </summary>
        /// <param name="providerName">Name of the provider (e.g., "Ring", "Wyze", "Uniview").</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>True if the call can proceed; false if budget is exhausted.</returns>
        Task<bool> TryConsumeAsync(string providerName, CancellationToken ct);

        /// <summary>
        /// Records that an outbound provider API call actually happened, for future budget checks.
        /// </summary>
        /// <param name="providerName">Name of the provider.</param>
        /// <param name="ct">Cancellation token.</param>
        Task RecordCallAsync(string providerName, CancellationToken ct);

        /// <summary>
        /// Escalates a real rate-limit rejection from the provider (reusing existing
        /// IsRateLimitError detection, e.g. in RingMediaDownloadService) as an urgent security
        /// event - "Ring just rejected us" is exactly the "your monitoring may currently be blind"
        /// signal the whole notification pipeline exists for.
        /// </summary>
        /// <param name="providerName">Name of the provider.</param>
        /// <param name="auditLog">Security audit logger for recording the event.</param>
        /// <param name="ct">Cancellation token.</param>
        Task RecordRateLimitHitAsync(string providerName, ISecurityAuditLogger auditLog, CancellationToken ct);
    }
}
