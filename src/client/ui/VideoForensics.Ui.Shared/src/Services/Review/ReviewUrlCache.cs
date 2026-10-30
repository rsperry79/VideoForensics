using VideoForensics.Client.Common.Contracts;
using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Client.Ui.Shared.Services.Review;

/// <summary>
/// Holds the ticketed URLs the review timeline plays, so the player and the strip read them synchronously while rendering.
/// </summary>
/// <remarks>
/// Each URL carries a short-lived ticket, so an entry older than <see cref="Lifetime"/> is never returned; the caller calls
/// <see cref="EnsureAsync"/> again first. The preview URL swaps the route segment of the content URL. The preview route
/// validates the same ticket against the same media id (MediaApiEndpoints.GetPreviewAsync), so no second ticket is needed.
/// </remarks>
public sealed class ReviewUrlCache
{
    /// <summary>Most ids one ticket request names. Matches the server's MediaContentRoutes.MaxTicketsPerRequest.</summary>
    public const int BatchSize = 200;

    /// <summary>How long a ticketed URL is returned before it is requested again.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);

    private const string ContentSegment = "/content?";
    private const string PreviewSegment = "/preview?";

    private readonly IMediaContentUrlProvider _urls;
    private readonly TimeProvider _time;
    private readonly Dictionary<Guid, Entry> _entries = new();

    /// <summary>Creates the cache over the URL provider and the clock that ages its entries.</summary>
    public ReviewUrlCache(IMediaContentUrlProvider urls, TimeProvider time)
    {
        _urls = urls;
        _time = time;
    }

    /// <summary>Returns the video URL for an item, or null when it is missing or its ticket is older than <see cref="Lifetime"/>.</summary>
    public string? ContentUrl(Guid mediaItemId)
    {
        return Fresh(mediaItemId)?.ContentUrl;
    }

    /// <summary>Returns the preview URL for an item, or null when the item has no preview or its ticket is stale.</summary>
    public string? PreviewUrl(Guid mediaItemId)
    {
        return Fresh(mediaItemId)?.PreviewUrl;
    }

    /// <summary>
    /// Requests URLs for the items that are missing, stale, or lack a preview URL they now need, in batches of <see cref="BatchSize"/>.
    /// Fresh entries are not requested again.
    /// </summary>
    public async Task EnsureAsync(IEnumerable<MediaItem> items, CancellationToken ct)
    {
        DateTimeOffset now = _time.GetUtcNow();
        var wanted = new Dictionary<Guid, bool>();
        foreach (MediaItem item in items)
        {
            bool hasPreview = !string.IsNullOrEmpty(item.PreviewPath);
            if (NeedsRequest(item.Id, hasPreview, now))
            {
                wanted[item.Id] = hasPreview;
            }
        }

        List<Guid> ids = wanted.Keys.ToList();
        for (int start = 0; start < ids.Count; start += BatchSize)
        {
            List<Guid> batch = ids.GetRange(start, Math.Min(BatchSize, ids.Count - start));
            IReadOnlyDictionary<Guid, string> urls = await _urls.GetContentUrlsAsync(batch, ct);
            DateTimeOffset fetchedAt = _time.GetUtcNow();

            foreach (Guid id in batch)
            {
                if (urls.TryGetValue(id, out string? url) && !string.IsNullOrEmpty(url))
                {
                    string? preview = wanted[id] ? PreviewFor(url) : null;
                    _entries[id] = new Entry(url, preview, fetchedAt);
                }
                else
                {
                    // The provider left the id out, so there is no ticket to play with.
                    _ = _entries.Remove(id);
                }
            }
        }
    }

    private Entry? Fresh(Guid id)
    {
        return _entries.TryGetValue(id, out Entry? entry) && _time.GetUtcNow() - entry.FetchedAt <= Lifetime
            ? entry
            : null;
    }

    private bool NeedsRequest(Guid id, bool hasPreview, DateTimeOffset now)
    {
        if (!_entries.TryGetValue(id, out Entry? entry))
        {
            return true;
        }

        return now - entry.FetchedAt > Lifetime || (hasPreview && entry.PreviewUrl is null);
    }

    /// <summary>Swaps the content route for the preview route. Returns null when the URL is not a content URL.</summary>
    private static string? PreviewFor(string contentUrl)
    {
        string preview = contentUrl.Replace(ContentSegment, PreviewSegment, StringComparison.Ordinal);
        return preview == contentUrl ? null : preview;
    }

    private sealed record Entry(string ContentUrl, string? PreviewUrl, DateTimeOffset FetchedAt);
}
