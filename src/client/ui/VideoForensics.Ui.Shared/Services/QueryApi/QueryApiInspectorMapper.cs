using VideoForensics.Providers.Common.Contracts;
using VideoForensics.Ui.Shared.Services.Inspector;

namespace VideoForensics.Ui.Shared.Services.QueryApi;

/// <summary>
/// Helpers for drill-down actions (row selection filling input fields).
/// </summary>
public static class QueryApiDrillDownHelpers
{
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
        var title = $"Location · {location.Name}";

        var provenance = new List<KeyValuePair<string, string>>
        {
            new KeyValuePair<string, string>("Source", "IDeviceDiscoveryService.GetLocationsAsync"),
            new KeyValuePair<string, string>("RetrievedAtUtc", retrievedAt.ToString("u"))
        };

        return new InspectorModel(
            Title: title,
            Fields: location,
            RawJson: null,
            Related: new List<InspectorLink>(),
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
        var title = $"Device · {device.Name} ({device.Type})";

        var provenance = new List<KeyValuePair<string, string>>
        {
            new KeyValuePair<string, string>("LocationId", locationId),
            new KeyValuePair<string, string>("Source", "IDeviceDiscoveryService.GetDevicesAsync"),
            new KeyValuePair<string, string>("RetrievedAtUtc", retrievedAt.ToString("u"))
        };

        return new InspectorModel(
            Title: title,
            Fields: device,
            RawJson: null,
            Related: new List<InspectorLink>(),
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
        var title = $"Event · {@event.EventType} · {@event.Timestamp:u}";

        var provenance = new List<KeyValuePair<string, string>>
        {
            new KeyValuePair<string, string>("DeviceId", deviceId),
            new KeyValuePair<string, string>("DateRangeFromUtc", fromUtc.ToString("u")),
            new KeyValuePair<string, string>("DateRangeToUtc", toUtc.ToString("u")),
            new KeyValuePair<string, string>("Source", "IEventAndConfigService.GetEventsAsync"),
            new KeyValuePair<string, string>("RetrievedAtUtc", retrievedAt.ToString("u"))
        };

        return new InspectorModel(
            Title: title,
            Fields: @event,
            RawJson: null,
            Related: new List<InspectorLink>(),
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
        var title = $"Device Config · {deviceId}";

        var provenance = new List<KeyValuePair<string, string>>
        {
            new KeyValuePair<string, string>("DeviceId", deviceId),
            new KeyValuePair<string, string>("Source", "IEventAndConfigService.GetDeviceConfigAsync"),
            new KeyValuePair<string, string>("RetrievedAtUtc", retrievedAt.ToString("u"))
        };

        return new InspectorModel(
            Title: title,
            Fields: config,
            RawJson: null,
            Related: new List<InspectorLink>(),
            Provenance: provenance);
    }
}
