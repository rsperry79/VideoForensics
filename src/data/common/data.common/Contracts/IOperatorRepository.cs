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

        /// <summary>Marks an Operator as approved, granting access to the system.</summary>
        Task ApproveAsync(Guid operatorId, CancellationToken ct);

        /// <summary>Updates only the DisplayName field for an Operator.</summary>
        Task UpdateDisplayNameAsync(Guid operatorId, string displayName, CancellationToken ct);

        /// <summary>Finds an Operator by their login username, or null if none exists.</summary>
        Task<Operator?> GetByUsernameAsync(string username, CancellationToken ct);

        /// <summary>Sets the password hash for an Operator, regenerates their security stamp (invalidating existing sessions), and updates PasswordUpdatedAtUtc to now.</summary>
        Task SetPasswordAsync(Guid operatorId, string passwordHash, bool mustChangePassword, CancellationToken ct);

        /// <summary>Updates the canonical Role for an Operator.</summary>
        Task SetRoleAsync(Guid operatorId, OperatorRole role, CancellationToken ct);

        /// <summary>Marks that the operator's first successful login after approval has been notified (sets ApprovalFirstLoginNotifiedAtUtc to now).</summary>
        Task SetApprovalFirstLoginNotifiedAsync(Guid operatorId, CancellationToken ct);
    }
}
