namespace VideoForensics.Core.Api.Contracts
{
    /// <summary>
    /// Data transfer object for a two-factor authentication requirement for a specific operator role.
    /// </summary>
    /// <param name="Role">The operator role (ReadOnly, Review, Admin, SuperAdmin).</param>
    /// <param name="RequireTwoFactor">Whether two-factor authentication is required for this role.</param>
    /// <param name="UpdatedAtUtc">Timestamp of the last update to this requirement, in UTC.</param>
    public record TwoFactorRoleRequirementDto(
        OperatorRoleDto Role,
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
        TwoFactorRequirementOverrideDto Override
    );
}
