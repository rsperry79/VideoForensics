using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Client.Ui.Shared.Contracts;

/// <summary>Loads the media items the review timeline plays, for a set of cameras over a UTC range.</summary>
public interface IReviewClipLoader
{
    /// <summary>
    /// Returns the media items recorded by <paramref name="deviceIds"/> in the range, oldest first, up to the loader's safety cap.
    /// </summary>
    /// <param name="deviceIds">Cameras to load. Duplicates are ignored.</param>
    /// <param name="fromUtc">Start of the range (UTC).</param>
    /// <param name="toUtc">End of the range (UTC).</param>
    /// <param name="ct">Cancellation token.</param>
    Task<ReviewClipSet> LoadAsync(IReadOnlyList<Guid> deviceIds, DateTime fromUtc, DateTime toUtc, CancellationToken ct);
}

/// <summary>The media items one review load returned.</summary>
/// <param name="Items">Items, oldest first within each camera.</param>
/// <param name="Truncated">True when the safety cap stopped the load before every item was read.</param>
public sealed record ReviewClipSet(IReadOnlyList<MediaItem> Items, bool Truncated);
