namespace VideoForensics.Core.Api.Contracts.Repositories
{
    /// <summary>Legal holds on media, with DTO signatures. Client hosts use this interface; the server implements it.</summary>
    public interface ILegalHoldRepository
    {
        /// <summary>Places a legal hold on a media item and returns it.</summary>
        Task<LegalHoldDto> PlaceAsync(Guid mediaItemId, string reason, string createdBy, CancellationToken ct);

        /// <summary>Releases a legal hold.</summary>
        Task ReleaseAsync(Guid legalHoldId, string releasedBy, string releaseReason, CancellationToken ct);

        /// <summary>Active legal holds for the given media items.</summary>
        Task<IReadOnlyList<LegalHoldDto>> GetActiveByMediaItemIdsAsync(IEnumerable<Guid> mediaItemIds, CancellationToken ct);
    }
}
