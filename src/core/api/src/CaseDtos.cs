namespace VideoForensics.Core.Api.Contracts
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
    /// <param name="CaseNumber">Optional unique case number (max 64 chars). If not provided, the server generates one automatically.</param>
    /// <param name="Title">Case title (required, max 256 chars).</param>
    /// <param name="Description">Optional case description (max 4000 chars).</param>
    /// <param name="LeadOperatorId">Optional ID of the lead operator.</param>
    /// <param name="ScopeFromUtc">Optional start of the time window (inclusive, UTC).</param>
    /// <param name="ScopeToUtc">Optional end of the time window (exclusive, UTC).</param>
    /// <param name="DeviceIds">IDs of devices in the case scope.</param>
    public record CreateCaseRequestDto(
        string? CaseNumber,
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

    /// <summary>
    /// Row for the account details page's case list. Case type is derived server-side from the case number prefix
    /// so clients never parse case numbers.
    /// </summary>
    /// <param name="Id">Case ID, used for navigation.</param>
    /// <param name="CaseNumber">Unique case number.</param>
    /// <param name="Title">Case title.</param>
    /// <param name="Status">Current status (Open or Closed).</param>
    /// <param name="CaseType">Detected, Suspected, Manual, or Other.</param>
    /// <param name="ScopeFromUtc">Date of event (start of the scope window, UTC), null when unset.</param>
    /// <param name="CreatedAtUtc">Timestamp when the case was created.</param>
    public record AccountCaseDto(
        Guid Id,
        string CaseNumber,
        string Title,
        string Status,
        string CaseType,
        DateTime? ScopeFromUtc,
        DateTime CreatedAtUtc
    );

    /// <summary>
    /// Data transfer object for an append-only note on a forensic case.
    /// </summary>
    /// <param name="Id">Unique identifier for the note.</param>
    /// <param name="CaseId">ID of the case the note belongs to.</param>
    /// <param name="Body">Note text, trimmed at the ends (max 4000 chars).</param>
    /// <param name="AddedBy">Operator ID that wrote the note.</param>
    /// <param name="AddedAtUtc">Timestamp when the note was written (UTC).</param>
    public record CaseNoteDto(
        Guid Id,
        Guid CaseId,
        string Body,
        string AddedBy,
        DateTime AddedAtUtc
    );

    /// <summary>
    /// Request DTO for adding a note to a forensic case. The author is the signed-in operator, taken server-side.
    /// </summary>
    /// <param name="Body">Note text (required, max 4000 chars after trimming).</param>
    public record AddCaseNoteRequest(
        string Body
    );
}
