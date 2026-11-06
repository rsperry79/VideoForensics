using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Core.Api.Contracts
{
    /// <summary>
    /// Data transfer object for a note attached to a case, a jamming incident or a media flag.
    /// </summary>
    /// <param name="Id">Unique identifier for the note.</param>
    /// <param name="TargetKind">Kind of record the note is attached to.</param>
    /// <param name="TargetId">ID of the target record, interpreted by <paramref name="TargetKind"/>.</param>
    /// <param name="Body">Note text, trimmed at the ends (max 4000 chars).</param>
    /// <param name="AddedBy">Operator ID that wrote the note.</param>
    /// <param name="AddedAtUtc">Timestamp when the note was written (UTC).</param>
    public record NoteDto(
        Guid Id,
        NoteTargetKind TargetKind,
        Guid TargetId,
        string Body,
        string AddedBy,
        DateTime AddedAtUtc
    );

    /// <summary>
    /// Request DTO for adding a note to a case, jamming incident or media flag. The author is the signed-in operator, taken server-side.
    /// </summary>
    /// <param name="Body">Note text (required, max 4000 chars after trimming).</param>
    public record AddNoteRequest(
        string Body
    );

    /// <summary>Extension methods for mapping any-kind Note entities to NoteDto.</summary>
    public static class NoteDtoMapping
    {
        /// <summary>Converts a Note entity of any target kind to a NoteDto.</summary>
        /// <param name="note">The note entity to convert.</param>
        public static NoteDto ToNoteDto(this Note note)
        {
            return new NoteDto(note.Id, note.TargetKind, note.TargetId, note.Body, note.AddedBy, note.AddedAtUtc);
        }

        /// <summary>Converts a NoteDto to a Note entity of the DTO's target kind.</summary>
        /// <param name="dto">The note DTO to convert.</param>
        public static Note ToDomain(this NoteDto dto)
        {
            return new Note
            {
                Id = dto.Id,
                TargetKind = dto.TargetKind,
                TargetId = dto.TargetId,
                Body = dto.Body,
                AddedBy = dto.AddedBy,
                AddedAtUtc = dto.AddedAtUtc,
            };
        }
    }
}
