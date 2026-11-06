namespace VideoForensics.Data.Core.Contracts
{
    /// <summary>Raises the BitrateCollapse review flag for recent clips that were flagged before the review feature existed.</summary>
    public interface IMediaFlagBackfill
    {
        /// <summary>
        /// Upserts one BitrateCollapse flag per flagged clip in the backfill window. Returns how many flags were upserted.
        /// Running it again changes nothing, because an existing Unreviewed flag is refreshed rather than duplicated.
        /// </summary>
        Task<int> BackfillAsync(CancellationToken ct);
    }
}
