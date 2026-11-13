using VideoForensics.Core.Api.Contracts;
using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Core.Api.Contracts
{
    /// <summary>Extension methods for mapping two-factor policy domain types to/from DTOs.</summary>
    public static class TwoFactorPolicyDtoMapping
    {
        /// <summary>
        /// Converts a domain TwoFactorRoleRequirement to a TwoFactorRoleRequirementDto.
        /// </summary>
        public static TwoFactorRoleRequirementDto ToDto(this TwoFactorRoleRequirement requirement)
        {
            return new TwoFactorRoleRequirementDto(
                Role: Enum.Parse<OperatorRoleDto>(requirement.Role.ToString()),
                RequireTwoFactor: requirement.RequireTwoFactor,
                UpdatedAtUtc: requirement.UpdatedAtUtc
            );
        }

        /// <summary>
        /// Converts a TwoFactorRoleRequirementDto to a domain TwoFactorRoleRequirement.
        /// </summary>
        public static TwoFactorRoleRequirement ToDomain(this TwoFactorRoleRequirementDto dto, Guid? operatorId = null)
        {
            return new TwoFactorRoleRequirement
            {
                Id = Guid.NewGuid(),
                Role = Enum.Parse<VideoForensics.Data.Common.Entities.OperatorRole>(dto.Role.ToString()),
                RequireTwoFactor = dto.RequireTwoFactor,
                UpdatedAtUtc = DateTime.UtcNow,
                UpdatedByOperatorId = operatorId
            };
        }
    }
}
