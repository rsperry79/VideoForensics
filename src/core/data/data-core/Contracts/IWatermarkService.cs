namespace VideoForensics.Data.Core.Contracts
{
    /// <summary>Service for resolving incremental download start dates using watermarks.</summary>
    public interface IWatermarkService
    {
        /// <summary>
        /// Resolves the effective start date for pulling events.
        /// If force is true, returns requestedStartDate as-is.
        /// Otherwise, returns max(requestedStartDate, lastSuccessfulPullTime - buffer),
        /// where buffer is a tunable window (default 1 hour) to catch in-flight events.
        /// </summary>
        Task<DateTime> ResolveStartDateAsync(Guid deviceId, DateTime requestedStartDate, bool force, CancellationToken ct);
    }
}
