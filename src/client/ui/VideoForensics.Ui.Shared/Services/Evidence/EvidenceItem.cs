using VideoForensics.Data.Common.Entities;
using VideoForensics.Ui.Shared.Services;

namespace VideoForensics.Ui.Shared.Services.Evidence;

/// <summary>
/// Enumeration of evidence item kinds.
/// </summary>
public enum EvidenceKind
{
    /// <summary>An event detected by a device.</summary>
    Event,

    /// <summary>A snapshot image (media-only).</summary>
    Snapshot,

    /// <summary>A video file (media-only).</summary>
    Video,

    /// <summary>Other file types (media-only).</summary>
    File
}

/// <summary>
/// Represents a unified evidence item: either an event (with optional media) or media-only (snapshot/video/file).
/// </summary>
public sealed class EvidenceItem
{
    /// <summary>
    /// Stable key for identity: "event:{eventId}" or "media:{mediaId}".
    /// </summary>
    public required string Key { get; init; }

    /// <summary>
    /// The kind of evidence item (Event, Snapshot, Video, File).
    /// </summary>
    public required EvidenceKind Kind { get; init; }

    /// <summary>
    /// The time when this evidence occurred (event.OccurredAtUtc or media.RecordedAtUtc).
    /// </summary>
    public required DateTime OccurredAtUtc { get; init; }

    /// <summary>
    /// The device ID this evidence is associated with.
    /// </summary>
    public required Guid DeviceId { get; init; }

    /// <summary>
    /// The user-friendly name of the device.
    /// </summary>
    public required string DeviceName { get; init; }

    /// <summary>
    /// The event, if this is an event-based item (null for media-only).
    /// </summary>
    public Event? Event { get; init; }

    /// <summary>
    /// The media item, if one is attached (optional even for events, null for pure events).
    /// </summary>
    public MediaItem? Media { get; init; }

    /// <summary>
    /// Human-readable label for the item: event type for events, or "Snapshot"/"Video"/"File" for media.
    /// </summary>
    public string Label =>
        Kind switch
        {
            EvidenceKind.Event => Event?.EventType ?? "Event",
            EvidenceKind.Snapshot => "Snapshot",
            EvidenceKind.Video => "Video",
            EvidenceKind.File => "File",
            _ => "Unknown"
        };

    /// <summary>
    /// True if this item has viewable media (image or video).
    /// </summary>
    public bool HasViewableMedia =>
        Media is not null && (MediaFormatHelper.IsImage(Media.MediaFormat) || MediaFormatHelper.IsVideo(Media.MediaFormat));
}
