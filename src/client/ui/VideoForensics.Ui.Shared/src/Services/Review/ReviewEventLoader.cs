using VideoForensics.Client.Ui.Shared.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Client.Ui.Shared.Services.Review;

/// <summary>
/// Reads one page of the review events list through the repository, which filters and sorts in SQL.
/// </summary>
public sealed class ReviewEventLoader : IReviewEventLoader
{
    /// <summary>Most devices one query names. The repository rejects more; the page never asks for more than this.</summary>
    public const int MaxDevices = 64;

    private readonly IEventRepository _events;
    private readonly ReviewCaseMode? _caseMode;

    /// <summary>
    /// Creates the loader over the event repository. When <paramref name="caseMode"/> is active, pages come from the
    /// case's pinned events in memory. Null means the case view is never active.
    /// </summary>
    public ReviewEventLoader(IEventRepository events, ReviewCaseMode? caseMode = null)
    {
        _events = events;
        _caseMode = caseMode;
    }

    /// <inheritdoc />
    public Task<EventPage> LoadAsync(IReadOnlyList<Guid> deviceIds, DateTime fromUtc, DateTime toUtc, string? eventType, int skip, int take, CancellationToken ct)
    {
        if (_caseMode is { Active: true })
        {
            return Task.FromResult(CaseEventPage(_caseMode, deviceIds, fromUtc, toUtc, eventType, skip, take));
        }

        // Cameras are capped at the state's limit, so this cut is defensive only.
        List<Guid> devices = deviceIds.Distinct().Take(MaxDevices).ToList();
        return _events.ListPageByDevicesAndDateRangeAsync(
            devices, fromUtc, toUtc, ReviewEventFilters.ExcludedEventTypes,
            eventType is null ? null : [eventType], skip, take, ct);
    }

    /// <summary>
    /// Filters the case's events to the requested devices and window, newest first, then cuts the page. The total is the
    /// full matching count, so the events tab's paging works the same as against the repository.
    /// </summary>
    private static EventPage CaseEventPage(ReviewCaseMode caseMode, IReadOnlyList<Guid> deviceIds, DateTime fromUtc, DateTime toUtc, string? eventType, int skip, int take)
    {
        HashSet<Guid> devices = deviceIds.ToHashSet();
        List<Event> matching = caseMode.Events
            .Where(evt => devices.Contains(evt.DeviceId) && evt.OccurredAtUtc >= fromUtc && evt.OccurredAtUtc <= toUtc && (eventType is null || evt.EventType == eventType))
            .OrderByDescending(evt => evt.OccurredAtUtc)
            .ToList();
        return new EventPage(matching.Skip(skip).Take(take).ToList(), matching.Count);
    }
}
