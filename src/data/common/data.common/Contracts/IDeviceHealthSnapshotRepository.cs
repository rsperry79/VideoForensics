using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Common.Contracts
{
    /// <summary>Repository for append-only device health/connectivity telemetry snapshots.</summary>
    public interface IDeviceHealthSnapshotRepository
    {
        /// <summary>Appends a new device health snapshot.</summary>
        Task<DeviceHealthSnapshot> AppendSnapshotAsync(DeviceHealthSnapshot snapshot, CancellationToken ct);

        /// <summary>Gets the nearest snapshot at or before the given time - the "last known state" going into a gap.</summary>
        Task<DeviceHealthSnapshot?> GetLatestBeforeAsync(Guid deviceId, DateTime atOrBeforeUtc, CancellationToken ct);

        /// <summary>Gets the full snapshot history for a device, newest first.</summary>
        Task<IReadOnlyList<DeviceHealthSnapshot>> GetHistoryAsync(Guid deviceId, CancellationToken ct);
    }
}
