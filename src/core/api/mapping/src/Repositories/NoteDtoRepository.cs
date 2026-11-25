using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Core.Api.Contracts.Repositories
{
    /// <summary>Server-side <see cref="INoteRepository"/> over <see cref="INoteWriteRepository"/>, mapping to DTOs.</summary>
    public sealed class NoteDtoRepository(INoteWriteRepository notes) : INoteRepository
    {
        /// <inheritdoc />
        public async Task<NoteDto> AddAsync(NoteTargetKindDto kind, Guid targetId, string body, string addedBy, CancellationToken ct) =>
            (await notes.AddAsync(ToDomainKind(kind), targetId, body, addedBy, ct)).ToNoteDto();

        /// <inheritdoc />
        public async Task<IReadOnlyList<NoteDto>> ListAsync(NoteTargetKindDto kind, Guid targetId, CancellationToken ct) =>
            (await notes.ListAsync(ToDomainKind(kind), targetId, ct)).Select(n => n.ToNoteDto()).ToList();

        private static NoteTargetKind ToDomainKind(NoteTargetKindDto kind) => Enum.Parse<NoteTargetKind>(kind.ToString());
    }
}
