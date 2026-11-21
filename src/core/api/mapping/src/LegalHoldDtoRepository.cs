using VideoForensics.Data.Common.Contracts;

namespace VideoForensics.Core.Api.Contracts.Repositories
{
    /// <summary>Server-side <see cref="ILegalHoldRepository"/> over <see cref="ILegalHoldWriteRepository"/>, mapping to DTOs.</summary>
    public sealed class LegalHoldDtoRepository(ILegalHoldWriteRepository holds) : ILegalHoldRepository
    {
        /// <inheritdoc />
        public async Task<LegalHoldDto> PlaceAsync(Guid mediaItemId, string reason, string createdBy, CancellationToken ct) =>
            (await holds.PlaceAsync(mediaItemId, reason, createdBy, ct)).ToDto();

        /// <inheritdoc />
        public Task ReleaseAsync(Guid legalHoldId, string releasedBy, string releaseReason, CancellationToken ct) =>
            holds.ReleaseAsync(legalHoldId, releasedBy, releaseReason, ct);

        /// <inheritdoc />
        public async Task<IReadOnlyList<LegalHoldDto>> GetActiveByMediaItemIdsAsync(IEnumerable<Guid> mediaItemIds, CancellationToken ct) =>
            (await holds.GetActiveByMediaItemIdsAsync(mediaItemIds, ct)).Select(h => h.ToDto()).ToList();
    }
}
