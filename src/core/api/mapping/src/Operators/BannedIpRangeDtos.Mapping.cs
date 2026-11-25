using VideoForensics.Core.Api.Contracts;
using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Core.Api.Contracts
{
    /// <summary>Extension methods for mapping banned IP range domain entities to DTOs.</summary>
    public static class BannedIpRangeDtoMapping
    {
        /// <summary>
        /// Well-known CreatedByOperatorId that marks a ban as written by the system auto-ban tracker, not a person.
        /// Single source of truth: FailedLoginIpTracker.SystemOperatorId and the UI's "Auto" badge both derive from this via BannedIpRangeDto.IsAutoBan.
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
