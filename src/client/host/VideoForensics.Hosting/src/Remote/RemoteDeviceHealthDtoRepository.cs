using VideoForensics.Core.Api.Contracts;
using VideoForensics.Core.Api.Contracts.Repositories;
using VideoForensics.Data.Common.Contracts;

namespace VideoForensics.Client.Hosting.Remote
{
    /// <summary>Client-side <see cref="IDeviceHealthRepository"/> that maps the HTTP-backed entity repository to DTOs.</summary>
    public sealed class RemoteDeviceHealthDtoRepository(IDeviceHealthWriteRepository health) : IDeviceHealthRepository
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
