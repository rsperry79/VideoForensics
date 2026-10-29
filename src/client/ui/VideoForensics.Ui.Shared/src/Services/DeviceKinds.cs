namespace VideoForensics.Client.Ui.Shared.Services;

/// <summary>Classifies stored device types, which are the provider's own kind strings (for example "doorbell", "stickup_cam", "chime").</summary>
public static class DeviceKinds
{
    // Kinds that are cameras without "cam" in the name. A doorbell and a floodlight both record video.
    private static readonly string[] CameraPrefixes =
    [
        "doorbell", "doorbot", "authorized_doorb", "stickup", "floodlight", "spotlight", "cocoa", "lpd_", "jbox",
    ];

    /// <summary>
    /// True when <paramref name="type"/> is a device the viewers can play video from. An unknown or missing type is not
    /// a camera: a chime or bridge in a camera list is worse than a missing camera, which the device page still shows.
    /// </summary>
    public static bool IsCamera(string? type)
    {
        if (string.IsNullOrWhiteSpace(type))
        {
            return false;
        }

        string kind = type.Trim().ToLowerInvariant();
        return kind.Contains("cam", StringComparison.Ordinal)
            || CameraPrefixes.Any(prefix => kind.StartsWith(prefix, StringComparison.Ordinal));
    }
}
