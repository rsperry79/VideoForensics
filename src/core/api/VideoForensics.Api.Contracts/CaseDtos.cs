namespace VideoForensics.Api.Contracts
{
    /// <summary>
    /// Data transfer object for a forensic case.
    /// </summary>
    /// <param name="Id">Unique identifier for the case.</param>
    /// <param name="CaseNumber">Unique case number.</param>
    /// <param name="Title">Case title.</param>
    /// <param name="Description">Optional case description.</param>
    /// <param name="Status">Current status (Open or Closed).</param>
    /// <param name="LeadOperatorId">ID of the lead operator assigned to this case.</param>
    /// <param name="CreatedBy">Username who created the case.</param>
    /// <param name="CreatedAtUtc">Timestamp when the case was created.</param>
    /// <param name="UpdatedAtUtc">Timestamp of the most recent update.</param>
    /// <param name="ClosedBy">Username who closed the case, if closed.</param>
    /// <param name="ClosedAtUtc">Timestamp when the case was closed, if closed.</param>
    /// <param name="ScopeFromUtc">Start of the time window (inclusive, UTC).</param>
    /// <param name="ScopeToUtc">End of the time window (exclusive, UTC).</param>
    /// <param name="DeviceIds">IDs of devices in the case scope.</param>
    public record ForensicCaseDto(
        Guid Id,
        string CaseNumber,
        string Title,
        string? Description,
        string Status,
        Guid? LeadOperatorId,
        string CreatedBy,
        DateTime CreatedAtUtc,
        DateTime UpdatedAtUtc,
        string? ClosedBy,
        DateTime? ClosedAtUtc,
        DateTime? ScopeFromUtc,
        DateTime? ScopeToUtc,
        IReadOnlyList<Guid> DeviceIds
    );

    /// <summary>
    /// Data transfer object for an evidence item pinned to a forensic case.
    /// </summary>
    /// <param name="Id">Unique identifier for the case item.</param>
    /// <param name="CaseId">ID of the case this item is pinned to.</param>
    /// <param name="Kind">Type of evidence (Event or Media).</param>
    /// <param name="TargetId">ID of the event or media item.</param>
    /// <param name="Reason">Reason this item was pinned to the case.</param>
    /// <param name="AddedBy">Username who pinned this item.</param>
    /// <param name="AddedAtUtc">Timestamp when the item was pinned.</param>
    /// <param name="MediaSha256AtAdd">Snapshot of media SHA256 at add time (for integrity verification).</param>
    /// <param name="RemovedBy">Username who removed this item, if soft-removed.</param>
    /// <param name="RemovedAtUtc">Timestamp when the item was soft-removed.</param>
    /// <param name="RemovalReason">Reason for removal, if soft-removed.</param>
    public record CaseItemDto(
        Guid Id,
        Guid CaseId,
        string Kind,
        Guid TargetId,
        string Reason,
        string AddedBy,
        DateTime AddedAtUtc,
        string? MediaSha256AtAdd,
        string? RemovedBy,
        DateTime? RemovedAtUtc,
        string? RemovalReason
    );

    /// <summary>
    /// Request DTO for creating a forensic case.
    /// </summary>
    /// <param name="CaseNumber">Unique case number (required, max 64 chars).</param>
    /// <param name="Title">Case title (required, max 256 chars).</param>
    /// <param name="Description">Optional case description (max 4000 chars).</param>
    /// <param name="LeadOperatorId">Optional ID of the lead operator.</param>
    /// <param name="ScopeFromUtc">Optional start of the time window (inclusive, UTC).</param>
    /// <param name="ScopeToUtc">Optional end of the time window (exclusive, UTC).</param>
    /// <param name="DeviceIds">IDs of devices in the case scope.</param>
    public record CreateCaseRequestDto(
        string CaseNumber,
        string Title,
        string? Description,
        Guid? LeadOperatorId,
        DateTime? ScopeFromUtc,
        DateTime? ScopeToUtc,
        IReadOnlyCollection<Guid> DeviceIds
    );

    /// <summary>
    /// Request DTO for updating case details (title, description, lead operator).
    /// </summary>
    /// <param name="Title">New case title (required, max 256 chars).</param>
    /// <param name="Description">Optional new case description (max 4000 chars).</param>
    /// <param name="LeadOperatorId">Optional new lead operator ID.</param>
    public record UpdateCaseDetailsRequestDto(
        string Title,
        string? Description,
        Guid? LeadOperatorId
    );

    /// <summary>
    /// Request DTO for updating case scope (time window and device set).
    /// </summary>
    /// <param name="ScopeFromUtc">Optional start of the time window (inclusive, UTC).</param>
    /// <param name="ScopeToUtc">Optional end of the time window (exclusive, UTC).</param>
    /// <param name="DeviceIds">IDs of devices in the case scope.</param>
    public record SetCaseScopeRequestDto(
        DateTime? ScopeFromUtc,
        DateTime? ScopeToUtc,
        IReadOnlyCollection<Guid> DeviceIds
    );

    /// <summary>
    /// Request DTO for adding an evidence item to a case.
    /// </summary>
    /// <param name="Kind">Type of evidence (Event or Media).</param>
    /// <param name="TargetId">ID of the event or media item to pin.</param>
    /// <param name="Reason">Reason for pinning this item (required, max 1024 chars).</param>
    public record AddCaseItemRequestDto(
        string Kind,
        Guid TargetId,
        string Reason
    );

    /// <summary>
    /// Request DTO for removing an evidence item from a case.
    /// </summary>
    /// <param name="Reason">Reason for removal (required, max 1024 chars).</param>
    public record RemoveCaseItemRequestDto(
        string Reason
    );

    /// <summary>Extension methods for mapping ForensicCase entities to/from ForensicCaseDto.</summary>
    public static class ForensicCaseDtoMapping
    {
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
            Guid targetId = entity.Kind == VideoForensics.Data.Common.Entities.CaseItemKind.Event
                ? entity.EventId ?? Guid.Empty
                : entity.MediaItemId ?? Guid.Empty;

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
            var kind = Enum.Parse<VideoForensics.Data.Common.Entities.CaseItemKind>(dto.Kind);
            return new VideoForensics.Data.Common.Entities.CaseItem
            {
                Id = dto.Id,
                CaseId = dto.CaseId,
                Kind = kind,
                EventId = kind == VideoForensics.Data.Common.Entities.CaseItemKind.Event ? dto.TargetId : null,
                MediaItemId = kind == VideoForensics.Data.Common.Entities.CaseItemKind.Media ? dto.TargetId : null,
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
