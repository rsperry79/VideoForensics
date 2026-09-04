using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Common.Contracts
{
    /// <summary>Repository for Operator entities (plan §5.11).</summary>
    public interface IOperatorRepository
    {
        Task<Operator?> GetAsync(Guid operatorId, CancellationToken ct);
        Task<Operator> AddAsync(Operator @operator, CancellationToken ct);
        Task<IReadOnlyList<Operator>> ListAsync(CancellationToken ct);

        /// <summary>True if no Operator has ever been created - used for the first-pairing-becomes-Super-Admin bootstrap rule (plan §5.10).</summary>
        Task<bool> IsEmptyAsync(CancellationToken ct);

        /// <summary>Marks an Operator inactive. Does NOT cascade-revoke their paired devices - callers should do that explicitly (see IPairedDeviceRepository.RevokeAllForOperatorAsync) so the two effects stay separately auditable.</summary>
        Task DeactivateAsync(Guid operatorId, CancellationToken ct);
    }
}
