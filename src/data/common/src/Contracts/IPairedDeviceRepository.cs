using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Common.Contracts
{
    /// <summary>Repository for PairedDevice entities (plan §5.1/§5.4/§5.10).</summary>
    public interface IPairedDeviceRepository
    {
        Task<PairedDevice?> GetAsync(Guid pairedDeviceId, CancellationToken ct);

        /// <summary>Finds the active paired device owning this WebAuthn credential ID, or null if none/revoked.</summary>
        Task<PairedDevice?> GetByWebAuthnCredentialIdAsync(string credentialId, CancellationToken ct);

        /// <summary>Finds the active paired device owning this fallback API key's hash, or null if none/revoked.</summary>
        Task<PairedDevice?> GetByFallbackApiKeyHashAsync(string apiKeyHash, CancellationToken ct);

        Task<PairedDevice> AddAsync(PairedDevice device, CancellationToken ct);
        Task UpdateAsync(PairedDevice device, CancellationToken ct);
        Task<IReadOnlyList<PairedDevice>> ListAsync(CancellationToken ct);
        Task<IReadOnlyList<PairedDevice>> ListForOperatorAsync(Guid operatorId, CancellationToken ct);

        /// <summary>Sets RevokedAtUtc/RevokedReason - the immediate, first-class action symmetric with pairing (plan §5.4). Does not by itself disconnect a live SignalR connection; the caller (an endpoint with access to the hub context) must do that separately.</summary>
        Task RevokeAsync(Guid pairedDeviceId, string reason, CancellationToken ct);

        /// <summary>Revokes every currently-active device belonging to an Operator in one action - the bulk equivalent of RevokeAsync, used when deactivating an Operator entirely (plan §5.11). Returns the ids revoked, so the caller can force-disconnect each one's live connection.</summary>
        Task<IReadOnlyList<Guid>> RevokeAllForOperatorAsync(Guid operatorId, string reason, CancellationToken ct);

        /// <summary>Updates the WebAuthn signature counter, last-seen timestamp/IP/tier after a successful authentication.</summary>
        Task RecordSuccessfulAuthAsync(Guid pairedDeviceId, uint newSignCount, string? sourceIp, NetworkTier tier, CancellationToken ct);
    }
}
