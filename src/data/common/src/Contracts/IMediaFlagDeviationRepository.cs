using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Common.Contracts
{
    /// <summary>Reads and fills the stored <see cref="MediaFlag.DeviationSigma"/> for the one-off deviation backfill.</summary>
    public interface IMediaFlagDeviationRepository
    {
        /// <summary>Lists flags of one type whose DeviationSigma is still null, in Id order. Reviewed flags are included.</summary>
        Task<IReadOnlyList<MediaFlag>> ListWithoutDeviationSigmaAsync(MediaFlagType flagType, CancellationToken ct);

        /// <summary>
        /// Stores the sigma on a flag that has none. A flag that already has a value is left as it is, so the call is safe to
        /// repeat. Does not change the review state or the row version.
        /// </summary>
        Task SetDeviationSigmaAsync(Guid flagId, double deviationSigma, CancellationToken ct);
    }
}
