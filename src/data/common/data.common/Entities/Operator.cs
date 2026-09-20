namespace VideoForensics.Data.Common.Entities
{
    /// <summary>
    /// A local person operating this VideoForensics installation - distinct from the existing
    /// Ring-scoped <see cref="User"/> entity (which represents the Ring account holder, keyed by
    /// ProviderUserKey). Exists for legal/chain-of-custody attribution: a paired identity is really
    /// (Operator, device credential, Role), not just a device, so a shared laptop can host multiple
    /// separately-attributed people (plan §5.11).
    /// </summary>
    public class Operator
    {
        public Guid Id { get; set; }
        public required string DisplayName { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public bool Active { get; set; } = true;

        /// <summary>
        /// True if this operator is approved to access the system; false if awaiting approval (for self-service signups when other operators already exist).
        /// Bootstrap operators (first pairing) default to true. Admin-approved operators are set to true.
        /// </summary>
        public bool IsApproved { get; set; } = true;

        /// <summary>
        /// Login handle for human web authentication, distinct from DisplayName; unique across all operators.
        /// </summary>
        public required string Username { get; set; }

        /// <summary>
        /// Canonical role for this operator, used by password/passkey web logins; service/device credentials in PairedDevice keep their own independently-settable Role.
        /// </summary>
        public OperatorRole Role { get; set; }

        /// <summary>
        /// PBKDF2 hash of the operator's password via PasswordHasher&lt;Operator&gt;; null until a password is set.
        /// </summary>
        public string? PasswordHash { get; set; }

        /// <summary>
        /// True after a SuperAdmin issues a temporary password; forces a password change on next login.
        /// </summary>
        public bool MustChangePassword { get; set; }

        /// <summary>
        /// Timestamp of the last password update or reset, in UTC.
        /// </summary>
        public DateTime? PasswordUpdatedAtUtc { get; set; }

        /// <summary>
        /// First name of the operator; used for contact identity and display purposes.
        /// </summary>
        public required string FirstName { get; set; }

        /// <summary>
        /// Last name of the operator; used for contact identity and display purposes.
        /// </summary>
        public required string LastName { get; set; }

        /// <summary>
        /// Email address of the operator; unique across all operators, used for notifications.
        /// </summary>
        public required string Email { get; set; }

        /// <summary>
        /// Optional phone number for the operator.
        /// </summary>
        public string? Phone { get; set; }

        /// <summary>
        /// Security stamp regenerated on creation and on every password reset/change; baked into issued session tokens to invalidate existing sessions on password change.
        /// </summary>
        public Guid SecurityStamp { get; set; }

        /// <summary>
        /// One-shot flag set the first time a newly-approved self-service operator successfully logs in, so the approval-confirmation notice fires exactly once.
        /// </summary>
        public DateTime? ApprovalFirstLoginNotifiedAtUtc { get; set; }
    }
}
