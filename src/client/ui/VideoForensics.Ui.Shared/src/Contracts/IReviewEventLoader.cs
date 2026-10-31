using VideoForensics.Data.Common.Contracts;

namespace VideoForensics.Client.Ui.Shared.Contracts;

/// <summary>Loads one page of provider events for a set of cameras over a UTC range, newest first.</summary>
public interface IReviewEventLoader
{
    /// <summary>
    /// Returns one page of the events recorded by <paramref name="deviceIds"/> in the range, newest first, without the
    /// excluded event types.
    /// </summary>
    /// <param name="deviceIds">Cameras to query. Duplicates are ignored; only the first 64 are used.</param>
    /// <param name="fromUtc">Start of the range (UTC).</param>
    /// <param name="toUtc">End of the range (UTC).</param>
    /// <param name="eventTypes">When set, only events of exactly these types; null for every type. An empty list matches nothing.</param>
    /// <param name="skip">Rows to skip from the newest.</param>
    /// <param name="take">Rows to return (1 to 500).</param>
    /// <param name="ct">Cancellation token.</param>
    Task<EventPage> LoadAsync(IReadOnlyList<Guid> deviceIds, DateTime fromUtc, DateTime toUtc, IReadOnlyCollection<string>? eventTypes, int skip, int take, CancellationToken ct);
}
