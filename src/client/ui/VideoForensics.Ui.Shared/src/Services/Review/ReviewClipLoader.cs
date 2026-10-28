using VideoForensics.Client.Ui.Shared.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Client.Ui.Shared.Services.Review;

/// <summary>
/// Reads the clips the review timeline plays by paging the repository query. The query filters and sorts in SQL;
/// this class decides how many pages to read and when to stop.
/// </summary>
public sealed class ReviewClipLoader : IReviewClipLoader
{
    /// <summary>Most items one page asks for. The repository accepts 1 to 500.</summary>
    public const int PageSize = 500;

    /// <summary>
    /// Safety cap on items read per load. A load that stops at the cap, with more items still waiting, returns
    /// <see cref="ReviewClipSet.Truncated"/> set, so the page can say the list is incomplete.
    /// </summary>
    public const int MaxItems = 5000;

    /// <summary>Most devices one page query names. A longer list is split across calls.</summary>
    public const int MaxDevicesPerCall = 64;

    private readonly IMediaItemRepository _mediaItems;

    /// <summary>Creates the loader over the media item repository.</summary>
    public ReviewClipLoader(IMediaItemRepository mediaItems)
    {
        _mediaItems = mediaItems;
    }

    /// <inheritdoc />
    public async Task<ReviewClipSet> LoadAsync(IReadOnlyList<Guid> deviceIds, DateTime fromUtc, DateTime toUtc, CancellationToken ct)
    {
        List<Guid> devices = deviceIds.Distinct().ToList();
        var items = new List<MediaItem>();

        // The cap is shared across device chunks, so one chunk can stop the whole load.
        for (int start = 0; start < devices.Count; start += MaxDevicesPerCall)
        {
            List<Guid> chunk = devices.GetRange(start, Math.Min(MaxDevicesPerCall, devices.Count - start));
            if (await ReadChunkAsync(chunk, fromUtc, toUtc, items, ct))
            {
                return new ReviewClipSet(items, true);
            }
        }

        return new ReviewClipSet(items, false);
    }

    /// <summary>
    /// Reads one device chunk into <paramref name="items"/>. Returns true when the cap was reached while the chunk still had items.
    /// </summary>
    private async Task<bool> ReadChunkAsync(
        IReadOnlyCollection<Guid> chunk, DateTime fromUtc, DateTime toUtc, List<MediaItem> items, CancellationToken ct)
    {
        int skip = 0;
        while (true)
        {
            int remaining = MaxItems - items.Count;
            if (remaining == 0)
            {
                // The cap is spent. Ask for one row to learn whether anything was left unread, without reading it.
                MediaItemPage probe = await _mediaItems.GetPageByDevicesAndDateRangeAsync(chunk, fromUtc, toUtc, 0, 1, ct);
                return probe.TotalCount > 0;
            }

            MediaItemPage page = await _mediaItems.GetPageByDevicesAndDateRangeAsync(
                chunk, fromUtc, toUtc, skip, Math.Min(PageSize, remaining), ct);
            items.AddRange(page.Items);
            skip += page.Items.Count;

            // An empty page or a page that reached the total means the chunk is finished.
            if (page.Items.Count == 0 || skip >= page.TotalCount)
            {
                return false;
            }
        }
    }
}
