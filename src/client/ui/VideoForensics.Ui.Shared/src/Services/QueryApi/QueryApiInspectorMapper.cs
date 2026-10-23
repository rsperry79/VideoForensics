using VideoForensics.Core.Providers.Common.Contracts;
using VideoForensics.Client.Ui.Shared.Services.Inspector;

namespace VideoForensics.Client.Ui.Shared.Services.QueryApi;

/// <summary>
/// A dropdown entry for the QueryApi filter panel: <see cref="Value"/> is the id used for queries, <see cref="Label"/> is shown to the user.
/// </summary>
public sealed record QueryApiSelectOption(string Value, string Label);

/// <summary>
/// Helpers for drill-down actions (row selection filling input fields).
/// </summary>
public static class QueryApiDrillDownHelpers
{
    /// <summary>
    /// Human-readable label for a Location: its Name, or its Id when Name is blank (IDs are opaque, so Name is preferred).
    /// </summary>
    public static string LocationDisplayLabel(Location? location)
    {
        return PreferName(location?.Name, location?.Id);
    }

    /// <summary>
    /// Human-readable label for a Device: its Name, or its Id when Name is blank.
    /// </summary>
    public static string DeviceDisplayLabel(Device? device)
    {
        return PreferName(device?.Name, device?.Id);
    }

    private static string PreferName(string? name, string? id)
    {
        return string.IsNullOrWhiteSpace(name) ? id ?? string.Empty : name;
    }

    /// <summary>
    /// Extracts the location ID for drill-down (filling _locationIdInput when a Location row is selected).
    /// </summary>
    public static string DrillDownLocationId(Location? location)
    {
        return location?.Id ?? string.Empty;
    }

    /// <summary>
    /// Extracts the device ID for drill-down (filling _eventsDeviceIdInput when a Device row is selected).
    /// </summary>
    public static string DrillDownDeviceId(Device? device)
    {
        return device?.Id ?? string.Empty;
    }
}

/// <summary>
/// Maps Location, Device, DeviceEvent, and DeviceConfig to InspectorModel for QueryApi page display.
/// </summary>
public static class LocationInspectorMapper
{
    public static InspectorModel ToInspector(Location location, DateTime retrievedAt)
    {
        string title = $"Location · {location.Name}";

        var provenance = new List<KeyValuePair<string, string>>
        {
            new("Source", "IDeviceDiscoveryService.GetLocationsAsync"),
            new("RetrievedAtUtc", retrievedAt.ToString("u"))
        };

        return new InspectorModel(
            Title: title,
            Fields: location,
            RawJson: null,
            Related: [],
            Provenance: provenance);
    }
}

/// <summary>
/// Maps Device to InspectorModel for QueryApi page display.
/// </summary>
public static class DeviceInspectorMapper
{
    public static InspectorModel ToInspector(Device device, string locationId, DateTime retrievedAt)
    {
        string title = $"Device · {device.Name} ({device.Type})";

        var provenance = new List<KeyValuePair<string, string>>
        {
            new("LocationId", locationId),
            new("Source", "IDeviceDiscoveryService.GetDevicesAsync"),
            new("RetrievedAtUtc", retrievedAt.ToString("u"))
        };

        return new InspectorModel(
            Title: title,
            Fields: device,
            RawJson: null,
            Related: [],
            Provenance: provenance);
    }
}

/// <summary>
/// Maps DeviceEvent to InspectorModel for QueryApi page display.
/// </summary>
public static class DeviceEventInspectorMapper
{
    public static InspectorModel ToInspector(
        DeviceEvent @event,
        string deviceId,
        DateTime fromUtc,
        DateTime toUtc,
        DateTime retrievedAt)
    {
        string title = $"Event · {@event.EventType} · {@event.Timestamp:u}";

        var provenance = new List<KeyValuePair<string, string>>
        {
            new("DeviceId", deviceId),
            new("DateRangeFromUtc", fromUtc.ToString("u")),
            new("DateRangeToUtc", toUtc.ToString("u")),
            new("Source", "IEventAndConfigService.GetEventsAsync"),
            new("RetrievedAtUtc", retrievedAt.ToString("u"))
        };

        return new InspectorModel(
            Title: title,
            Fields: @event,
            RawJson: null,
            Related: [],
            Provenance: provenance);
    }
}

/// <summary>
/// Maps DeviceConfig to InspectorModel for QueryApi page display.
/// </summary>
public static class DeviceConfigInspectorMapper
{
    public static InspectorModel ToInspector(DeviceConfig config, string deviceId, DateTime retrievedAt)
    {
        string title = $"Device Config · {deviceId}";

        var provenance = new List<KeyValuePair<string, string>>
        {
            new("DeviceId", deviceId),
            new("Source", "IEventAndConfigService.GetDeviceConfigAsync"),
            new("RetrievedAtUtc", retrievedAt.ToString("u"))
        };

        return new InspectorModel(
            Title: title,
            Fields: config,
            RawJson: null,
            Related: [],
            Provenance: provenance);
    }
}
