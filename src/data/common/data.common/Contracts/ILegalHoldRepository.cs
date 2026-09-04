using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Common.Contracts
{
    /// <summary>Repository for legal holds that exempt media items from retention-policy auto-deletion.</summary>
    public interface ILegalHoldRepository
    {
        /// <summary>Places a new legal hold on a media item, recording the reason and actor. Also appends a chain-of-custody ActionLog entry ("PlaceLegalHold") in the same operation.</summary>
        Task<LegalHold> PlaceAsync(Guid mediaItemId, string reason, string createdBy, CancellationToken ct);

        /// <summary>Releases an active legal hold, recording who released it and why. Also appends a chain-of-custody ActionLog entry ("ReleaseLegalHold") in the same operation. Throws InvalidOperationException if the hold does not exist or is already released.</summary>
        Task ReleaseAsync(Guid legalHoldId, string releasedBy, string releaseReason, CancellationToken ct);

        /// <summary>Gets the currently-active legal hold (ReleasedAtUtc == null) for each of the given media items that has one. Media items with no active hold are simply absent from the result.</summary>
        Task<IReadOnlyList<LegalHold>> GetActiveByMediaItemIdsAsync(IEnumerable<Guid> mediaItemIds, CancellationToken ct);
    }
}
