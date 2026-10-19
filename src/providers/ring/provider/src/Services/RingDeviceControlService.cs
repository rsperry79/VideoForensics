using Microsoft.Extensions.Logging;

using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Providers.Ring.Entities;

using CommonContracts = VideoForensics.Core.Providers.Common.Contracts;
using DeviceEntity = VideoForensics.Data.Common.Entities.Device;

namespace VideoForensics.Providers.Ring.Services
{
    /// <summary>
    /// Provider-agnostic device controls implemented over the Ring SDK session. The provider device id is
    /// Ring's numeric doorbot/chime id; non-numeric ids are rejected with ArgumentException.
    /// Each control runs on the single Ring account linked to the device's location; the session is never
    /// chosen by "whichever account is current", so a multi-account host cannot control the wrong account.
    /// </summary>
    public class RingDeviceControlService : CommonContracts.IDeviceControlService
    {
        private const string RingProviderName = "Ring";

        private readonly ILogger _logger;
        private readonly ISessionProvider _sessionProvider;
        private readonly IDeviceRepository _devices;
        private readonly ILocationRepository _locations;

        public RingDeviceControlService(ILogger logger, ISessionProvider sessionProvider, IDeviceRepository devices, ILocationRepository locations)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _sessionProvider = sessionProvider ?? throw new ArgumentNullException(nameof(sessionProvider));
            _devices = devices ?? throw new ArgumentNullException(nameof(devices));
            _locations = locations ?? throw new ArgumentNullException(nameof(locations));
        }

        public async Task SetLightAsync(string providerDeviceId, bool on, CancellationToken ct)
        {
            long id = ParseId(providerDeviceId);
            Session session = await ResolveSessionAsync(providerDeviceId, ct);
            string name = await ResolveDoorbotNameAsync(session, id, ct);
            await session.SetLight(id, on, ct);
            Audit(name, "Light");
        }

        public async Task SetSirenAsync(string providerDeviceId, bool on, int? durationSeconds, CancellationToken ct)
        {
            long id = ParseId(providerDeviceId);
            Session session = await ResolveSessionAsync(providerDeviceId, ct);
            string name = await ResolveDoorbotNameAsync(session, id, ct);
            await session.SetSiren(id, on, durationSeconds, ct);
            Audit(name, "Siren");
        }

        public async Task SetVolumeAsync(string providerDeviceId, int volume, CancellationToken ct)
        {
            long id = ParseId(providerDeviceId);
            Session session = await ResolveSessionAsync(providerDeviceId, ct);
            string name = await ResolveDoorbotNameAsync(session, id, ct);
            await session.SetVolume(id, volume, name, ct);
            Audit(name, "Volume");
        }

        public async Task SetNightModeAsync(string providerDeviceId, bool enabled, CancellationToken ct)
        {
            long id = ParseId(providerDeviceId);
            Session session = await ResolveSessionAsync(providerDeviceId, ct);
            string name = await ResolveDoorbotNameAsync(session, id, ct);
            await session.SetNightMode(id, enabled, ct);
            Audit(name, "NightMode");
        }

        public async Task SetMotionDetectionAsync(string providerDeviceId, bool enabled, CancellationToken ct)
        {
            long id = ParseId(providerDeviceId);
            Session session = await ResolveSessionAsync(providerDeviceId, ct);
            string name = await ResolveDoorbotNameAsync(session, id, ct);
            await session.SetMotionDetection(id, enabled, ct);
            Audit(name, "MotionDetection");
        }

        public async Task SetDoNotDisturbAsync(string providerDeviceId, int seconds, CancellationToken ct)
        {
            long id = ParseId(providerDeviceId);
            Session session = await ResolveSessionAsync(providerDeviceId, ct);
            string name = await ResolveChimeNameAsync(session, id, ct);
            await session.SetDoNotDisturb(id, seconds, ct);
            Audit(name, "DoNotDisturb");
        }

        private static long ParseId(string providerDeviceId)
        {
            if (!long.TryParse(providerDeviceId, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out long id))
            {
                throw new ArgumentException("Provider device id must be a numeric Ring id.", nameof(providerDeviceId));
            }

            return id;
        }

        // Resolves the device by provider id, then the Ring account linked to its location. Fails closed:
        // zero or several Ring accounts throw before any provider call, so a control cannot land on the wrong account.
        private async Task<Session> ResolveSessionAsync(string providerDeviceId, CancellationToken ct)
        {
            IReadOnlyList<DeviceEntity> rows = await _devices.ListAsync(ct);
            DeviceEntity device = rows.FirstOrDefault(d => string.Equals(d.ProviderDeviceId, providerDeviceId, StringComparison.Ordinal))
                ?? throw new KeyNotFoundException("Device was not found.");

            IReadOnlyList<ProviderAccount> linked = await _locations.ListProviderAccountsForLocationAsync(device.LocationId, ct);
            List<ProviderAccount> ringAccounts = linked
                .Where(a => string.Equals(a.ProviderName, RingProviderName, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (ringAccounts.Count == 0)
            {
                throw new KeyNotFoundException("No Ring account is linked to this device.");
            }

            if (ringAccounts.Count > 1)
            {
                throw new InvalidOperationException("Device is linked to more than one Ring account; control refused");
            }

            return _sessionProvider.GetSession(ringAccounts[0].Id)
                ?? throw new InvalidOperationException("Ring account is not signed in.");
        }

        // Ring's device-settings calls need the device's display name (volume PUT sends it as
        // doorbot[description]), so the name is looked up from the device list before each control.
        private static async Task<string> ResolveDoorbotNameAsync(Session session, long id, CancellationToken ct)
        {
            Devices devices = await session.GetRingDevices(null);
            ct.ThrowIfCancellationRequested();

            IEnumerable<(long Id, string? Description)> candidates = (devices.Doorbots ?? new List<Doorbot>())
                .Concat(devices.AuthorizedDoorbots ?? new List<Doorbot>())
                .Select(d => ((long)d.Id, (string?)d.Description))
                .Concat((devices.StickupCams ?? new List<StickupCam>()).Where(c => c.Id.HasValue).Select(c => (c.Id!.Value, (string?)c.Description)));

            return FindName(candidates, id);
        }

        private static async Task<string> ResolveChimeNameAsync(Session session, long id, CancellationToken ct)
        {
            Devices devices = await session.GetRingDevices(null);
            ct.ThrowIfCancellationRequested();

            IEnumerable<(long Id, string? Description)> candidates = (devices.Chimes ?? new List<Chime>())
                .Select(c => ((long)c.Id, (string?)c.Description));

            return FindName(candidates, id);
        }

        private static string FindName(IEnumerable<(long Id, string? Description)> candidates, long id)
        {
            foreach ((long candidateId, string? description) in candidates)
            {
                if (candidateId == id)
                {
                    return description ?? string.Empty;
                }
            }

            throw new InvalidOperationException("Ring device was not found on this account.");
        }

        // Logs the device name and control name only, never Ring ids or URLs.
        private void Audit(string deviceName, string control)
        {
            _logger.LogInformation("Ring device control {Control} applied to {DeviceName}", control, deviceName);
        }
    }
}
