using Microsoft.Extensions.Logging;

using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;

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
        private readonly IDeviceLightEventRepository _lightEvents;

        public RingDeviceControlService(ILogger logger, ISessionProvider sessionProvider, IDeviceRepository devices, ILocationRepository locations, IDeviceLightEventRepository lightEvents)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _sessionProvider = sessionProvider ?? throw new ArgumentNullException(nameof(sessionProvider));
            _devices = devices ?? throw new ArgumentNullException(nameof(devices));
            _locations = locations ?? throw new ArgumentNullException(nameof(locations));
            _lightEvents = lightEvents ?? throw new ArgumentNullException(nameof(lightEvents));
        }

        public async Task SetLightAsync(string providerDeviceId, bool on, CancellationToken ct)
        {
            ResolvedControlTarget target = await ResolveTargetAsync(providerDeviceId, ct);
            await target.Session.SetLight(target.DoorbotId, on, ct);
            if (on)
            {
                // The call returns no duration, so the command event carries none. Recorded only after Ring accepted it.
                await _lightEvents.AddCommandAsync(target.DeviceId, DateTime.UtcNow, null, ct);
            }

            Audit(target.Name, "Light");
        }

        public async Task SetSirenAsync(string providerDeviceId, bool on, int? durationSeconds, CancellationToken ct)
        {
            ResolvedControlTarget target = await ResolveTargetAsync(providerDeviceId, ct);
            await target.Session.SetSiren(target.DoorbotId, on, durationSeconds, ct);
            Audit(target.Name, "Siren");
        }

        public async Task SetVolumeAsync(string providerDeviceId, int volume, CancellationToken ct)
        {
            ResolvedControlTarget target = await ResolveTargetAsync(providerDeviceId, ct);
            await target.Session.SetVolume(target.DoorbotId, volume, target.Name, ct);
            Audit(target.Name, "Volume");
        }

        public async Task SetNightModeAsync(string providerDeviceId, bool enabled, CancellationToken ct)
        {
            ResolvedControlTarget target = await ResolveTargetAsync(providerDeviceId, ct);
            await target.Session.SetNightMode(target.DoorbotId, enabled, ct);
            Audit(target.Name, "NightMode");
        }

        public async Task SetMotionDetectionAsync(string providerDeviceId, bool enabled, CancellationToken ct)
        {
            ResolvedControlTarget target = await ResolveTargetAsync(providerDeviceId, ct);
            await target.Session.SetMotionDetection(target.DoorbotId, enabled, ct);
            Audit(target.Name, "MotionDetection");
        }

        public async Task SetDoNotDisturbAsync(string providerDeviceId, int seconds, CancellationToken ct)
        {
            ResolvedControlTarget target = await ResolveTargetAsync(providerDeviceId, ct);
            await target.Session.SetDoNotDisturb(target.DoorbotId, seconds, ct);
            Audit(target.Name, "DoNotDisturb");
        }

        private static long ParseId(string providerDeviceId)
        {
            return !long.TryParse(providerDeviceId, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out long id)
                ? throw new ArgumentException("Provider device id must be a numeric Ring id.", nameof(providerDeviceId))
                : id;
        }

        // Resolved once per control: the Ring SDK session for the device's single linked account, the numeric
        // Ring id, and the display name stored on the Device row. The control path never reads device state from Ring.
        private sealed record ResolvedControlTarget(Session Session, long DoorbotId, string Name, Guid DeviceId);

        // Resolves the device by provider id, then the Ring account linked to its location. Fails closed:
        // zero or several Ring accounts throw before any provider call, so a control cannot land on the wrong account.
        // The stored device name is required; a blank name throws before the session is fetched, and Ring is never
        // queried for it (only the config sync populates Device.Name).
        private async Task<ResolvedControlTarget> ResolveTargetAsync(string providerDeviceId, CancellationToken ct)
        {
            long id = ParseId(providerDeviceId);

            IReadOnlyList<DeviceEntity> rows = await _devices.ListAsync(ct);
            DeviceEntity device = rows.FirstOrDefault(d => string.Equals(d.ProviderDeviceId, providerDeviceId, StringComparison.Ordinal))
                ?? throw new KeyNotFoundException("Device was not found.");

            IReadOnlyList<ProviderAccount> linked = await _locations.ListProviderAccountsForLocationAsync(device.LocationId, ct);
            var ringAccounts = linked
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

            if (string.IsNullOrWhiteSpace(device.Name))
            {
                throw new InvalidOperationException("Device name is not stored yet; run a sync first");
            }

            Session session = _sessionProvider.GetSession(ringAccounts[0].Id)
                ?? throw new InvalidOperationException("Ring account is not signed in.");

            return new ResolvedControlTarget(session, id, device.Name, device.Id);
        }

        // Logs the device name and control name only, never Ring ids or URLs.
        private void Audit(string deviceName, string control)
        {
            _logger.LogInformation("Ring device control {Control} applied to {DeviceName}", control, deviceName);
        }
    }
}
