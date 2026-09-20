namespace VideoForensics.Data.Common.Entities
{
    /// <summary>
    /// A passkey bound to an operator for human web login, distinct from <see cref="PairedDevice"/> which is scoped to service/device pairing.
    /// Represents a WebAuthn credential registered to an operator and used for username-scoped password/passkey authentication.
    /// </summary>
    public class OperatorCredential
    {
        public Guid Id { get; set; }
        public Guid OperatorId { get; set; }

        /// <summary>
        /// User-supplied friendly name for this credential, e.g. "Chrome on Richard's PC".
        /// </summary>
        public required string Label { get; set; }

        /// <summary>
        /// Base64url-encoded WebAuthn credential ID.
        /// </summary>
        public required string WebAuthnCredentialId { get; set; }

        /// <summary>
        /// The credential's public key (COSE-encoded).
        /// </summary>
        public required byte[] WebAuthnPublicKey { get; set; }

        /// <summary>
        /// The authenticator's signature counter as of the last successful authentication - must
        /// only ever increase; a value that doesn't increase (or resets) between authentications is
        /// a cloned-authenticator red flag per the WebAuthn spec.
        /// </summary>
        public uint WebAuthnSignCount { get; set; }

        public DateTime CreatedAtUtc { get; set; }
        public DateTime? LastUsedAtUtc { get; set; }

        /// <summary>
        /// False until a SuperAdmin/Admin approves this credential; except credentials created alongside a brand-new operator registration are approved together with the operator itself.
        /// </summary>
        public bool IsApproved { get; set; }

        public DateTime? RevokedAtUtc { get; set; }
        public string? RevokedReason { get; set; }

        /// <summary>
        /// Computed property indicating whether this credential is currently active (not revoked).
        /// </summary>
        public bool IsActive => RevokedAtUtc == null;

        /// <summary>
        /// One-shot flag set the first time this newly-approved credential is successfully used to sign in, so the approval-confirmation notice fires exactly once.
        /// </summary>
        public DateTime? FirstLoginNotifiedAtUtc { get; set; }
    }
}
