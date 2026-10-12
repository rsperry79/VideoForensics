using VideoForensics.Data.Common.Entities;

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
    /// <param name="CreatedByOperatorId">Id of the operator who created the ban, or <see cref="BannedIpRangeDtoMapping.SystemAutoBanOperatorId"/> for system auto-bans.</param>
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

    /// <summary>Extension methods for mapping banned IP range domain entities to DTOs.</summary>
    public static class BannedIpRangeDtoMapping
    {
        /// <summary>
        /// Well-known CreatedByOperatorId that marks a ban as written by the system auto-ban tracker, not a person.
        /// Single source of truth: FailedLoginIpTracker.SystemOperatorId and the UI's "Auto" badge both derive from this via <see cref="BannedIpRangeDto.IsAutoBan"/>.
        /// </summary>
        public static readonly Guid SystemAutoBanOperatorId = new("00000000-0000-0000-0000-00000000f2b1");

        /// <summary>
        /// Converts a domain BannedIpRange to a BannedIpRangeDto.
        /// </summary>
        public static BannedIpRangeDto ToDto(this BannedIpRange range)
        {
            return new BannedIpRangeDto(
                Id: range.Id,
                CidrRange: range.CidrRange,
                Reason: range.Reason,
                CreatedAtUtc: range.CreatedAtUtc,
                ExpiresAtUtc: range.ExpiresAtUtc,
                CreatedByOperatorId: range.CreatedByOperatorId,
                IsAutoBan: range.CreatedByOperatorId == SystemAutoBanOperatorId
            );
        }
    }
}
