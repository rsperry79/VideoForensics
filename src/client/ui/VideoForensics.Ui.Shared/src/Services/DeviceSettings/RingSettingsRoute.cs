using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Client.Ui.Shared.Services.DeviceSettings;

/// <summary>Routing for the strongly typed Ring settings page, opened from the device configuration page.</summary>
public static class RingSettingsRoute
{
    /// <summary>True when the device is linked to a Ring account, so the Ring settings page applies.</summary>
    public static bool IsRingDevice(Device device)
    {
        return string.Equals(device.ProviderName, "Ring", StringComparison.Ordinal);
    }

    /// <summary>The Ring settings page address for one device.</summary>
    public static string Href(Guid deviceId)
    {
        return $"/devices/config/ring/{deviceId}";
    }
}
