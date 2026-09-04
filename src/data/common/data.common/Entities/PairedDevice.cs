namespace VideoForensics.Data.Common.Entities
{
    /// <summary>
    /// A device+Operator combination authorized to talk to this server's API (plan §5.1) - governs
    /// WHICH physical devices, operated by WHOM, may authenticate at all. Distinct from
    /// <see cref="OperatorRole"/> which governs what an already-paired identity may then DO.
    ///
    /// Exactly one of (WebAuthnCredentialId/WebAuthnPublicKey) or FallbackApiKeyHash is populated,
    /// depending on whether the pairing ceremony used a real passkey or the SecureStorage-key
    /// fallback (plan §5.1's stated contingency).
    /// </summary>
    public class PairedDevice
    {
        public Guid Id { get; set; }
        public Guid OperatorId { get; set; }
        public required string DeviceName { get; set; }
        public OperatorRole Role { get; set; }

        /// <summary>Base64url-encoded WebAuthn credential ID, set only for a real passkey pairing.</summary>
        public string? WebAuthnCredentialId { get; set; }

        /// <summary>The credential's public key (COSE-encoded), set only for a real passkey pairing.</summary>
        public byte[]? WebAuthnPublicKey { get; set; }

        /// <summary>
        /// The authenticator's signature counter as of the last successful authentication - must
        /// only ever increase; a value that doesn't increase (or resets) between authentications is
        /// a cloned-authenticator red flag per the WebAuthn spec.
        /// </summary>
        public uint WebAuthnSignCount { get; set; }

        /// <summary>SHA-256 hash of the fallback device-bound API key, set only when the SecureStorage-key contingency was used instead of a real passkey. Never store the raw key.</summary>
        public string? FallbackApiKeyHash { get; set; }

        /// <summary>
        /// SHA-256 fingerprint of the TLS certificate this device pinned at pairing time - checked
        /// on every subsequent connection (plan §5.1), not just at pairing. A mismatch means either
        /// the server's cert legitimately rotated (out of band re-confirmation needed) or a
        /// man-in-the-middle; the client must refuse to connect rather than silently trusting it.
        /// </summary>
        public string? PinnedCertificateFingerprint { get; set; }

        public DateTime PairedAtUtc { get; set; }
        public DateTime? LastSeenAtUtc { get; set; }
        public string? LastSeenIp { get; set; }
        public NetworkTier? LastSeenTier { get; set; }

        public DateTime? RevokedAtUtc { get; set; }
        public string? RevokedReason { get; set; }

        public bool IsActive => RevokedAtUtc == null;
    }
}
