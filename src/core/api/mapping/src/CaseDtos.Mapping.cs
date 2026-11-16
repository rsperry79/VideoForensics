using VideoForensics.Core.Api.Contracts;
using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Core.Api.Contracts
{
    /// <summary>Extension methods for mapping case Note entities to/from CaseNoteDto.</summary>
    public static class CaseNoteDtoMapping
    {
        /// <summary>Converts a case Note entity to a CaseNoteDto. The note's target ID is the case ID.</summary>
        public static CaseNoteDto ToDto(this VideoForensics.Data.Common.Entities.Note entity)
        {
            return new CaseNoteDto(entity.Id, entity.TargetId, entity.Body, entity.AddedBy, entity.AddedAtUtc);
        }

        /// <summary>Converts a CaseNoteDto to a case Note entity (target kind Case).</summary>
        public static VideoForensics.Data.Common.Entities.Note ToDomain(this CaseNoteDto dto)
        {
            return new VideoForensics.Data.Common.Entities.Note
            {
                Id = dto.Id,
                TargetKind = VideoForensics.Data.Common.Entities.NoteTargetKind.Case,
                TargetId = dto.CaseId,
                Body = dto.Body,
                AddedBy = dto.AddedBy,
                AddedAtUtc = dto.AddedAtUtc
            };
        }
    }

    /// <summary>Extension methods for mapping ForensicCase entities to/from ForensicCaseDto and AccountCaseDto.</summary>
    public static class ForensicCaseDtoMapping
    {
        /// <summary>Converts a ForensicCase entity to an AccountCaseDto, deriving CaseType from CaseNumber.</summary>
        public static AccountCaseDto ToDto(this VideoForensics.Data.Common.Entities.ForensicCase entity)
        {
            return new AccountCaseDto(
                Id: entity.Id,
                CaseNumber: entity.CaseNumber,
                Title: entity.Title,
                Status: entity.Status.ToString(),
                CaseType: VideoForensics.Data.Common.Entities.CaseTypeClassifier.FromCaseNumber(entity.CaseNumber),
                ScopeFromUtc: entity.ScopeFromUtc,
                CreatedAtUtc: entity.CreatedAtUtc);
        }

        /// <summary>Converts an AccountCaseDto back to a partial ForensicCase entity (list fields only).</summary>
        public static VideoForensics.Data.Common.Entities.ForensicCase ToDomain(this AccountCaseDto dto)
        {
            return new VideoForensics.Data.Common.Entities.ForensicCase
            {
                Id = dto.Id,
                CaseNumber = dto.CaseNumber,
                Title = dto.Title,
                Status = Enum.Parse<VideoForensics.Data.Common.Entities.CaseStatus>(dto.Status),
                CreatedBy = string.Empty,
                CreatedAtUtc = dto.CreatedAtUtc,
                UpdatedAtUtc = dto.CreatedAtUtc,
                ScopeFromUtc = dto.ScopeFromUtc
            };
        }

        /// <summary>
        /// Converts a ForensicCase entity to a ForensicCaseDto.
        /// DeviceIds are provided separately by the caller (from GetDeviceIdsAsync).
        /// </summary>
        public static ForensicCaseDto ToDto(this VideoForensics.Data.Common.Entities.ForensicCase entity, IReadOnlyList<Guid> deviceIds)
        {
            return new ForensicCaseDto(
                Id: entity.Id,
                CaseNumber: entity.CaseNumber,
                Title: entity.Title,
                Description: entity.Description,
                Status: entity.Status.ToString(),
                LeadOperatorId: entity.LeadOperatorId,
                CreatedBy: entity.CreatedBy,
                CreatedAtUtc: entity.CreatedAtUtc,
                UpdatedAtUtc: entity.UpdatedAtUtc,
                ClosedBy: entity.ClosedBy,
                ClosedAtUtc: entity.ClosedAtUtc,
                ScopeFromUtc: entity.ScopeFromUtc,
                ScopeToUtc: entity.ScopeToUtc,
                DeviceIds: deviceIds
            );
        }

        /// <summary>
        /// Converts a ForensicCaseDto to a ForensicCase entity (without DeviceIds, which are managed separately).
        /// </summary>
        public static VideoForensics.Data.Common.Entities.ForensicCase ToDomain(this ForensicCaseDto dto)
        {
            return new VideoForensics.Data.Common.Entities.ForensicCase
            {
                Id = dto.Id,
                CaseNumber = dto.CaseNumber,
                Title = dto.Title,
                Description = dto.Description,
                Status = Enum.Parse<VideoForensics.Data.Common.Entities.CaseStatus>(dto.Status),
                LeadOperatorId = dto.LeadOperatorId,
                CreatedBy = dto.CreatedBy,
                CreatedAtUtc = dto.CreatedAtUtc,
                UpdatedAtUtc = dto.UpdatedAtUtc,
                ClosedBy = dto.ClosedBy,
                ClosedAtUtc = dto.ClosedAtUtc,
                ScopeFromUtc = dto.ScopeFromUtc,
                ScopeToUtc = dto.ScopeToUtc
            };
        }
    }

    /// <summary>Extension methods for mapping CaseItem entities to/from CaseItemDto.</summary>
    public static class CaseItemDtoMapping
    {
        /// <summary>
        /// Converts a CaseItem entity to a CaseItemDto.
        /// </summary>
        public static CaseItemDto ToDto(this VideoForensics.Data.Common.Entities.CaseItem entity)
        {
            Guid targetId = entity.Kind switch
            {
                VideoForensics.Data.Common.Entities.CaseItemKind.Event => entity.EventId ?? Guid.Empty,
                VideoForensics.Data.Common.Entities.CaseItemKind.Snapshot => entity.SnapshotCaptureId ?? Guid.Empty,
                _ => entity.MediaItemId ?? Guid.Empty,
            };

            return new CaseItemDto(
                Id: entity.Id,
                CaseId: entity.CaseId,
                Kind: entity.Kind.ToString(),
                TargetId: targetId,
                Reason: entity.Reason,
                AddedBy: entity.AddedBy,
                AddedAtUtc: entity.AddedAtUtc,
                MediaSha256AtAdd: entity.MediaSha256AtAdd,
                RemovedBy: entity.RemovedBy,
                RemovedAtUtc: entity.RemovedAtUtc,
                RemovalReason: entity.RemovalReason
            );
        }

        /// <summary>
        /// Converts a CaseItemDto to a CaseItem entity.
        /// </summary>
        public static VideoForensics.Data.Common.Entities.CaseItem ToDomain(this CaseItemDto dto)
        {
            CaseItemKind kind = Enum.Parse<VideoForensics.Data.Common.Entities.CaseItemKind>(dto.Kind);
            return new VideoForensics.Data.Common.Entities.CaseItem
            {
                Id = dto.Id,
                CaseId = dto.CaseId,
                Kind = kind,
                EventId = kind == VideoForensics.Data.Common.Entities.CaseItemKind.Event ? dto.TargetId : null,
                MediaItemId = kind == VideoForensics.Data.Common.Entities.CaseItemKind.Media ? dto.TargetId : null,
                SnapshotCaptureId = kind == VideoForensics.Data.Common.Entities.CaseItemKind.Snapshot ? dto.TargetId : null,
                Reason = dto.Reason,
                AddedBy = dto.AddedBy,
                AddedAtUtc = dto.AddedAtUtc,
                MediaSha256AtAdd = dto.MediaSha256AtAdd,
                RemovedBy = dto.RemovedBy,
                RemovedAtUtc = dto.RemovedAtUtc,
                RemovalReason = dto.RemovalReason
            };
        }
    }
}
