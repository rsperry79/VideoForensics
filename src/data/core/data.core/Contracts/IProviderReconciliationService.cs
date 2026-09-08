using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Core.Contracts
{
    /// <summary>Service for recording provider reconciliation findings (discrepancies between stored and live provider data).</summary>
    public interface IProviderReconciliationService
    {
        /// <summary>
        /// Records a batch of reconciliation discrepancies for a device, writing them atomically via IUnitOfWork
        /// and logging a single ActionLog summary entry.
        /// </summary>
        Task RecordReconciliationRunAsync(
            Guid deviceId,
            IReadOnlyList<ReconciliationDiscrepancy> discrepancies,
            CancellationToken ct);

        /// <summary>Gets the reconciliation history for a device.</summary>
        Task<IReadOnlyList<ProviderReconciliationRecord>> GetHistoryAsync(Guid deviceId, CancellationToken ct);

        /// <summary>
        /// Auto-fix discrepancies by inserting new events and updating changed metadata from provider data.
        /// Returns count of items fixed.
        /// </summary>
        Task<AutoFixResult> AutoFixDiscrepanciesAsync(
            Guid deviceId,
            IReadOnlyList<ReconciliationDiscrepancy> discrepancies,
            Func<string, DateTime, DateTime, CancellationToken, Task<IReadOnlyList<Event>>> fetchEventsFunc,
            CancellationToken ct);
    }

    /// <summary>Result of auto-fixing reconciliation discrepancies.</summary>
    public class AutoFixResult
    {
        public int NewEventsInserted { get; set; }
        public int MetadataUpdated { get; set; }
        public int Failed { get; set; }
        public List<string> ErrorDetails { get; set; } = new();
    }
}
