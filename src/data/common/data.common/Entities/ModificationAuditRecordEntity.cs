namespace VideoForensics.Data.Common.Entities
{
    /// <summary>Records modifications to events with type, approval status, and change tracking.</summary>
    public class ModificationAuditRecordEntity
    {
        /// <summary>Gets or sets the unique identifier for this modification audit record.</summary>
        public Guid Id { get; set; }

        /// <summary>Gets or sets the ID of the event that was modified.</summary>
        public Guid EventId { get; set; }

        /// <summary>Gets or sets the UTC timestamp when the modification was made.</summary>
        public DateTime ModifiedAtUtc { get; set; }

        /// <summary>Gets or sets the user identifier of who performed the modification (max 256 chars).</summary>
        public required string ModifiedBy { get; set; }

        /// <summary>Gets or sets the type of modification: "TimestampAdjustment", "MetadataUpdate", etc. (max 256 chars).</summary>
        public required string ModificationType { get; set; }

        /// <summary>Gets or sets a summary of the changes made (max 2000 chars).</summary>
        public required string ChangeSummary { get; set; }

        /// <summary>Gets or sets a value indicating whether the modification was approved by the investigator.</summary>
        public bool ApprovedByInvestigator { get; set; }
    }
}
