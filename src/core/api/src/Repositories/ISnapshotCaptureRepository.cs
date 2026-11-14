namespace VideoForensics.Core.Api.Contracts.Repositories
{
    /// <summary>Snapshot capture reads, with DTO signatures. Client hosts use this interface. Writes stay server-side.</summary>
    public interface ISnapshotCaptureRepository
    {
        /// <summary>Gets one snapshot capture attempt by id, or null.</summary>
        Task<SnapshotCaptureDto?> GetAsync(Guid id, CancellationToken ct);

        /// <summary>Gets the current (200) snapshots for a device within a time range.</summary>
        Task<IReadOnlyList<SnapshotCaptureDto>> GetCurrentForDeviceAsync(Guid deviceId, DateTimeOffset from, DateTimeOffset to, CancellationToken ct);

        /// <summary>Gets every snapshot attempt for a device within a time range.</summary>
        Task<IReadOnlyList<SnapshotCaptureDto>> GetAttemptsForDeviceAsync(Guid deviceId, DateTimeOffset from, DateTimeOffset to, CancellationToken ct);

        /// <summary>Gets the ids of the devices, from the given set, that have any attempt within a time range.</summary>
        Task<IReadOnlySet<Guid>> GetDeviceIdsWithAttemptsAsync(IReadOnlyCollection<Guid> deviceIds, DateTimeOffset from, DateTimeOffset to, CancellationToken ct);
    }
}
