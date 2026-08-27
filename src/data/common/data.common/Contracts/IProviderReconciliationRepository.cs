using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Common.Contracts
{
    /// <summary>Repository for append-only provider reconciliation records.</summary>
    public interface IProviderReconciliationRepository
    {
        /// <summary>Gets a provider reconciliation record by ID.</summary>
        Task<ProviderReconciliationRecord?> GetAsync(Guid recordId, CancellationToken ct);

        /// <summary>Appends a new provider reconciliation record.</summary>
        Task<ProviderReconciliationRecord> AppendAsync(ProviderReconciliationRecord record, CancellationToken ct);

        /// <summary>Gets the history of reconciliation records for a device.</summary>
        Task<IReadOnlyList<ProviderReconciliationRecord>> GetHistoryForDeviceAsync(Guid deviceId, CancellationToken ct);

        /// <summary>Gets open (unreviewed) discrepancies across all devices.</summary>
        Task<IReadOnlyList<ProviderReconciliationRecord>> GetOpenDiscrepanciesAsync(CancellationToken ct);

        /// <summary>Lists all provider reconciliation records.</summary>
        Task<IReadOnlyList<ProviderReconciliationRecord>> ListAsync(CancellationToken ct);
    }
}
