using VideoForensics.Data.Common.Entities;
using VideoForensics.Client.Ui.Shared.Services.Inspector;
using VideoForensics.Client.Ui.Shared.Services.Time;

namespace VideoForensics.Client.Ui.Shared.Services.Evidence;

/// <summary>
/// Maps EvidenceItem to InspectorModel for display in the inspector panel.
/// </summary>
public static class EvidenceInspectorMapper
{
    /// <param name="zone">The signed-in user's zone for the times shown. Null uses UTC, the zone before the browser's is known.</param>
    public static InspectorModel ToInspector(
        EvidenceItem item,
        IReadOnlyDictionary<Guid, LegalHold> holds,
        IReadOnlyList<IntegrityRecord> integrity,
        UserTimeZone? zone = null)
    {
        UserTimeZone clock = zone ?? UserTimeZone.Unavailable;
        string title = item.Kind switch
        {
            EvidenceKind.Event => $"{item.Label} · {clock.Format(item.OccurredAtUtc, Clock)}",
            _ => $"{item.Label} · {item.DeviceName} · {clock.Format(item.OccurredAtUtc, Clock)}"
        };

        // Fields: include event and media
        var fields = new
        {
            item.Event,
            MediaItem = item.Media
        };

        // RawJson: prefer event metadata, fall back to media metadata
        string? rawJson = item.Event?.MetadataJson ?? item.Media?.MetadataJson;

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
                clock.Format(item.Event.DiscoveredAtUtc, Clock)));

            if (item.Event.DownloadedAtUtc.HasValue)
            {
                provenance.Add(new KeyValuePair<string, string>(
                    "DownloadedAtUtc",
                    clock.Format(item.Event.DownloadedAtUtc.Value, Clock)));
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
                clock.Format(item.Media.RecordedAtUtc, Clock)));

            provenance.Add(new KeyValuePair<string, string>(
                "DownloadedAtUtc",
                clock.Format(item.Media.DownloadedAtUtc, Clock)));

            if (!string.IsNullOrWhiteSpace(item.Media.Sha256Hash))
            {
                provenance.Add(new KeyValuePair<string, string>("Sha256Hash", item.Media.Sha256Hash));
            }

            provenance.Add(new KeyValuePair<string, string>(
                "IntegrityVerified",
                item.Media.IntegrityVerified.ToString()));

            // Computed status (from the freshest IntegrityRecord for this media item), same as the
            // Events grid's "Integrity" column used to show - distinct from the raw
            // IntegrityVerified/LastVerifiedAtUtc entity fields above.
            (string? integrityStatusText, string _) = EvidenceActions.DetermineIntegrityStatus(item.Media.Id, integrity);
            provenance.Add(new KeyValuePair<string, string>("IntegrityStatus", integrityStatusText));

            if (item.Media.LastVerifiedAtUtc.HasValue)
            {
                provenance.Add(new KeyValuePair<string, string>(
                    "LastVerifiedAtUtc",
                    clock.Format(item.Media.LastVerifiedAtUtc.Value, Clock)));
            }

            if (!string.IsNullOrWhiteSpace(item.Media.ApiSourceHash))
            {
                provenance.Add(new KeyValuePair<string, string>("ApiSourceHash", item.Media.ApiSourceHash));
            }
        }

        // Legal hold status
        string holdStatus = item.Media is not null && holds.TryGetValue(item.Media.Id, out _)
            ? "On Hold"
            : "No Hold";
        provenance.Add(new KeyValuePair<string, string>("LegalHoldStatus", holdStatus));

        // Determine pin target: event if present, otherwise media
        PinTarget? pinTarget = null;
        if (item.Event is not null)
        {
            pinTarget = new PinTarget(CaseItemKind.Event, item.Event.Id);
        }
        else if (item.Media is not null)
        {
            pinTarget = new PinTarget(CaseItemKind.Media, item.Media.Id);
        }

        // Evidence actions: legal hold place/release, device integrity verification, export - all
        // gated on this item having a linked, downloaded media item (see EvidenceActions).
        Guid? activeHoldId = item.Media is not null && holds.TryGetValue(item.Media.Id, out LegalHold? activeHold)
            ? activeHold.Id
            : null;
        var actions = new EvidenceActionState(
            MediaItemId: item.Media?.Id,
            DeviceId: item.DeviceId,
            ActiveHoldId: activeHoldId);

        return new InspectorModel(
            Title: title,
            Fields: fields,
            RawJson: rawJson,
            Related: [],
            Provenance: provenance,
            Pin: pinTarget,
            Actions: actions);
    }

    /// <summary>Date and time to the second, with no zone suffix: the zone is the user's, so the label does not claim UTC.</summary>
    private const string Clock = "yyyy-MM-dd HH:mm:ss";
}
