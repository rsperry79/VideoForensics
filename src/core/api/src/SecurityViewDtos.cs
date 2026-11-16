namespace VideoForensics.Core.Api.Contracts
{
    /// <summary>A passkey credential registered to an operator. JSON shape matches the self-API responses.</summary>
    public class OperatorCredentialDto
    {
        public Guid Id { get; set; }
        public Guid OperatorId { get; set; }
        public string Label { get; set; } = string.Empty;
        public string WebAuthnCredentialId { get; set; } = string.Empty;
        public byte[] WebAuthnPublicKey { get; set; } = [];
        public uint WebAuthnSignCount { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public DateTime? LastUsedAtUtc { get; set; }
        public bool IsApproved { get; set; }
        public DateTime? RevokedAtUtc { get; set; }
        public string? RevokedReason { get; set; }
        public bool IsActive => RevokedAtUtc == null;
        public DateTime? FirstLoginNotifiedAtUtc { get; set; }
    }

    /// <summary>One row of the security audit log. JSON shape matches the self-API responses.</summary>
    public class SecurityAuditLogEntryDto
    {
        public Guid Id { get; set; }
        public DateTime TimestampUtc { get; set; }
        public string EventType { get; set; } = string.Empty;
        public Guid? OperatorId { get; set; }
        public Guid? PairedDeviceId { get; set; }
        public string? SourceIp { get; set; }
        public string? Details { get; set; }
        public bool IsUrgent { get; set; }
    }

    /// <summary>A paired device (passkey or fallback key) for an operator. JSON shape matches the self-API responses.</summary>
    public class PairedDeviceDto
    {
        public Guid Id { get; set; }
        public Guid OperatorId { get; set; }
        public string DeviceName { get; set; } = string.Empty;
        public OperatorRoleDto Role { get; set; }
        public string? WebAuthnCredentialId { get; set; }
        public byte[]? WebAuthnPublicKey { get; set; }
        public uint WebAuthnSignCount { get; set; }
        public string? FallbackApiKeyHash { get; set; }
        public string? PinnedCertificateFingerprint { get; set; }
        public DateTime PairedAtUtc { get; set; }
        public DateTime? LastSeenAtUtc { get; set; }
        public string? LastSeenIp { get; set; }
        public NetworkTierDto? LastSeenTier { get; set; }
        public DateTime? RevokedAtUtc { get; set; }
        public string? RevokedReason { get; set; }
        public bool IsActive => RevokedAtUtc == null;
    }
}
