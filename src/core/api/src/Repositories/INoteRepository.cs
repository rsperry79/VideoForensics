namespace VideoForensics.Core.Api.Contracts.Repositories
{
    /// <summary>Notes on jamming incidents, media flags and cases, with DTO signatures. Client hosts use this interface.</summary>
    public interface INoteRepository
    {
        /// <summary>Appends a note to a target. The author is the signed-in operator.</summary>
        Task<NoteDto> AddAsync(NoteTargetKindDto kind, Guid targetId, string body, string addedBy, CancellationToken ct);

        /// <summary>Lists the notes on a target.</summary>
        Task<IReadOnlyList<NoteDto>> ListAsync(NoteTargetKindDto kind, Guid targetId, CancellationToken ct);
    }
}
