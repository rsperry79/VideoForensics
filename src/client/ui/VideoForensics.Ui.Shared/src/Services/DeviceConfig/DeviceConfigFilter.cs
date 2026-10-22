using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Ui.Shared.Services.DeviceConfig;

/// <summary>
/// Provider-type filtering for the device configuration page. Devices with no linked provider account have a
/// null <see cref="Device.ProviderName"/>; they appear only when no provider filter is set.
/// </summary>
public static class DeviceConfigFilter
{
    /// <summary>The distinct linked provider names, sorted, for the provider filter's options.</summary>
    public static IReadOnlyList<string> ProviderOptions(IEnumerable<Device> devices) =>
        devices
            .Select(d => d.ProviderName)
            .Where(n => !string.IsNullOrEmpty(n))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(n => n, StringComparer.Ordinal)
            .Select(n => n!)
            .ToList();

    /// <summary>Devices linked to <paramref name="providerName"/>, or every device when it is null or empty.</summary>
    public static IReadOnlyList<Device> Filter(IEnumerable<Device> devices, string? providerName) =>
        string.IsNullOrEmpty(providerName)
            ? devices.ToList()
            : devices.Where(d => d.ProviderName == providerName).ToList();
}
