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
    }
}
