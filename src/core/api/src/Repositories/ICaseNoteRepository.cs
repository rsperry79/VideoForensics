namespace VideoForensics.Core.Api.Contracts.Repositories
{
    /// <summary>Notes on cases, with DTO signatures. Client hosts use this interface.</summary>
    public interface ICaseNoteRepository
    {
        /// <summary>Appends a note to a case. The author is the signed-in operator.</summary>
        Task<CaseNoteDto> AddAsync(Guid caseId, string body, string addedBy, CancellationToken ct);

        /// <summary>Lists the notes on a case.</summary>
        Task<IReadOnlyList<CaseNoteDto>> ListForCaseAsync(Guid caseId, CancellationToken ct);
    }
}
