namespace VideoForensics.Ui.Shared.Services.Devices;

/// <summary>Readable names for provider device types, and which types are cameras (only cameras have snapshots and time lapses).</summary>
public static class DeviceTypeNames
{
    /// <summary>Human-readable name for a stored device type. Unknown types are shown with their words capitalised.</summary>
    public static string Display(string? type) => type?.ToLowerInvariant() switch
    {
        "doorbot" or "authorized_doorbot" => "Doorbell",
        "stickup_cam" => "Stick Up Cam",
        "chime" => "Chime",
        "base_station" => "Base Station",
        "range_extender" => "Range Extender",
        null or "" => "Unknown",
        _ => string.Join(" ", type.Split('_', StringSplitOptions.RemoveEmptyEntries).Select(w => char.ToUpperInvariant(w[0]) + w[1..]))
    };

    /// <summary>True for a camera type. Snapshots and time lapses exist only for cameras.</summary>
    public static bool IsCamera(string? type) => type is "doorbot" or "authorized_doorbot" or "stickup_cam";
}
