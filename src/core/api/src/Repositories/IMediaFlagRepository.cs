namespace VideoForensics.Core.Api.Contracts.Repositories
{
    /// <summary>
    /// Read access to media review flags, with DTO signatures. Client hosts use this interface. Raising, reconciling,
    /// reviewing and reopening flags are server-only (IMediaFlagWriteRepository).
    /// </summary>
    public interface IMediaFlagRepository
    {
        /// <summary>Gets a flag by id, or null.</summary>
        Task<MediaFlagDto?> GetAsync(Guid flagId, CancellationToken ct);

        /// <summary>Gets one page of flags matching the filter, with the total match count.</summary>
        Task<MediaFlagPageDto> ListPageAsync(MediaFlagFilterDto filter, int skip, int take, CancellationToken ct);

        /// <summary>Counts flags matching the filter.</summary>
        Task<int> CountAsync(MediaFlagFilterDto filter, CancellationToken ct);

        /// <summary>Gets the flag before or after <paramref name="flagId"/> in list order among those matching the filter, or null at the end.</summary>
        Task<MediaFlagDto?> GetNeighbourAsync(Guid flagId, MediaFlagFilterDto filter, MediaFlagNeighbourDto direction, CancellationToken ct);

        /// <summary>1-based position of the flag in list order among those matching the filter, or null when missing or filtered out.</summary>
        Task<int?> GetPositionAsync(Guid flagId, MediaFlagFilterDto filter, CancellationToken ct);

        /// <summary>Compare-and-set review (Kept or Dismissed). Statuses are sent by name. Throws on a conflict.</summary>
        Task<MediaFlagDto> UpdateStatusAsync(Guid flagId, string expectedStatus, string newStatus, string? reason, string operatorName, CancellationToken ct);

        /// <summary>Re-opens a Dismissed flag as Unreviewed. Throws on a conflict.</summary>
        Task<MediaFlagDto> ReopenAsync(Guid flagId, string operatorName, string reason, CancellationToken ct);
    }
}
