using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Ui.Shared.Services.Events;

/// <summary>
/// Represents a single row in the Events grid, combining an event with its associated media item.
/// </summary>
public class EventRow
{
    public required Event Event { get; init; }
    public MediaItem? MediaItem { get; set; }
    public required Guid DeviceId { get; init; }

    public string EventType => Event.EventType;
    public DateTime OccurredAtUtc => Event.OccurredAtUtc;
    public DateTime DiscoveredAtUtc => Event.DiscoveredAtUtc;
    public DateTime? DownloadedAtUtc => Event.DownloadedAtUtc;
    public string ProviderEventId => Event.ProviderEventId;
}
