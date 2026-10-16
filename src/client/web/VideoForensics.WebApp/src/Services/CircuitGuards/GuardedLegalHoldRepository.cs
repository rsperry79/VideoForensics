using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Ui.Shared.Layout;
using VideoForensics.WebApp.Contracts;

namespace VideoForensics.WebApp.Services.CircuitGuards
{
    /// <summary>
    /// Guards placing and releasing legal holds, which the Evidence page makes in-process. Matches the legal-holds
    /// endpoints, which require Admin. Reading active holds stays open because every operator sees evidence status.
    /// </summary>
    public sealed class GuardedLegalHoldRepository(ILegalHoldRepository inner, ICircuitOperatorGate gate) : ILegalHoldRepository
    {
        public Task<LegalHold> PlaceAsync(Guid mediaItemId, string reason, string createdBy, CancellationToken ct)
        {
            Require();
            return inner.PlaceAsync(mediaItemId, reason, createdBy, ct);
        }

        public Task ReleaseAsync(Guid legalHoldId, string releasedBy, string releaseReason, CancellationToken ct)
        {
            Require();
            return inner.ReleaseAsync(legalHoldId, releasedBy, releaseReason, ct);
        }

        public Task<IReadOnlyList<LegalHold>> GetActiveByMediaItemIdsAsync(IEnumerable<Guid> mediaItemIds, CancellationToken ct) =>
            inner.GetActiveByMediaItemIdsAsync(mediaItemIds, ct);

        private void Require()
        {
            if (!gate.Allows(AccessPolicy.Admin))
            {
                throw new UnauthorizedAccessException("Legal holds require Admin.");
            }
        }
    }
}
