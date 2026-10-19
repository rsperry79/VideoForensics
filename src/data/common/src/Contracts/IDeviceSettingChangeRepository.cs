using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Common.Contracts
{
    /// <summary>Repository for the append-only device setting change history.</summary>
    public interface IDeviceSettingChangeRepository
    {
        /// <summary>Appends one or more device setting change records.</summary>
        Task AddRangeAsync(IEnumerable<DeviceSettingChange> changes, CancellationToken ct);

        /// <summary>Lists the change records for a device, newest first.</summary>
        Task<IReadOnlyList<DeviceSettingChange>> ListByDeviceAsync(Guid deviceId, CancellationToken ct);
    }
}
