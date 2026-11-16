namespace VideoForensics.Core.Api.Contracts
{
    /// <summary>
    /// Full operator record for the SuperAdmin operator pages. Carries the same JSON properties as the
    /// data-layer Operator, except the password hash, which the client never needs.
    /// </summary>
    public class OperatorDto
    {
        public Guid Id { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public DateTime CreatedAtUtc { get; set; }
        public bool Active { get; set; } = true;
        public bool IsApproved { get; set; } = true;
        public string Username { get; set; } = string.Empty;
        public OperatorRoleDto Role { get; set; }
        public bool MustChangePassword { get; set; }
        public DateTime? PasswordUpdatedAtUtc { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public Guid SecurityStamp { get; set; }
        public DateTime? ApprovalFirstLoginNotifiedAtUtc { get; set; }
        public bool IsPrimarySuperAdmin { get; set; }
        public int FailedLoginAttemptCount { get; set; }
        public DateTime? LockedOutUntilUtc { get; set; }
        public TwoFactorRequirementOverrideDto TwoFactorRequirementOverride { get; set; }
        public bool PasskeyRequired { get; set; } = true;
        public List<OperatorCredentialDto> OperatorCredentials { get; set; } = [];
    }
}
