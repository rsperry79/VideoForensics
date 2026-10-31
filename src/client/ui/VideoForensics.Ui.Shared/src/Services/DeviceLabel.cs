namespace VideoForensics.Client.Ui.Shared.Services;

/// <summary>The label shown for a device. Duplicate names are common, so the label always includes the full provider ID.</summary>
public static class DeviceLabel
{
    /// <summary>Returns "name (ID full-id)", or "Device full-id" when the name is blank.</summary>
    public static string For(string name, string providerDeviceId)
    {
        return string.IsNullOrWhiteSpace(name) ? $"Device {providerDeviceId}" : $"{name} (ID {providerDeviceId})";
    }
}
