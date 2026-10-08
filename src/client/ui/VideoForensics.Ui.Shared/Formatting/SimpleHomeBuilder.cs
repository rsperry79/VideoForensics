using System.Globalization;
using VideoForensics.Api.Contracts;
using VideoForensics.Client.Common.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Ui.Shared.Services;

namespace VideoForensics.Ui.Shared.Formatting;

/// <summary>
/// Builds the plain-language Simple Mode home (timeline plus evidence list) from stored data.
/// Output strings never contain identifiers, hashes, file paths, provider names or signal metrics.
/// </summary>
public sealed class SimpleHomeBuilder
{
    /// <summary>Maximum number of timeline entries returned.</summary>
    public const int MaxTimelineEntries = 100;

    /// <summary>Maximum number of evidence items returned.</summary>
    public const int MaxEvidenceItems = 50;

    private const string FallbackDeviceName = "Your camera";

    private readonly TimeProvider _timeProvider;
    private readonly IMediaContentUrlProvider _urlProvider;
    private readonly TimeZoneInfo _timeZone;

    /// <param name="timeProvider">Clock used for the window and the Today/Yesterday headings.</param>
    /// <param name="urlProvider">Resolves browser-loadable addresses for evidence items.</param>
    /// <param name="timeZone">Zone used to show local times; defaults to the machine's local zone.</param>
    public SimpleHomeBuilder(TimeProvider timeProvider, IMediaContentUrlProvider urlProvider, TimeZoneInfo? timeZone = null)
    {
        _timeProvider = timeProvider;
        _urlProvider = urlProvider;
        _timeZone = timeZone ?? TimeZoneInfo.Local;
    }

    /// <summary>Builds the Simple Mode home model for the given look-back window.</summary>
    /// <param name="events">Event repository.</param>
    /// <param name="media">Media repository.</param>
    /// <param name="devices">Device repository.</param>
    /// <param name="jamming">Jamming incident repository; null when the host has none (remote client), in which case no blocked entries are added.</param>
    /// <param name="window">How far back from now to look (the Simple home uses 7 days).</param>
    /// <param name="ct">Cancellation token, forwarded to every repository call.</param>
    public async Task<SimpleHomeModel> BuildAsync(
        IEventRepository events,
        IMediaItemRepository media,
        IDeviceRepository devices,
        IJammingRepository? jamming,
        TimeSpan window,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
        var fromUtc = nowUtc - window;

        var deviceList = await devices.ListAsync(ct);
        var names = deviceList.ToDictionary(d => d.Id, d => string.IsNullOrWhiteSpace(d.Name) ? null : d.Name.Trim());

        var entries = new List<SimpleTimelineEntry>();
        var mediaItems = new List<(MediaItem Item, string? Name)>();

        foreach (var device in deviceList)
        {
            var name = names[device.Id];

            foreach (var evt in await events.ListByDeviceAndDateRangeAsync(device.Id, fromUtc, nowUtc, ct))
            {
                var when = AsUtc(evt.OccurredAtUtc);
                entries.Add(new SimpleTimelineEntry(when, TimeText(when), EventPlainLanguageFormatter.Describe(evt.ToDto(), name), SimpleEntryKind.Event));
            }

            foreach (var item in await media.GetByDeviceAndDateRangeAsync(device.Id, fromUtc, nowUtc, ct))
                mediaItems.Add((item, name));
        }

        // Blocked-activity data is optional: a missing or failing source must not hide the device events.
        var blockedUnavailable = jamming is null;
        if (jamming is not null)
        {
            try
            {
                foreach (var incident in await jamming.ListIncidentsAsync(null, fromUtc, nowUtc, ct))
                {
                    names.TryGetValue(incident.DeviceId, out var name);
                    var start = AsUtc(incident.StartUtc);
                    // Only the duration is passed on: signal strength and degradation are technical and must not reach victims.
                    var text = EventPlainLanguageFormatter.DescribeJammingIncident(name, start, AsUtc(incident.EndUtc));
                    entries.Add(new SimpleTimelineEntry(start, TimeText(start), text, SimpleEntryKind.Blocked));
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception)
            {
                blockedUnavailable = true;
            }
        }
        var ordered = entries.OrderByDescending(e => e.TimeUtc).ToList();
        var hasMore = ordered.Count > MaxTimelineEntries;
        var days = GroupByLocalDay(ordered.Take(MaxTimelineEntries), nowUtc);

        var evidence = await BuildEvidenceAsync(mediaItems, ct);

        return new SimpleHomeModel(days, evidence, hasMore, fromUtc, blockedUnavailable);
    }

    private async Task<IReadOnlyList<SimpleEvidenceItem>> BuildEvidenceAsync(List<(MediaItem Item, string? Name)> mediaItems, CancellationToken ct)
    {
        var top = mediaItems.OrderByDescending(m => m.Item.RecordedAtUtc).Take(MaxEvidenceItems).ToList();
        if (top.Count == 0)
            return Array.Empty<SimpleEvidenceItem>();

        var available = top.Where(m => !m.Item.IsPurged).Select(m => m.Item.Id).ToList();
        IReadOnlyDictionary<Guid, string> urls = available.Count == 0
            ? new Dictionary<Guid, string>()
            : await _urlProvider.GetContentUrlsAsync(available, ct);

        return top.Select(m =>
        {
            var when = AsUtc(m.Item.RecordedAtUtc);
            var timeText = TimeText(when);
            string? url = null;
            if (!m.Item.IsPurged && urls.TryGetValue(m.Item.Id, out var found))
                url = found;

            return new SimpleEvidenceItem(
                $"{CameraLabel(m.Name)} - {timeText}",
                MediaFormatHelper.IsImage(m.Item.MediaFormat) ? "Snapshot" : "Video",
                when,
                timeText,
                url,
                url is not null);
        }).ToList();
    }

    private IReadOnlyList<SimpleTimelineDay> GroupByLocalDay(IEnumerable<SimpleTimelineEntry> ordered, DateTime nowUtc)
    {
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(nowUtc, _timeZone));

        return ordered
            .GroupBy(e => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(e.TimeUtc, _timeZone)))
            .OrderByDescending(g => g.Key)
            .Select(g => new SimpleTimelineDay(g.Key, Heading(g.Key, today), g.ToList()))
            .ToList();
    }

    private static string Heading(DateOnly date, DateOnly today)
    {
        if (date == today) return "Today";
        if (date == today.AddDays(-1)) return "Yesterday";
        return date.ToString("dddd d MMMM", CultureInfo.InvariantCulture);
    }

    private string TimeText(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(utc, _timeZone).ToString("h:mm tt", CultureInfo.InvariantCulture);

    private static DateTime AsUtc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);

    // Evidence labels read as a noun phrase ("Front door camera"), unlike timeline text ("Your front door camera ...").
    private static string CameraLabel(string? name) =>
        string.IsNullOrWhiteSpace(name) ? FallbackDeviceName : char.ToUpperInvariant(name[0]) + name[1..];
}