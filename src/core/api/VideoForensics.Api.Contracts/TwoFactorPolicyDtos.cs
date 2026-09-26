using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Api.Contracts
{
    /// <summary>
    /// Data transfer object for a two-factor authentication requirement for a specific operator role.
    /// </summary>
    /// <param name="Role">The operator role (ReadOnly, Review, Admin, SuperAdmin).</param>
    /// <param name="RequireTwoFactor">Whether two-factor authentication is required for this role.</param>
    /// <param name="UpdatedAtUtc">Timestamp of the last update to this requirement, in UTC.</param>
    public record TwoFactorRoleRequirementDto(
        OperatorRole Role,
        bool RequireTwoFactor,
        DateTime UpdatedAtUtc
    );

    /// <summary>
    /// Data transfer object for a request to update two-factor requirement for a specific role.
    /// </summary>
    /// <param name="RequireTwoFactor">Whether two-factor authentication should be required for this role.</param>
    public record UpdateTwoFactorRoleRequirementRequest(
        bool RequireTwoFactor
    );

    /// <summary>
    /// Data transfer object for a request to update two-factor requirement override for a specific operator.
    /// </summary>
    /// <param name="Override">The override setting (Inherit, Required, or NotRequired).</param>
    public record UpdateOperatorTwoFactorOverrideRequest(
        TwoFactorRequirementOverride Override
    );

    /// <summary>Extension methods for mapping two-factor policy domain types to/from DTOs.</summary>
    public static class TwoFactorPolicyDtoMapping
    {
        /// <summary>
        /// Converts a domain TwoFactorRoleRequirement to a TwoFactorRoleRequirementDto.
        /// </summary>
        public static TwoFactorRoleRequirementDto ToDto(this TwoFactorRoleRequirement requirement)
        {
            return new TwoFactorRoleRequirementDto(
                Role: requirement.Role,
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
                Role = dto.Role,
                RequireTwoFactor = dto.RequireTwoFactor,
                UpdatedAtUtc = DateTime.UtcNow,
                UpdatedByOperatorId = operatorId
            };
        }
    }
}
