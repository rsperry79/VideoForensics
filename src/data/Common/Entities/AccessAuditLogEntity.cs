namespace VideoForensics.Data.Common.Entities
{
    /// <summary>Records access to evidence items with user, action, and purpose tracking.</summary>
    public class AccessAuditLogEntity
    {
        /// <summary>Gets or sets the unique identifier for this access audit log entry.</summary>
        public Guid Id { get; set; }

        /// <summary>Gets or sets the ID of the evidence that was accessed.</summary>
        public Guid EvidenceId { get; set; }

        /// <summary>Gets or sets the user ID of the person accessing the evidence (max 256 chars).</summary>
        public required string UserId { get; set; }

        /// <summary>Gets or sets the UTC timestamp when the evidence was accessed.</summary>
        public DateTime AccessedAtUtc { get; set; }

        /// <summary>Gets or sets the action performed: "View", "Download", "Export", etc. (max 256 chars).</summary>
        public required string Action { get; set; }

        /// <summary>Gets or sets the IP address from which the access occurred (max 256 chars).</summary>
        public required string IpAddress { get; set; }

        /// <summary>Gets or sets the stated purpose for accessing the evidence (max 2000 chars).</summary>
        public required string Purpose { get; set; }

        /// <summary>Gets or sets the UTC timestamp when this audit log entry was created.</summary>
        public DateTime CreatedAtUtc { get; set; }
    }
}
