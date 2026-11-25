using VideoForensics.Data.Common.Contracts;

namespace VideoForensics.Core.Api.Contracts.Repositories
{
    /// <summary>Server-side <see cref="ICaseNoteRepository"/> over <see cref="ICaseNoteWriteRepository"/>, mapping to DTOs.</summary>
    public sealed class CaseNoteDtoRepository(ICaseNoteWriteRepository notes) : ICaseNoteRepository
    {
        /// <inheritdoc />
        public async Task<CaseNoteDto> AddAsync(Guid caseId, string body, string addedBy, CancellationToken ct) =>
            (await notes.AddAsync(caseId, body, addedBy, ct)).ToDto();

        /// <inheritdoc />
        public async Task<IReadOnlyList<CaseNoteDto>> ListForCaseAsync(Guid caseId, CancellationToken ct) =>
            (await notes.ListForCaseAsync(caseId, ct)).Select(n => n.ToDto()).ToList();
    }
}
