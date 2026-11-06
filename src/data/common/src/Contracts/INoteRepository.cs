using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Common.Contracts
{
    /// <summary>Append-only repository for notes on a case, jamming incident or media flag. Notes cannot be edited or deleted.</summary>
    public interface INoteRepository
    {
        /// <summary>
        /// Appends a note to a target and returns it with its write time set to UTC now.
        /// The body is trimmed at the ends and must be non-blank and at most 4000 characters; otherwise ArgumentException.
        /// For <see cref="NoteTargetKind.Case"/> only: throws KeyNotFoundException if the case does not exist, and
        /// InvalidOperationException if the case is closed. For other kinds the target is not checked here; the caller checks it.
        /// </summary>
        /// <param name="kind">Kind of record the note is attached to.</param>
        /// <param name="targetId">ID of the target record, interpreted by <paramref name="kind"/>.</param>
        /// <param name="body">Note text; may contain line breaks.</param>
        /// <param name="addedBy">Operator ID of the author (max 256 chars).</param>
        /// <param name="ct">Cancellation token.</param>
        Task<Note> AddAsync(NoteTargetKind kind, Guid targetId, string body, string addedBy, CancellationToken ct);

        /// <summary>Lists all notes for a target of the given kind, oldest first.</summary>
        /// <param name="kind">Kind of record the notes are attached to.</param>
        /// <param name="targetId">ID of the target record.</param>
        /// <param name="ct">Cancellation token.</param>
        Task<IReadOnlyList<Note>> ListAsync(NoteTargetKind kind, Guid targetId, CancellationToken ct);
    }
}
