namespace VideoForensics.Data.Common.Entities
{
    /// <summary>An append-only, multi-line text note on a case, jamming incident or media flag. Notes are never edited or deleted.</summary>
    public class Note
    {
        /// <summary>Gets or sets the note ID.</summary>
        public Guid Id { get; set; }

        /// <summary>Gets or sets the kind of record this note is attached to (stored by name, max 32 chars).</summary>
        public NoteTargetKind TargetKind { get; set; }

        /// <summary>Gets or sets the ID of the target record. Its meaning depends on <see cref="TargetKind"/>. No foreign key: a note can target several tables.</summary>
        public Guid TargetId { get; set; }

        /// <summary>Gets or sets the note text. Multi-line; stored as given, trimmed only at the ends (required, max 4000 chars).</summary>
        public required string Body { get; set; }

        /// <summary>Gets or sets the operator ID that wrote the note, as a string (required, max 256 chars).</summary>
        public required string AddedBy { get; set; }

        /// <summary>Gets or sets the UTC timestamp when the note was written.</summary>
        public DateTime AddedAtUtc { get; set; }
    }
}
