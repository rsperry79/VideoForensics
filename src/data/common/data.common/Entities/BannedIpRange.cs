namespace VideoForensics.Data.Common.Entities
{
    /// <summary>
    /// Manual IP address range ban for login prevention: administrators can block specific CIDR ranges (e.g. "203.0.113.0/24") with an optional expiration.
    /// These are checked during login before password verification.
    /// </summary>
    public class BannedIpRange
    {
        /// <summary>
        /// Unique identifier for this ban entry.
        /// </summary>
        public required Guid Id { get; set; }

        /// <summary>
        /// CIDR notation IP range to block, e.g. "203.0.113.0/24" or "2001:db8::/32" for IPv6.
        /// </summary>
        public required string CidrRange { get; set; }

        /// <summary>
        /// Optional human-readable reason for the ban, e.g. "Known botnet C2 network" or "Sanctions regime country".
        /// </summary>
        public string? Reason { get; set; }

        /// <summary>
        /// Timestamp when this ban was created, in UTC.
        /// </summary>
        public DateTime CreatedAtUtc { get; set; }

        /// <summary>
        /// Id of the Operator (usually SuperAdmin) who created this ban.
        /// </summary>
        public required Guid CreatedByOperatorId { get; set; }

        /// <summary>
        /// Optional expiration time for this ban, in UTC; null means the ban is permanent.
        /// </summary>
        public DateTime? ExpiresAtUtc { get; set; }
    }
}
