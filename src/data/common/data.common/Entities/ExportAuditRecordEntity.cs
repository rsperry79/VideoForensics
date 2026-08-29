namespace VideoForensics.Data.Common.Entities
{
    /// <summary>Records evidence export operations with format, event count, and purpose tracking.</summary>
    public class ExportAuditRecordEntity
    {
        /// <summary>Gets or sets the unique identifier for this export audit record (ExportId).</summary>
        public Guid Id { get; set; }

        /// <summary>Gets or sets the ID of the location from which evidence was exported.</summary>
        public Guid LocationId { get; set; }

        /// <summary>Gets or sets the UTC timestamp when the export operation completed.</summary>
        public DateTime ExportedAtUtc { get; set; }

        /// <summary>Gets or sets the user identifier of who performed the export (max 256 chars).</summary>
        public required string ExportedBy { get; set; }

        /// <summary>Gets or sets the number of events that were exported.</summary>
        public int EventsExported { get; set; }

        /// <summary>Gets or sets the export format used: "AES256Zip", "PDF", etc. (max 256 chars).</summary>
        public required string ExportFormat { get; set; }

        /// <summary>Gets or sets the stated purpose for exporting the evidence (max 2000 chars).</summary>
        public required string Purpose { get; set; }
    }
}
