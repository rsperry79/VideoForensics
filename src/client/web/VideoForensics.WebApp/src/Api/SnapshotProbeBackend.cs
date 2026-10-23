using VideoForensics.Providers.Ring.Entities;
using VideoForensics.Providers.Ring.Services;

using RingSession = VideoForensics.Providers.Ring.Session;

namespace VideoForensics.WebApp.Api
{
    /// <summary>
    /// Operations the snapshot probe needs from a Ring account. Exists so the probe routes can be tested
    /// without a live Ring Session (Session's snapshot methods are not virtual).
    /// </summary>
    public interface ISnapshotProbeBackend
    {
        /// <summary>Downloads the latest stored snapshot for a doorbot.</summary>
        /// <exception cref="SnapshotProbeSessionUnavailableException">No active session for the account.</exception>
        Task<byte[]> GetLatestSnapshotAsync(Guid providerAccountId, long doorbotId, CancellationToken cancellationToken);

        /// <summary>Asks Ring to take a fresh snapshot for a doorbot.</summary>
        /// <exception cref="SnapshotProbeSessionUnavailableException">No active session for the account.</exception>
        Task UpdateSnapshotAsync(Guid providerAccountId, long doorbotId, CancellationToken cancellationToken);

        /// <summary>Battery fields for a doorbot, taken from the existing device listing.</summary>
        /// <exception cref="SnapshotProbeSessionUnavailableException">No active session for the account.</exception>
        Task<SnapshotProbeBattery> GetBatteryAsync(Guid providerAccountId, long doorbotId, CancellationToken cancellationToken);
    }

    /// <summary>Battery fields the probe reports. Null means the device listing did not carry the value.</summary>
    public sealed record SnapshotProbeBattery(long? BatteryLife, decimal? BatteryVoltage);

    /// <summary>Thrown when the probe targets a Ring account that has no active session.</summary>
    public sealed class SnapshotProbeSessionUnavailableException : Exception
    {
        public SnapshotProbeSessionUnavailableException()
            : base("No active Ring session for that account.")
        {
        }
    }

    /// <summary>
    /// Production backend: resolves the per-account Session from ISessionProvider and calls its existing methods.
    /// Credentials are never read here; the Session already holds them.
    /// </summary>
    public sealed class SessionSnapshotProbeBackend : ISnapshotProbeBackend
    {
        private readonly ISessionProvider _sessions;

        public SessionSnapshotProbeBackend(ISessionProvider sessions)
        {
            _sessions = sessions;
        }

        public async Task<byte[]> GetLatestSnapshotAsync(Guid providerAccountId, long doorbotId, CancellationToken cancellationToken)
        {
            RingSession session = RequireSession(providerAccountId);
            using Stream stream = await session.GetLatestSnapshot(checked((int)doorbotId));
            using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer, cancellationToken);
            return buffer.ToArray();
        }

        public async Task UpdateSnapshotAsync(Guid providerAccountId, long doorbotId, CancellationToken cancellationToken)
        {
            RingSession session = RequireSession(providerAccountId);
            await session.UpdateSnapshot(checked((int)doorbotId));
        }

        public async Task<SnapshotProbeBattery> GetBatteryAsync(Guid providerAccountId, long doorbotId, CancellationToken cancellationToken)
        {
            RingSession session = RequireSession(providerAccountId);
            Devices devices = await session.GetRingDevices();
            return FindBattery(devices, doorbotId);
        }

        /// <summary>
        /// Picks the doorbot or stickup cam with the given id from a device listing. Doorbots carry only
        /// battery_life; battery_voltage exists only on stickup cams in the current entity model.
        /// </summary>
        internal static SnapshotProbeBattery FindBattery(Devices devices, long doorbotId)
        {
            Doorbot? doorbot = (devices.Doorbots ?? [])
                .Concat(devices.AuthorizedDoorbots ?? [])
                .FirstOrDefault(d => d.Id == doorbotId);
            StickupCam? cam = (devices.StickupCams ?? [])
                .FirstOrDefault(c => c.Id == doorbotId);

            long? life = doorbot?.BatteryLife is int doorbotLife ? doorbotLife : cam?.BatteryLife;
            return new SnapshotProbeBattery(life, cam?.BatteryVoltage);
        }

        private RingSession RequireSession(Guid providerAccountId)
        {
            return _sessions.GetSession(providerAccountId) ?? throw new SnapshotProbeSessionUnavailableException();
        }
    }
}
