using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Common.Contracts
{
    /// <summary>Repository for append-only device configuration snapshots.</summary>
    public interface IDeviceConfigRepository
    {
        /// <summary>Gets a device config snapshot by ID.</summary>
        Task<DeviceConfigSnapshot?> GetAsync(Guid snapshotId, CancellationToken ct);

        /// <summary>Appends a new device configuration snapshot.</summary>
        Task<DeviceConfigSnapshot> AppendSnapshotAsync(DeviceConfigSnapshot snapshot, CancellationToken ct);

        /// <summary>Gets the latest device configuration snapshot for a device.</summary>
        Task<DeviceConfigSnapshot?> GetLatestAsync(Guid deviceId, CancellationToken ct);

        /// <summary>Gets the full history of device configuration snapshots for a device.</summary>
        Task<IReadOnlyList<DeviceConfigSnapshot>> GetHistoryAsync(Guid deviceId, CancellationToken ct);

        /// <summary>Lists all device config snapshots.</summary>
        Task<IReadOnlyList<DeviceConfigSnapshot>> ListAsync(CancellationToken ct);
    }
}
