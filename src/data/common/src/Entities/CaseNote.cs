namespace VideoForensics.Data.Common.Entities
{
    /// <summary>An append-only, multi-line text note on a forensic case. Notes are never edited or deleted.</summary>
    public class CaseNote
    {
        /// <summary>Gets or sets the note ID.</summary>
        public Guid Id { get; set; }

        /// <summary>Gets or sets the ID of the case this note belongs to (foreign key, no cascade delete: notes are kept).</summary>
        public Guid CaseId { get; set; }

        /// <summary>Gets or sets the note text. Multi-line; stored as given, trimmed only at the ends (required, max 4000 chars).</summary>
        public required string Body { get; set; }

        /// <summary>Gets or sets the operator ID that wrote the note, as a string (required, max 256 chars).</summary>
        public required string AddedBy { get; set; }

        /// <summary>Gets or sets the UTC timestamp when the note was written.</summary>
        public DateTime AddedAtUtc { get; set; }
    }
}
