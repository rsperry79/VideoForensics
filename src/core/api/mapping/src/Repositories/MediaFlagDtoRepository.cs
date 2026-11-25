using VideoForensics.Client.Hosting.Signal;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Core.Api.Contracts.Repositories
{
    /// <summary>
    /// Server-side <see cref="IMediaFlagRepository"/> that reads flags through <see cref="IMediaFlagWriteRepository"/> and maps
    /// each entity, filter and page to its DTO.
    /// </summary>
    public sealed class MediaFlagDtoRepository(IMediaFlagWriteRepository flags) : IMediaFlagRepository
    {
        /// <inheritdoc />
        public async Task<MediaFlagDto?> GetAsync(Guid flagId, CancellationToken ct)
        {
            MediaFlag? flag = await flags.GetAsync(flagId, ct);
            return flag?.ToDto();
        }

        /// <inheritdoc />
        public async Task<MediaFlagPageDto> ListPageAsync(MediaFlagFilterDto filter, int skip, int take, CancellationToken ct)
        {
            MediaFlagPage page = await flags.ListPageAsync(filter.ToDomain(), skip, take, ct);
            return page.ToDto();
        }

        /// <inheritdoc />
        public Task<int> CountAsync(MediaFlagFilterDto filter, CancellationToken ct) =>
            flags.CountAsync(filter.ToDomain(), ct);

        /// <inheritdoc />
        public async Task<MediaFlagDto?> GetNeighbourAsync(Guid flagId, MediaFlagFilterDto filter, MediaFlagNeighbourDto direction, CancellationToken ct)
        {
            MediaFlag? neighbour = await flags.GetNeighbourAsync(
                flagId,
                filter.ToDomain(),
                Enum.Parse<MediaFlagNeighbour>(direction.ToString()),
                ct);
            return neighbour?.ToDto();
        }

        /// <inheritdoc />
        public Task<int?> GetPositionAsync(Guid flagId, MediaFlagFilterDto filter, CancellationToken ct) =>
            flags.GetPositionAsync(flagId, filter.ToDomain(), ct);

        /// <inheritdoc />
        public async Task<MediaFlagDto> UpdateStatusAsync(Guid flagId, string expectedStatus, string newStatus, string? reason, string operatorName, CancellationToken ct)
        {
            MediaFlag updated = await flags.UpdateStatusAsync(
                flagId,
                Enum.Parse<MediaFlagStatus>(expectedStatus),
                Enum.Parse<MediaFlagStatus>(newStatus),
                reason,
                operatorName,
                ct);
            return updated.ToDto();
        }

        /// <inheritdoc />
        public async Task<MediaFlagDto> ReopenAsync(Guid flagId, string operatorName, string reason, CancellationToken ct)
        {
            MediaFlag reopened = await flags.ReopenAsync(flagId, operatorName, reason, ct);
            return reopened.ToDto();
        }
    }
}
