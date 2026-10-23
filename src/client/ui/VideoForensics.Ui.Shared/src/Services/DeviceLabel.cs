namespace VideoForensics.Client.Ui.Shared.Services;

/// <summary>The label shown for a device. Duplicate names are common, so the label always includes part of the provider ID.</summary>
public static class DeviceLabel
{
    /// <summary>Returns "name (ID ...last4)", or "Device ...last4" when the name is blank.</summary>
    public static string For(string name, string providerDeviceId)
    {
        string suffix = providerDeviceId.Length <= 4 ? providerDeviceId : providerDeviceId[^4..];
        return string.IsNullOrWhiteSpace(name) ? $"Device ...{suffix}" : $"{name} (ID ...{suffix})";
    }
}
