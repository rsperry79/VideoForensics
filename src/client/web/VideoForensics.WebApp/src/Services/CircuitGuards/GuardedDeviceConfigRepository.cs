using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Ui.Shared.Layout;
using VideoForensics.WebApp.Contracts;

namespace VideoForensics.WebApp.Services.CircuitGuards
{
    /// <summary>
    /// Guards writing a device-config snapshot, which the Device Configuration page makes in-process. Matches the
    /// device-config API, which requires SuperAdminLocal. Reads stay open.
    /// </summary>
    public sealed class GuardedDeviceConfigRepository(IDeviceConfigRepository inner, ICircuitOperatorGate gate) : IDeviceConfigRepository
    {
        public Task<DeviceConfigSnapshot?> GetAsync(Guid snapshotId, CancellationToken ct) => inner.GetAsync(snapshotId, ct);

        public Task<DeviceConfigSnapshot> AppendSnapshotAsync(DeviceConfigSnapshot snapshot, CancellationToken ct)
        {
            if (!gate.Allows(AccessPolicy.SuperAdminLocal))
            {
                throw new UnauthorizedAccessException("Saving device configuration requires SuperAdminLocal.");
            }

            return inner.AppendSnapshotAsync(snapshot, ct);
        }

        public Task<DeviceConfigSnapshot?> GetLatestAsync(Guid deviceId, CancellationToken ct) => inner.GetLatestAsync(deviceId, ct);

        public Task<IReadOnlyList<DeviceConfigSnapshot>> GetHistoryAsync(Guid deviceId, CancellationToken ct) => inner.GetHistoryAsync(deviceId, ct);

        public Task<IReadOnlyList<DeviceConfigSnapshot>> ListAsync(CancellationToken ct) => inner.ListAsync(ct);
    }
}
