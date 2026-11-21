using VideoForensics.Core.Api.Contracts.Repositories;
using VideoForensics.Core.Api.Contracts;


namespace VideoForensics.Client.Ui.Shared.Components.Review;

/// <summary>Picks the flag a review page moves to once the viewer has reviewed the one on screen.</summary>
public static class FlagAdvance
{
    /// <summary>
    /// Finds the next flag that is still Unreviewed, within the page's filter. It looks Next (the older flag) first, then
    /// Previous (the newer flag), so a review of the last flag in the list still lands on remaining work.
    /// </summary>
    /// <remarks>
    /// The reviewed flag no longer matches Unreviewed, so the lookup starts from its position in the list, not from the
    /// filter. Both lookups use the same narrowed filter, so the neighbour is always an Unreviewed flag.
    /// </remarks>
    /// <param name="repository">The flag repository to query.</param>
    /// <param name="filter">The page's filter. Its status list is replaced with Unreviewed.</param>
    /// <param name="fromFlagId">The flag the viewer just reviewed.</param>
    /// <param name="ct">Cancels the lookups.</param>
    /// <returns>The nearest Unreviewed flag on either side, or null when none is left.</returns>
    public static async Task<MediaFlagDto?> FindNextUnreviewedAsync(IMediaFlagRepository repository, MediaFlagFilterDto filter, Guid fromFlagId, CancellationToken ct)
    {
        MediaFlagFilterDto unreviewed = filter with { Statuses = ["Unreviewed"] };
        return await repository.GetNeighbourAsync(fromFlagId, unreviewed, MediaFlagNeighbourDto.Next, ct)
            ?? await repository.GetNeighbourAsync(fromFlagId, unreviewed, MediaFlagNeighbourDto.Previous, ct);
    }
}
