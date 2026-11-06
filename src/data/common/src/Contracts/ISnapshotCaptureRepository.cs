using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Common.Contracts
{
    /// <summary>Repository for per-attempt snapshot capture records.</summary>
    public interface ISnapshotCaptureRepository
    {
        /// <summary>Records one snapshot attempt (current or not).</summary>
        Task<SnapshotCapture> AddAsync(SnapshotCapture capture, CancellationToken ct);

        /// <summary>Gets one capture attempt by its id, or null when no such capture exists.</summary>
        Task<SnapshotCapture?> GetAsync(Guid id, CancellationToken ct);

        /// <summary>
        /// Gets a device's current captures (IsCurrent == true) captured within a date range
        /// (inclusive of both ends), oldest first.
        /// </summary>
        Task<IReadOnlyList<SnapshotCapture>> GetCurrentForDeviceAsync(Guid deviceId, DateTimeOffset from, DateTimeOffset to, CancellationToken ct);

        /// <summary>
        /// Gets every capture attempt for a device (current and failed, 200 and non-200) within a date range
        /// (inclusive of both ends), oldest first. Failed attempts matter to timelines: they end a still's hold.
        /// </summary>
        Task<IReadOnlyList<SnapshotCapture>> GetAttemptsForDeviceAsync(Guid deviceId, DateTimeOffset from, DateTimeOffset to, CancellationToken ct);

        /// <summary>
        /// Gets which of the given devices have at least one capture attempt (current or not) within a date range (inclusive of both ends).
        /// One call covers every device of an account, so the capture loop does not query once per device.
        /// </summary>
        Task<IReadOnlySet<Guid>> GetDeviceIdsWithAttemptsAsync(IReadOnlyCollection<Guid> deviceIds, DateTimeOffset from, DateTimeOffset to, CancellationToken ct);
    }
}
