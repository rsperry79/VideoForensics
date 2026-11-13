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
        NoteTargetKindDto TargetKind,
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
}
