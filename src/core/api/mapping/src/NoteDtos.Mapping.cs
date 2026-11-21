using VideoForensics.Core.Api.Contracts;
using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Core.Api.Contracts
{
    /// <summary>Extension methods for mapping any-kind Note entities to NoteDto.</summary>
    public static class NoteDtoMapping
    {
        /// <summary>Converts a Note entity of any target kind to a NoteDto.</summary>
        /// <param name="note">The note entity to convert.</param>
        public static NoteDto ToNoteDto(this Note note)
        {
            return new NoteDto(note.Id, Enum.Parse<NoteTargetKindDto>(note.TargetKind.ToString()), note.TargetId, note.Body, note.AddedBy, note.AddedAtUtc);
        }

        /// <summary>Converts a NoteDto to a Note entity of the DTO's target kind.</summary>
        /// <param name="dto">The note DTO to convert.</param>
        public static Note ToDomain(this NoteDto dto)
        {
            return new Note
            {
                Id = dto.Id,
                TargetKind = Enum.Parse<VideoForensics.Data.Common.Entities.NoteTargetKind>(dto.TargetKind.ToString()),
                TargetId = dto.TargetId,
                Body = dto.Body,
                AddedBy = dto.AddedBy,
                AddedAtUtc = dto.AddedAtUtc,
            };
        }
    }
}
