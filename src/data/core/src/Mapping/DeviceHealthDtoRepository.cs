using VideoForensics.Data.Common.Contracts;

namespace VideoForensics.Core.Api.Contracts.Repositories
{
    /// <summary>Server-side <see cref="IDeviceHealthRepository"/> over <see cref="IDeviceHealthWriteRepository"/>, mapping to DTOs.</summary>
    public sealed class DeviceHealthDtoRepository(IDeviceHealthWriteRepository health) : IDeviceHealthRepository
    {
        /// <inheritdoc />
        public async Task<DeviceHealthDto?> GetLatestAsync(Guid deviceId, CancellationToken ct) =>
            (await health.GetLatestAsync(deviceId, ct))?.ToDto();

        /// <inheritdoc />
        public async Task<IReadOnlyList<DeviceHealthDto>> GetHistoryAsync(Guid deviceId, CancellationToken ct) =>
            (await health.GetHistoryAsync(deviceId, ct)).Select(h => h.ToDto()).ToList();

        /// <inheritdoc />
        public async Task<IReadOnlyList<DeviceHealthDto>> GetHistoryAsync(Guid deviceId, DateTime fromUtc, DateTime toUtc, CancellationToken ct) =>
            (await health.GetHistoryAsync(deviceId, fromUtc, toUtc, ct)).Select(h => h.ToDto()).ToList();
    }
}
