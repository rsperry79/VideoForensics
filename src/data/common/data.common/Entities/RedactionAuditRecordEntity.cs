namespace VideoForensics.Data.Common.Entities
{
    /// <summary>Records evidence redaction operations with approval and justification tracking.</summary>
    public class RedactionAuditRecordEntity
    {
        /// <summary>Gets or sets the unique identifier for this redaction audit record.</summary>
        public Guid Id { get; set; }

        /// <summary>Gets or sets the ID of the evidence that was redacted.</summary>
        public Guid EvidenceId { get; set; }

        /// <summary>Gets or sets the UTC timestamp when the redaction was performed.</summary>
        public DateTime RedactedAtUtc { get; set; }

        /// <summary>Gets or sets the user identifier of who performed the redaction (max 256 chars).</summary>
        public required string RedactedBy { get; set; }

        /// <summary>Gets or sets the user identifier of who approved the redaction (max 256 chars).</summary>
        public required string ApprovedBy { get; set; }

        /// <summary>Gets or sets the description of what content was redacted (max 2000 chars).</summary>
        public required string ContentRedacted { get; set; }

        /// <summary>Gets or sets the justification notes for the redaction (max 2000 chars).</summary>
        public required string JustificationNotes { get; set; }
    }
}
