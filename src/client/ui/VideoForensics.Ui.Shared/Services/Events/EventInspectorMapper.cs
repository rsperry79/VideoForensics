using VideoForensics.Data.Common.Entities;
using VideoForensics.Ui.Shared.Services.Inspector;

namespace VideoForensics.Ui.Shared.Services.Events;

/// <summary>
/// Maps EventRow to InspectorModel for display in the inspector panel.
/// </summary>
public static class EventInspectorMapper
{
    public static InspectorModel ToInspector(
        EventRow row,
        IReadOnlyDictionary<Guid, LegalHold> holds,
        IReadOnlyList<IntegrityRecord> integrity)
    {
        var title = $"{row.EventType} · {row.OccurredAtUtc:u}";

        // Fields: combine event and media item properties
        var fields = new
        {
            Event = row.Event,
            MediaItem = row.MediaItem
        };

        // RawJson: prefer event metadata, fall back to media metadata
        var rawJson = row.Event.MetadataJson ?? row.MediaItem?.MetadataJson;

        // Provenance: event context, media info, integrity, legal hold status
        var provenance = new List<KeyValuePair<string, string>>();

        // Event provenance
        if (!string.IsNullOrWhiteSpace(row.ProviderEventId))
        {
            provenance.Add(new KeyValuePair<string, string>("ProviderEventId", row.ProviderEventId));
        }

        provenance.Add(new KeyValuePair<string, string>(
            "DiscoveredAtUtc",
            row.DiscoveredAtUtc.ToString("u")));

        if (row.DownloadedAtUtc.HasValue)
        {
            provenance.Add(new KeyValuePair<string, string>(
                "DownloadedAtUtc",
                row.DownloadedAtUtc.Value.ToString("u")));
        }

        // Media provenance
        if (row.MediaItem is not null)
        {
            if (!string.IsNullOrWhiteSpace(row.MediaItem.Sha256Hash))
            {
                provenance.Add(new KeyValuePair<string, string>(
                    "Sha256Hash",
                    row.MediaItem.Sha256Hash));
            }

            provenance.Add(new KeyValuePair<string, string>(
                "IntegrityVerified",
                row.MediaItem.IntegrityVerified.ToString()));

            if (row.MediaItem.LastVerifiedAtUtc.HasValue)
            {
                provenance.Add(new KeyValuePair<string, string>(
                    "LastVerifiedAtUtc",
                    row.MediaItem.LastVerifiedAtUtc.Value.ToString("u")));
            }

            if (!string.IsNullOrWhiteSpace(row.MediaItem.ApiSourceHash))
            {
                provenance.Add(new KeyValuePair<string, string>(
                    "ApiSourceHash",
                    row.MediaItem.ApiSourceHash));
            }
        }

        // Legal hold status
        var holdStatus = row.MediaItem is not null && holds.TryGetValue(row.MediaItem.Id, out _)
            ? "On Hold"
            : "No Hold";
        provenance.Add(new KeyValuePair<string, string>("LegalHoldStatus", holdStatus));

        return new InspectorModel(
            Title: title,
            Fields: fields,
            RawJson: rawJson,
            Related: new List<InspectorLink>(),
            Provenance: provenance);
    }
}
