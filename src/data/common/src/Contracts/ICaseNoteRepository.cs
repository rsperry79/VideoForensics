using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Common.Contracts
{
    /// <summary>Case-only view of <see cref="INoteRepository"/>, used by the case notes API and the remote case client. Notes cannot be edited or deleted.</summary>
    public interface ICaseNoteRepository
    {
        /// <summary>
        /// Appends a note to a case. Same rules as <see cref="INoteRepository.AddAsync"/> with kind <see cref="NoteTargetKind.Case"/>:
        /// ArgumentException for a blank or over-long body, KeyNotFoundException for an unknown case, InvalidOperationException for a closed case.
        /// </summary>
        /// <param name="caseId">ID of the case to add the note to.</param>
        /// <param name="body">Note text; may contain line breaks.</param>
        /// <param name="addedBy">Operator ID of the author (max 256 chars).</param>
        /// <param name="ct">Cancellation token.</param>
        Task<Note> AddAsync(Guid caseId, string body, string addedBy, CancellationToken ct);

        /// <summary>Lists all notes for a case, oldest first.</summary>
        /// <param name="caseId">ID of the case.</param>
        /// <param name="ct">Cancellation token.</param>
        Task<IReadOnlyList<Note>> ListForCaseAsync(Guid caseId, CancellationToken ct);
    }
}
