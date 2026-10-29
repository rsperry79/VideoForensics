using VideoForensics.Client.Ui.Shared.Contracts;
using VideoForensics.Data.Common.Contracts;

namespace VideoForensics.Client.Ui.Shared.Services.Review;

/// <summary>
/// Reads one page of the review events list through the repository, which filters and sorts in SQL.
/// </summary>
public sealed class ReviewEventLoader : IReviewEventLoader
{
    /// <summary>Most devices one query names. The repository rejects more; the page never asks for more than this.</summary>
    public const int MaxDevices = 64;

    private readonly IEventRepository _events;

    /// <summary>Creates the loader over the event repository.</summary>
    public ReviewEventLoader(IEventRepository events)
    {
        _events = events;
    }

    /// <inheritdoc />
    public Task<EventPage> LoadAsync(IReadOnlyList<Guid> deviceIds, DateTime fromUtc, DateTime toUtc, int skip, int take, CancellationToken ct)
    {
        // Cameras are capped at the state's limit, so this cut is defensive only.
        List<Guid> devices = deviceIds.Distinct().Take(MaxDevices).ToList();
        return _events.ListPageByDevicesAndDateRangeAsync(
            devices, fromUtc, toUtc, ReviewEventFilters.ExcludedEventTypes, skip, take, ct);
    }
}
