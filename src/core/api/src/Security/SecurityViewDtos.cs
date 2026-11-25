namespace VideoForensics.Core.Api.Contracts
{
    /// <summary>A passkey credential registered to an operator. JSON shape matches the self-API responses.</summary>
    public class OperatorCredentialDto
    {
        /// <summary>Credential identifier.</summary>
        public Guid Id { get; set; }
        /// <summary>Owning operator identifier.</summary>
        public Guid OperatorId { get; set; }
        /// <summary>User-supplied label for the credential.</summary>
        public string Label { get; set; } = string.Empty;
        /// <summary>WebAuthn credential identifier.</summary>
        public string WebAuthnCredentialId { get; set; } = string.Empty;
        /// <summary>WebAuthn public key bytes.</summary>
        public byte[] WebAuthnPublicKey { get; set; } = [];
        /// <summary>WebAuthn signature counter.</summary>
        public uint WebAuthnSignCount { get; set; }
        /// <summary>When the credential was registered (UTC).</summary>
        public DateTime CreatedAtUtc { get; set; }
        /// <summary>When the credential was last used (UTC), or null if never used.</summary>
        public DateTime? LastUsedAtUtc { get; set; }
        /// <summary>Whether an administrator has approved the credential.</summary>
        public bool IsApproved { get; set; }
        /// <summary>When the credential was revoked (UTC), or null if not revoked.</summary>
        public DateTime? RevokedAtUtc { get; set; }
        /// <summary>Reason the credential was revoked, if revoked.</summary>
        public string? RevokedReason { get; set; }
        /// <summary>True while the credential has not been revoked.</summary>
        public bool IsActive => RevokedAtUtc == null;
        /// <summary>When the first-login notice for this credential was sent (UTC), or null if not sent.</summary>
        public DateTime? FirstLoginNotifiedAtUtc { get; set; }
    }

    /// <summary>One row of the security audit log. JSON shape matches the self-API responses.</summary>
    public class SecurityAuditLogEntryDto
    {
        /// <summary>Audit log entry identifier.</summary>
        public Guid Id { get; set; }
        /// <summary>When the event occurred (UTC).</summary>
        public DateTime TimestampUtc { get; set; }
        /// <summary>Audit event type name.</summary>
        public string EventType { get; set; } = string.Empty;
        /// <summary>Operator involved in the event, or null if none.</summary>
        public Guid? OperatorId { get; set; }
        /// <summary>Paired device involved in the event, or null if none.</summary>
        public Guid? PairedDeviceId { get; set; }
        /// <summary>Source IP address of the request, if known.</summary>
        public string? SourceIp { get; set; }
        /// <summary>Free-text event details, if any.</summary>
        public string? Details { get; set; }
        /// <summary>Whether the event is flagged as urgent.</summary>
        public bool IsUrgent { get; set; }
    }

    /// <summary>A paired device (passkey or fallback key) for an operator. JSON shape matches the self-API responses.</summary>
    public class PairedDeviceDto
    {
        /// <summary>Paired device identifier.</summary>
        public Guid Id { get; set; }
        /// <summary>Owning operator identifier.</summary>
        public Guid OperatorId { get; set; }
        /// <summary>Display name of the device.</summary>
        public string DeviceName { get; set; } = string.Empty;
        /// <summary>Role granted to the device.</summary>
        public OperatorRoleDto Role { get; set; }
        /// <summary>WebAuthn credential identifier, for passkey devices.</summary>
        public string? WebAuthnCredentialId { get; set; }
        /// <summary>WebAuthn public key bytes, for passkey devices.</summary>
        public byte[]? WebAuthnPublicKey { get; set; }
        /// <summary>WebAuthn signature counter.</summary>
        public uint WebAuthnSignCount { get; set; }
        /// <summary>Hash of the fallback API key, for non-passkey devices.</summary>
        public string? FallbackApiKeyHash { get; set; }
        /// <summary>Pinned TLS certificate fingerprint.</summary>
        public string? PinnedCertificateFingerprint { get; set; }
        /// <summary>When the device was paired (UTC).</summary>
        public DateTime PairedAtUtc { get; set; }
        /// <summary>When the device was last seen (UTC), or null if never seen.</summary>
        public DateTime? LastSeenAtUtc { get; set; }
        /// <summary>IP address the device was last seen from.</summary>
        public string? LastSeenIp { get; set; }
        /// <summary>Network tier the device was last seen on.</summary>
        public NetworkTierDto? LastSeenTier { get; set; }
        /// <summary>When the device was revoked (UTC), or null if not revoked.</summary>
        public DateTime? RevokedAtUtc { get; set; }
        /// <summary>Reason the device was revoked, if revoked.</summary>
        public string? RevokedReason { get; set; }
        /// <summary>True while the device has not been revoked.</summary>
        public bool IsActive => RevokedAtUtc == null;
    }
}
