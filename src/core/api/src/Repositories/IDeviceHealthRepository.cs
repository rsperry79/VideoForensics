namespace VideoForensics.Core.Api.Contracts.Repositories
{
    /// <summary>Device health reads, with DTO signatures. Client hosts use this interface. Writes stay server-side.</summary>
    public interface IDeviceHealthRepository
    {
        /// <summary>Gets the most recent health snapshot for a device, or null.</summary>
        Task<DeviceHealthDto?> GetLatestAsync(Guid deviceId, CancellationToken ct);

        /// <summary>Gets the full health history for a device.</summary>
        Task<IReadOnlyList<DeviceHealthDto>> GetHistoryAsync(Guid deviceId, CancellationToken ct);

        /// <summary>Gets the health history for a device within a time range.</summary>
        Task<IReadOnlyList<DeviceHealthDto>> GetHistoryAsync(Guid deviceId, DateTime fromUtc, DateTime toUtc, CancellationToken ct);
    }
}
