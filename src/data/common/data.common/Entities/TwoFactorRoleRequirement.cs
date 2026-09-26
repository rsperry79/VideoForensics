namespace VideoForensics.Data.Common.Entities
{
    /// <summary>
    /// Two-factor authentication requirement per operator role: four rows (one per OperatorRole), each specifying whether that role must use two-factor auth.
    /// All roles default to true (two-factor required), implementing secure-by-default for the entire app.
    /// </summary>
    public class TwoFactorRoleRequirement
    {
        /// <summary>
        /// Unique identifier for this role requirement row.
        /// </summary>
        public required Guid Id { get; set; }

        /// <summary>
        /// The OperatorRole this requirement applies to (ReadOnly, Review, Admin, SuperAdmin).
        /// One row per role value; the Role column has a unique index.
        /// </summary>
        public OperatorRole Role { get; set; }

        /// <summary>
        /// When true, operators with this role must use two-factor authentication.
        /// When false, two-factor is optional for this role (but individual operators can override via Operator.TwoFactorRequirementOverride).
        /// </summary>
        public bool RequireTwoFactor { get; set; } = true;

        /// <summary>
        /// Timestamp of the last update to this requirement, in UTC.
        /// </summary>
        public DateTime UpdatedAtUtc { get; set; }

        /// <summary>
        /// Id of the Operator who last updated this requirement; null if updated by system initialization.
        /// </summary>
        public Guid? UpdatedByOperatorId { get; set; }
    }
}
