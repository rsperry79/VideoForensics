namespace VideoForensics.Core.Api.Contracts
{
    /// <summary>
    /// Full operator record for the SuperAdmin operator pages. Carries the same JSON properties as the
    /// data-layer Operator, except the password hash, which the client never needs.
    /// </summary>
    public class OperatorDto
    {
        /// <summary>Operator identifier.</summary>
        public Guid Id { get; set; }
        /// <summary>Operator display name.</summary>
        public string DisplayName { get; set; } = string.Empty;
        /// <summary>When the operator record was created (UTC).</summary>
        public DateTime CreatedAtUtc { get; set; }
        /// <summary>Whether the operator account is active.</summary>
        public bool Active { get; set; } = true;
        /// <summary>Whether an administrator has approved the operator.</summary>
        public bool IsApproved { get; set; } = true;
        /// <summary>Login name.</summary>
        public string Username { get; set; } = string.Empty;
        /// <summary>Operator role.</summary>
        public OperatorRoleDto Role { get; set; }
        /// <summary>Whether the operator must change their password at next sign-in.</summary>
        public bool MustChangePassword { get; set; }
        /// <summary>When the password was last changed (UTC), or null if never changed.</summary>
        public DateTime? PasswordUpdatedAtUtc { get; set; }
        /// <summary>Operator first name.</summary>
        public string FirstName { get; set; } = string.Empty;
        /// <summary>Operator last name.</summary>
        public string LastName { get; set; } = string.Empty;
        /// <summary>Operator email address.</summary>
        public string Email { get; set; } = string.Empty;
        /// <summary>Operator phone number, if recorded.</summary>
        public string? Phone { get; set; }
        /// <summary>Security stamp that invalidates existing sessions when it changes.</summary>
        public Guid SecurityStamp { get; set; }
        /// <summary>When the first-login approval notice was sent (UTC), or null if not sent.</summary>
        public DateTime? ApprovalFirstLoginNotifiedAtUtc { get; set; }
        /// <summary>Whether this is the primary super-administrator.</summary>
        public bool IsPrimarySuperAdmin { get; set; }
        /// <summary>Consecutive failed sign-in attempts.</summary>
        public int FailedLoginAttemptCount { get; set; }
        /// <summary>When the lockout ends (UTC), or null if not locked out.</summary>
        public DateTime? LockedOutUntilUtc { get; set; }
        /// <summary>Per-operator override of the two-factor requirement.</summary>
        public TwoFactorRequirementOverrideDto TwoFactorRequirementOverride { get; set; }
        /// <summary>Whether a passkey is required to sign in.</summary>
        public bool PasskeyRequired { get; set; } = true;
        /// <summary>Passkey credentials registered to the operator.</summary>
        public List<OperatorCredentialDto> OperatorCredentials { get; set; } = [];
    }
}
