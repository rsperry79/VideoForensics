using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Client.Common.Contracts
{
    /// <summary>Which evidence validation run a job performs.</summary>
    public enum EvidenceValidationJobKind
    {
        /// <summary>Local SHA-256 re-verification of downloaded files.</summary>
        Integrity,

        /// <summary>Reconciliation of stored events against the provider.</summary>
        Reconcile
    }

    /// <summary>Lifecycle state of an evidence validation job.</summary>
    public enum EvidenceValidationJobState
    {
        /// <summary>The job is still doing work.</summary>
        Running,

        /// <summary>The job finished and its results are available.</summary>
        Succeeded,

        /// <summary>The job stopped on an error; see <see cref="EvidenceValidationJob.ErrorMessage"/>.</summary>
        Failed
    }

    /// <summary>
    /// Immutable snapshot of an evidence validation job. Each read returns a fresh copy, so callers never see
    /// the log or result lists change under them.
    /// </summary>
    public sealed class EvidenceValidationJob
    {
        /// <summary>Unique job identifier.</summary>
        public required Guid Id { get; init; }

        /// <summary>Which validation run this job performs.</summary>
        public required EvidenceValidationJobKind Kind { get; init; }

        /// <summary>Current lifecycle state.</summary>
        public required EvidenceValidationJobState State { get; init; }

        /// <summary>When the job started (UTC).</summary>
        public required DateTime StartedUtc { get; init; }

        /// <summary>When the job finished (UTC); null while running.</summary>
        public DateTime? CompletedUtc { get; init; }

        /// <summary>Log lines, oldest first, each prefixed with a UTC HH:mm:ss timestamp.</summary>
        public IReadOnlyList<string> Log { get; init; } = Array.Empty<string>();

        /// <summary>Integrity results; null unless this is a completed integrity job.</summary>
        public IReadOnlyList<MediaVerificationResult>? IntegrityResults { get; init; }

        /// <summary>Reconciliation discrepancies; null unless this is a completed reconcile job.</summary>
        public IReadOnlyList<ReconciliationDiscrepancy>? Discrepancies { get; init; }

        /// <summary>Failure message when <see cref="State"/> is Failed; otherwise null.</summary>
        public string? ErrorMessage { get; init; }
    }

    /// <summary>
    /// Runs evidence validation as app-lifetime jobs so the work survives the page that started it.
    /// Keeps the latest job per kind in memory.
    /// </summary>
    public interface IEvidenceValidationJobs
    {
        /// <summary>
        /// Starts a local integrity check, or returns the running one if an integrity check is already in progress.
        /// </summary>
        /// <param name="deviceId">Device to verify (null = all devices).</param>
        /// <param name="ct">Cancellation token for the start call only; the job itself is not cancelled by it.</param>
        /// <returns>Snapshot of the started or already-running job.</returns>
        Task<EvidenceValidationJob> StartIntegrityAsync(Guid? deviceId, CancellationToken ct);

        /// <summary>
        /// Starts one local integrity job that verifies each listed device in turn, or returns the running integrity job.
        /// Results from every device land in the one job, each tagged with its <see cref="MediaVerificationResult.DeviceId"/>.
        /// </summary>
        /// <param name="deviceIds">Devices to verify; must not be empty.</param>
        /// <param name="ct">Cancellation token for the start call only; the job itself is not cancelled by it.</param>
        /// <returns>Snapshot of the started or already-running job.</returns>
        Task<EvidenceValidationJob> StartIntegrityForDevicesAsync(IReadOnlyList<Guid> deviceIds, CancellationToken ct);

        /// <summary>
        /// Starts a provider reconciliation, or returns the running one if a reconcile is already in progress.
        /// </summary>
        /// <param name="deviceId">Guid device ID from the data layer.</param>
        /// <param name="providerDeviceId">Provider's string device ID.</param>
        /// <param name="fromUtc">Start of date range to reconcile.</param>
        /// <param name="toUtc">End of date range to reconcile.</param>
        /// <param name="ct">Cancellation token for the start call only; the job itself is not cancelled by it.</param>
        /// <returns>Snapshot of the started or already-running job.</returns>
        Task<EvidenceValidationJob> StartReconcileAsync(
            Guid deviceId,
            string providerDeviceId,
            DateTime fromUtc,
            DateTime toUtc,
            CancellationToken ct);

        /// <summary>Returns a snapshot of the latest job of the given kind, or null if none has run.</summary>
        /// <param name="kind">Which validation run to look up.</param>
        /// <param name="ct">Cancellation token.</param>
        Task<EvidenceValidationJob?> GetLatestAsync(EvidenceValidationJobKind kind, CancellationToken ct);
    }
}
