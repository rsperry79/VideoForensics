using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Common.Contracts
{
    /// <summary>Repository for OperatorCredential entities (WebAuthn passkeys).</summary>
    public interface IOperatorCredentialRepository
    {
        /// <summary>Finds an OperatorCredential by its ID.</summary>
        Task<OperatorCredential?> GetAsync(Guid id, CancellationToken ct);

        /// <summary>Finds the active operator credential owning this WebAuthn credential ID, or null if none/revoked.</summary>
        Task<OperatorCredential?> GetByWebAuthnCredentialIdAsync(string credentialId, CancellationToken ct);

        /// <summary>Lists all credentials for an Operator, ordered by creation time descending.</summary>
        Task<IReadOnlyList<OperatorCredential>> ListForOperatorAsync(Guid operatorId, CancellationToken ct);

        /// <summary>Lists all credentials pending approval (not approved and not revoked), used for admin credential management.</summary>
        Task<IReadOnlyList<OperatorCredential>> ListPendingApprovalAsync(CancellationToken ct);

        /// <summary>Adds a new OperatorCredential to the system.</summary>
        Task AddAsync(OperatorCredential credential, CancellationToken ct);

        /// <summary>Marks an OperatorCredential as approved, granting authentication capability.</summary>
        Task ApproveAsync(Guid credentialId, CancellationToken ct);

        /// <summary>Revokes an OperatorCredential with a reason - the immediate, first-class action symmetric with credential approval.</summary>
        Task RevokeAsync(Guid credentialId, string reason, CancellationToken ct);

        /// <summary>Updates the WebAuthn signature counter and last-used timestamp after a successful authentication.</summary>
        Task RecordSuccessfulAuthAsync(Guid credentialId, uint newSignCount, CancellationToken ct);

        /// <summary>Marks the credential as having notified on first successful login (sets FirstLoginNotifiedAtUtc to now).</summary>
        Task MarkFirstLoginNotifiedAsync(Guid credentialId, CancellationToken ct);
    }
}
