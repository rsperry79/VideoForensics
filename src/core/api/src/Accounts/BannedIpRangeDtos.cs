namespace VideoForensics.Core.Api.Contracts
{
    /// <summary>
    /// Data transfer object for an active banned IP range (manual or system auto-ban).
    /// </summary>
    /// <param name="Id">Unique identifier of the ban entry.</param>
    /// <param name="CidrRange">CIDR notation range that is blocked, e.g. "203.0.113.0/24".</param>
    /// <param name="Reason">Optional human-readable reason for the ban.</param>
    /// <param name="CreatedAtUtc">Timestamp when the ban was created, in UTC.</param>
    /// <param name="ExpiresAtUtc">Expiration time in UTC; null means the ban is permanent.</param>
    /// <param name="CreatedByOperatorId">Id of the operator who created the ban, or the system auto-ban id (BannedIpRangeDtoMapping.SystemAutoBanOperatorId) for system auto-bans.</param>
    /// <param name="IsAutoBan">True when the ban was created by the system auto-ban tracker rather than an operator.</param>
    public record BannedIpRangeDto(
        Guid Id,
        string CidrRange,
        string? Reason,
        DateTime CreatedAtUtc,
        DateTime? ExpiresAtUtc,
        Guid CreatedByOperatorId,
        bool IsAutoBan
    );
}
