namespace VideoForensics.Core.Api.Contracts
{
    /// <summary>
    /// Wire DTO for one media review flag: the review state of a detected anomaly. Enums travel as their names
    /// (<c>FlagType</c>, <c>Severity</c>, <c>Status</c>) so a new value does not break an older client's binding.
    /// </summary>
    public sealed record MediaFlagDto(
        Guid Id,
        string FlagType,
        Guid DeviceId,
        Guid? MediaItemId,
        DateTime WindowStartUtc,
        DateTime WindowEndUtc,
        DateTime RaisedAtUtc,
        string Severity,
        string Status,
        string? ReviewedBy,
        DateTime? ReviewedAtUtc,
        string? ReviewNote,
        Guid? NoticeId,
        int RowVersion,
        string? EventType,
        double? DeviationSigma);

    /// <summary>One page of media flags plus the total number matching the filter.</summary>
    public sealed record MediaFlagPageDto(IReadOnlyList<MediaFlagDto> Items, int TotalCount);

    /// <summary>
    /// Filter for flag lists, counts and neighbour lookups, with enums as names. Null or empty collections mean "any".
    /// <paramref name="FromUtc"/> and <paramref name="ToUtc"/> bound the flag window start, inclusive.
    /// </summary>
    public sealed record MediaFlagFilterDto(
        IReadOnlyList<string>? Types,
        IReadOnlyList<string>? Statuses,
        IReadOnlyList<Guid>? DeviceIds,
        DateTime? FromUtc,
        DateTime? ToUtc);

    /// <summary>
    /// Body of POST media-flags/{id}/status. The operator is never sent: the server takes it from the signed-in token.
    /// <paramref name="ExpectedStatus"/> is the status the caller read; the server refuses the change if it has moved on.
    /// </summary>
    public sealed record SetFlagStatusRequestDto(string ExpectedStatus, string NewStatus, string? Reason);

    /// <summary>Body of POST media-flags/{id}/reopen. The operator is taken from the token, as for status changes.</summary>
    public sealed record ReopenFlagRequestDto(string Reason);

    /// <summary>
    /// Body of the 409 response when a flag's status changed since the caller read it: what the caller expected, what it is
    /// now, and who reviewed it, so the client can say "already reviewed by X".
    /// </summary>
    public sealed record FlagStatusConflictDto(string ExpectedStatus, string ActualStatus, string? ReviewedBy, DateTime? ReviewedAtUtc);
}
