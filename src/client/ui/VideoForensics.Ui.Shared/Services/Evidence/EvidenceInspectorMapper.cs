using VideoForensics.Data.Common.Entities;
using VideoForensics.Ui.Shared.Services.Inspector;

namespace VideoForensics.Ui.Shared.Services.Evidence;

/// <summary>
/// Maps EvidenceItem to InspectorModel for display in the inspector panel.
/// </summary>
public static class EvidenceInspectorMapper
{
    public static InspectorModel ToInspector(
        EvidenceItem item,
        IReadOnlyDictionary<Guid, LegalHold> holds,
        IReadOnlyList<IntegrityRecord> integrity)
    {
        var title = item.Kind switch
        {
            EvidenceKind.Event => $"{item.Label} · {item.OccurredAtUtc:u}",
            _ => $"{item.Label} · {item.DeviceName} · {item.OccurredAtUtc:u}"
        };

        // Fields: include event and media
        var fields = new
        {
            Event = item.Event,
            MediaItem = item.Media
        };

        // RawJson: prefer event metadata, fall back to media metadata
        var rawJson = item.Event?.MetadataJson ?? item.Media?.MetadataJson;

        // Provenance: build from event and media properties
        var provenance = new List<KeyValuePair<string, string>>();

        if (item.Event is not null)
        {
            if (!string.IsNullOrWhiteSpace(item.Event.ProviderEventId))
            {
                provenance.Add(new KeyValuePair<string, string>("ProviderEventId", item.Event.ProviderEventId));
            }

            provenance.Add(new KeyValuePair<string, string>(
                "DiscoveredAtUtc",
                item.Event.DiscoveredAtUtc.ToString("u")));

            if (item.Event.DownloadedAtUtc.HasValue)
            {
                provenance.Add(new KeyValuePair<string, string>(
                    "DownloadedAtUtc",
                    item.Event.DownloadedAtUtc.Value.ToString("u")));
            }
        }

        // Media provenance
        if (item.Media is not null)
        {
            if (!string.IsNullOrWhiteSpace(item.Media.FileName))
            {
                provenance.Add(new KeyValuePair<string, string>("FileName", item.Media.FileName));
            }

            if (!string.IsNullOrWhiteSpace(item.Media.MediaFormat))
            {
                provenance.Add(new KeyValuePair<string, string>("MediaFormat", item.Media.MediaFormat));
            }

            if (!string.IsNullOrWhiteSpace(item.Media.Resolution))
            {
                provenance.Add(new KeyValuePair<string, string>("Resolution", item.Media.Resolution));
            }

            if (item.Media.FrameRate.HasValue)
            {
                provenance.Add(new KeyValuePair<string, string>("FrameRate", item.Media.FrameRate.Value.ToString()));
            }

            provenance.Add(new KeyValuePair<string, string>("FileSizeBytes", item.Media.FileSizeBytes.ToString()));

            provenance.Add(new KeyValuePair<string, string>(
                "RecordedAtUtc",
                item.Media.RecordedAtUtc.ToString("u")));

            provenance.Add(new KeyValuePair<string, string>(
                "DownloadedAtUtc",
                item.Media.DownloadedAtUtc.ToString("u")));

            if (!string.IsNullOrWhiteSpace(item.Media.Sha256Hash))
            {
                provenance.Add(new KeyValuePair<string, string>("Sha256Hash", item.Media.Sha256Hash));
            }

            provenance.Add(new KeyValuePair<string, string>(
                "IntegrityVerified",
                item.Media.IntegrityVerified.ToString()));

            if (item.Media.LastVerifiedAtUtc.HasValue)
            {
                provenance.Add(new KeyValuePair<string, string>(
                    "LastVerifiedAtUtc",
                    item.Media.LastVerifiedAtUtc.Value.ToString("u")));
            }

            if (!string.IsNullOrWhiteSpace(item.Media.ApiSourceHash))
            {
                provenance.Add(new KeyValuePair<string, string>("ApiSourceHash", item.Media.ApiSourceHash));
            }
        }

        // Legal hold status
        var holdStatus = item.Media is not null && holds.TryGetValue(item.Media.Id, out _)
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
