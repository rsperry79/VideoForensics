using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Database.Repositories
{
    /// <summary>Case-only adapter over <see cref="INoteWriteRepository"/>: every call uses <see cref="NoteTargetKind.Case"/>.</summary>
    public class CaseNoteAdapter : ICaseNoteWriteRepository
    {
        private readonly INoteWriteRepository _notes;

        /// <summary>Initializes a new instance of the CaseNoteAdapter.</summary>
        public CaseNoteAdapter(INoteWriteRepository notes)
        {
            _notes = notes;
        }

        /// <summary>Appends a note to a case. See <see cref="INoteWriteRepository.AddAsync"/> for the rules.</summary>
        public Task<Note> AddAsync(Guid caseId, string body, string addedBy, CancellationToken ct)
        {
            return _notes.AddAsync(NoteTargetKind.Case, caseId, body, addedBy, ct);
        }

        /// <summary>Lists all notes for a case, oldest first.</summary>
        public Task<IReadOnlyList<Note>> ListForCaseAsync(Guid caseId, CancellationToken ct)
        {
            return _notes.ListAsync(NoteTargetKind.Case, caseId, ct);
        }
    }
}
