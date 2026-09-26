namespace VideoForensics.Data.Common.Entities
{
    /// <summary>A forensic case with defined scope, pinned evidence items, and chain-of-custody tracking.</summary>
    public class ForensicCase
    {
        /// <summary>Gets or sets the case ID.</summary>
        public Guid Id { get; set; }

        /// <summary>Gets or sets the unique case number (required, max 64 chars).</summary>
        public required string CaseNumber { get; set; }

        /// <summary>Gets or sets the case title (required, max 256 chars).</summary>
        public required string Title { get; set; }

        /// <summary>Gets or sets an optional case description (max 4000 chars).</summary>
        public string? Description { get; set; }

        /// <summary>Gets or sets the case status (Open or Closed), stored as string in database.</summary>
        public CaseStatus Status { get; set; } = CaseStatus.Open;

        /// <summary>Gets or sets the ID of the lead operator assigned to this case.</summary>
        public Guid? LeadOperatorId { get; set; }

        /// <summary>Gets or sets the username who created this case (required, max 256 chars).</summary>
        public required string CreatedBy { get; set; }

        /// <summary>Gets or sets the UTC timestamp when the case was created.</summary>
        public DateTime CreatedAtUtc { get; set; }

        /// <summary>Gets or sets the UTC timestamp of the most recent update to this case.</summary>
        public DateTime UpdatedAtUtc { get; set; }

        /// <summary>Gets or sets the username who closed this case, if it is closed.</summary>
        public string? ClosedBy { get; set; }

        /// <summary>Gets or sets the UTC timestamp when this case was closed, if it is closed.</summary>
        public DateTime? ClosedAtUtc { get; set; }

        /// <summary>Gets or sets the start of the time window for this case (inclusive, UTC).</summary>
        public DateTime? ScopeFromUtc { get; set; }

        /// <summary>Gets or sets the end of the time window for this case (exclusive, UTC).</summary>
        public DateTime? ScopeToUtc { get; set; }
    }
}
