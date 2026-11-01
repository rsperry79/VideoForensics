using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Common.Contracts
{
    /// <summary>Append-only repository for case notes. Notes cannot be edited or deleted.</summary>
    public interface ICaseNoteRepository
    {
        /// <summary>
        /// Appends a note to a case and returns it with its write time set to UTC now.
        /// The body is trimmed at the ends and must be non-blank and at most 4000 characters; otherwise ArgumentException.
        /// Throws KeyNotFoundException if the case does not exist, and InvalidOperationException if the case is closed.
        /// </summary>
        /// <param name="caseId">ID of the case to add the note to.</param>
        /// <param name="body">Note text; may contain line breaks.</param>
        /// <param name="addedBy">Operator ID of the author (max 256 chars).</param>
        /// <param name="ct">Cancellation token.</param>
        Task<CaseNote> AddAsync(Guid caseId, string body, string addedBy, CancellationToken ct);

        /// <summary>Lists all notes for a case, oldest first.</summary>
        /// <param name="caseId">ID of the case.</param>
        /// <param name="ct">Cancellation token.</param>
        Task<IReadOnlyList<CaseNote>> ListForCaseAsync(Guid caseId, CancellationToken ct);
    }
}
