using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Common.Contracts
{
    /// <summary>Repository for media review flags. Lists order by RaisedAtUtc then Id, newest first.</summary>
    public interface IMediaFlagWriteRepository
    {
        /// <summary>
        /// Inserts the flag, or refreshes the existing Unreviewed row with the same key (MediaItemId + FlagType, or
        /// DeviceId + FlagType + WindowStartUtc when there is no clip). Kept and Dismissed rows are returned untouched.
        /// Throws ArgumentException when DeviceId is empty or the window ends before it starts.
        /// </summary>
        Task<MediaFlag> UpsertRaisedAsync(MediaFlag raised, CancellationToken ct);

        /// <summary>Removes this clip's Unreviewed flags whose type is not in <paramref name="raised"/>. Reviewed rows stay. Returns the number removed.</summary>
        Task<int> ReconcileForMediaAsync(Guid mediaItemId, IReadOnlySet<MediaFlagType> raised, CancellationToken ct);

        /// <summary>Gets a flag by id, or null.</summary>
        Task<MediaFlag?> GetAsync(Guid flagId, CancellationToken ct);

        /// <summary>Gets one page of flags matching the filter, with the total match count.</summary>
        Task<MediaFlagPage> ListPageAsync(MediaFlagFilter filter, int skip, int take, CancellationToken ct);

        /// <summary>Counts flags matching the filter.</summary>
        Task<int> CountAsync(MediaFlagFilter filter, CancellationToken ct);

        /// <summary>Gets the flag before or after <paramref name="flagId"/> in list order among those matching the filter, or null at the end.</summary>
        Task<MediaFlag?> GetNeighbourAsync(Guid flagId, MediaFlagFilter filter, MediaFlagNeighbour direction, CancellationToken ct);

        /// <summary>
        /// Gets the 1-based position of the flag in list order among those matching the filter, or null when the flag is
        /// missing or filtered out. Position 1 is the newest flag, so the position counts the matching flags listed before it.
        /// </summary>
        Task<int?> GetPositionAsync(Guid flagId, MediaFlagFilter filter, CancellationToken ct);

        /// <summary>
        /// Compare-and-set review. Moves the flag to Kept or Dismissed only if it is still <paramref name="expectedStatus"/> and
        /// unchanged since it was read; otherwise throws <see cref="FlagStatusConflictException"/>. Dismissed needs a non-empty
        /// reason; Kept takes an optional note. Writes the change and its chain-of-custody ActionLog entry in one save.
        /// Moving to Unreviewed is <see cref="ReopenAsync"/>. Throws KeyNotFoundException when the flag does not exist.
        /// </summary>
        Task<MediaFlag> UpdateStatusAsync(Guid flagId, MediaFlagStatus expectedStatus, MediaFlagStatus newStatus, string? reason, string operatorName, CancellationToken ct);

        /// <summary>
        /// Re-opens a Dismissed flag as Unreviewed and clears the review fields; the ActionLog entry keeps the history. The role check is the caller's.
        /// Throws <see cref="FlagStatusConflictException"/> when the flag is not Dismissed, and ArgumentException for an empty reason.
        /// </summary>
        Task<MediaFlag> ReopenAsync(Guid flagId, string operatorName, string reason, CancellationToken ct);
    }
}
