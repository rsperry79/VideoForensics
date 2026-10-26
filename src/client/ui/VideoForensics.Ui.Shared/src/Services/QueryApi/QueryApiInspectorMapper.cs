using VideoForensics.Core.Providers.Common.Contracts;
using VideoForensics.Client.Ui.Shared.Services.Inspector;
using VideoForensics.Client.Ui.Shared.Services.Time;

namespace VideoForensics.Client.Ui.Shared.Services.QueryApi;

/// <summary>Formats the times the QueryApi inspectors show in the user's zone. Null uses UTC, the zone before the browser's is known.</summary>
internal static class InspectorClock
{
    internal const string Format = "yyyy-MM-dd HH:mm:ss";

    internal static string Show(DateTime value, UserTimeZone? zone) => (zone ?? UserTimeZone.Unavailable).Format(value, Format);
}

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
    public static InspectorModel ToInspector(Location location, DateTime retrievedAt, UserTimeZone? zone = null)
    {
        string title = $"Location · {location.Name}";

        var provenance = new List<KeyValuePair<string, string>>
        {
            new("Source", "IDeviceDiscoveryService.GetLocationsAsync"),
            new("RetrievedAtUtc", InspectorClock.Show(retrievedAt, zone))
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
    public static InspectorModel ToInspector(Device device, string locationId, DateTime retrievedAt, UserTimeZone? zone = null)
    {
        string title = $"Device · {device.Name} ({device.Type})";

        var provenance = new List<KeyValuePair<string, string>>
        {
            new("LocationId", locationId),
            new("Source", "IDeviceDiscoveryService.GetDevicesAsync"),
            new("RetrievedAtUtc", InspectorClock.Show(retrievedAt, zone))
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
        DateTime retrievedAt,
        UserTimeZone? zone = null)
    {
        string title = $"Event · {@event.EventType} · {InspectorClock.Show(@event.Timestamp, zone)}";

        var provenance = new List<KeyValuePair<string, string>>
        {
            new("DeviceId", deviceId),
            new("DateRangeFromUtc", InspectorClock.Show(fromUtc, zone)),
            new("DateRangeToUtc", InspectorClock.Show(toUtc, zone)),
            new("Source", "IEventAndConfigService.GetEventsAsync"),
            new("RetrievedAtUtc", InspectorClock.Show(retrievedAt, zone))
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
    public static InspectorModel ToInspector(DeviceConfig config, string deviceId, DateTime retrievedAt, UserTimeZone? zone = null)
    {
        string title = $"Device Config · {deviceId}";

        var provenance = new List<KeyValuePair<string, string>>
        {
            new("DeviceId", deviceId),
            new("Source", "IEventAndConfigService.GetDeviceConfigAsync"),
            new("RetrievedAtUtc", InspectorClock.Show(retrievedAt, zone))
        };

        return new InspectorModel(
            Title: title,
            Fields: config,
            RawJson: null,
            Related: [],
            Provenance: provenance);
    }
}
