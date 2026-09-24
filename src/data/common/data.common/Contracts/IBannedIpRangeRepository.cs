using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Common.Contracts
{
    /// <summary>Repository for BannedIpRange, manual IP address range bans for login prevention.</summary>
    public interface IBannedIpRangeRepository
    {
        /// <summary>
        /// Retrieves all active (non-expired) banned IP ranges.
        /// A ban is active if ExpiresAtUtc is null (permanent) or ExpiresAtUtc > DateTime.UtcNow (not yet expired).
        /// </summary>
        Task<IReadOnlyList<BannedIpRange>> GetActiveAsync(CancellationToken ct);

        /// <summary>
        /// Adds a new BannedIpRange entry.
        /// </summary>
        Task AddAsync(BannedIpRange range, CancellationToken ct);

        /// <summary>
        /// Removes a BannedIpRange by its Id.
        /// </summary>
        Task RemoveAsync(Guid id, CancellationToken ct);
    }
}
