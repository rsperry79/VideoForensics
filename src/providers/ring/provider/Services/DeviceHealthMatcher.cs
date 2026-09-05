using VideoForensics.Providers.Ring.Entities;

namespace VideoForensics.Providers.Ring.Services
{
    /// <summary>Matches a provider device id against the health telemetry embedded in a GET /clients_api/ring_devices response.</summary>
    public static class DeviceHealthMatcher
    {
        /// <summary>Finds the health telemetry for a device, checking doorbots, stickup cams, and authorized doorbots in turn.</summary>
        public static Entities.DeviceHealth? FindDeviceHealth(Entities.Devices? devices, string providerDeviceId)
        {
            if (devices == null)
            {
                return null;
            }

            Doorbot? doorbot = devices.Doorbots?.FirstOrDefault(d => d.Id.ToString() == providerDeviceId);
            if (doorbot?.Health != null)
            {
                return doorbot.Health;
            }

            StickupCam? stickupCam = devices.StickupCams?.FirstOrDefault(d => d.Id.HasValue && d.Id.Value.ToString() == providerDeviceId);
            if (stickupCam?.Health != null)
            {
                return stickupCam.Health;
            }

            Doorbot? authorizedDoorbot = devices.AuthorizedDoorbots?.FirstOrDefault(d => d.Id.ToString() == providerDeviceId);
            return authorizedDoorbot?.Health;
        }
    }
}
