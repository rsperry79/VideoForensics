using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Common.Contracts
{
    /// <summary>Repository for per-attempt snapshot capture records.</summary>
    public interface ISnapshotCaptureRepository
    {
        /// <summary>Records one snapshot attempt (current or not).</summary>
        Task<SnapshotCapture> AddAsync(SnapshotCapture capture, CancellationToken ct);

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
    }
}
