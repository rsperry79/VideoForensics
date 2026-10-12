using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;

namespace VideoForensics.WebApp.Api
{
    /// <summary>
    /// Helper for resolving the effective two-factor authentication requirement for an operator,
    /// respecting per-operator overrides and role-level defaults.
    /// </summary>
    public static class TwoFactorPolicyResolver
    {
        /// <summary>
        /// Determines whether an operator must use two-factor authentication based on their override
        /// setting and their role's default policy.
        /// </summary>
        /// <param name="op">The operator to check.</param>
        /// <param name="roleRequirements">Repository for role-level 2FA defaults.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>
        /// True if the operator must use two-factor authentication, false otherwise.
        /// </returns>
        public static async Task<bool> ResolveTwoFactorRequirementAsync(
            Operator op,
            ITwoFactorRoleRequirementRepository roleRequirements,
            CancellationToken ct)
        {
            return op.TwoFactorRequirementOverride switch
            {
                TwoFactorRequirementOverride.Required => true,
                TwoFactorRequirementOverride.NotRequired => false,
                _ => await roleRequirements.GetRequirementForRoleAsync(op.Role, ct)
            };
        }
    }
}
