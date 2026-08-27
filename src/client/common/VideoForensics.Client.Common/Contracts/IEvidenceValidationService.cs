using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Client.Common
{
    /// <summary>Results from verifying local file integrity or provider reconciliation.</summary>
    public class MediaVerificationResult
    {
        public required Guid MediaItemId { get; set; }
        public required string FileName { get; set; }
        public required string Status { get; set; } // "verified", "failed", "missing"
        public string? FailureReason { get; set; }
    }

    /// <summary>Service for validating evidence files (local integrity + provider reconciliation).</summary>
    public interface IEvidenceValidationService
    {
        /// <summary>
        /// Re-verifies the SHA-256 hash of downloaded files against their stored hashes.
        /// </summary>
        /// <param name="deviceId">Device to verify (null = all devices).</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>List of verification results per device/file.</returns>
        Task<IReadOnlyList<MediaVerificationResult>> VerifyLocalIntegrityAsync(Guid? deviceId, CancellationToken ct);

        /// <summary>
        /// Reconciles stored events against the provider's current record, detecting changes/deletions.
        /// </summary>
        /// <param name="deviceId">Guid device ID from the data layer.</param>
        /// <param name="providerDeviceId">Provider's string device ID (e.g. Ring doorbot ID).</param>
        /// <param name="fromUtc">Start of date range to reconcile.</param>
        /// <param name="toUtc">End of date range to reconcile.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>List of discrepancies found (persisted via IProviderReconciliationService).</returns>
        Task<IReadOnlyList<ReconciliationDiscrepancy>> ReconcileWithProviderAsync(
            Guid deviceId,
            string providerDeviceId,
            DateTime fromUtc,
            DateTime toUtc,
            CancellationToken ct);
    }
}
