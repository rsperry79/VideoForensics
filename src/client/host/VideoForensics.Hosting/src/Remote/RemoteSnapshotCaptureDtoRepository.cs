using VideoForensics.Core.Api.Contracts;
using VideoForensics.Core.Api.Contracts.Repositories;
using VideoForensics.Data.Common.Contracts;

namespace VideoForensics.Client.Hosting.Remote
{
    /// <summary>Client-side <see cref="ISnapshotCaptureRepository"/> that maps the HTTP-backed entity repository to DTOs.</summary>
    public sealed class RemoteSnapshotCaptureDtoRepository(ISnapshotCaptureWriteRepository captures) : ISnapshotCaptureRepository
    {
        /// <inheritdoc />
        public async Task<SnapshotCaptureDto?> GetAsync(Guid id, CancellationToken ct) =>
            (await captures.GetAsync(id, ct))?.ToDto();

        /// <inheritdoc />
        public async Task<IReadOnlyList<SnapshotCaptureDto>> GetCurrentForDeviceAsync(Guid deviceId, DateTimeOffset from, DateTimeOffset to, CancellationToken ct) =>
            (await captures.GetCurrentForDeviceAsync(deviceId, from, to, ct)).Select(c => c.ToDto()).ToList();

        /// <inheritdoc />
        public async Task<IReadOnlyList<SnapshotCaptureDto>> GetAttemptsForDeviceAsync(Guid deviceId, DateTimeOffset from, DateTimeOffset to, CancellationToken ct) =>
            (await captures.GetAttemptsForDeviceAsync(deviceId, from, to, ct)).Select(c => c.ToDto()).ToList();

        /// <inheritdoc />
        public Task<IReadOnlySet<Guid>> GetDeviceIdsWithAttemptsAsync(IReadOnlyCollection<Guid> deviceIds, DateTimeOffset from, DateTimeOffset to, CancellationToken ct) =>
            captures.GetDeviceIdsWithAttemptsAsync(deviceIds, from, to, ct);
    }
}
