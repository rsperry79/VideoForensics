using Microsoft.AspNetCore.Authorization;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Hosting;

namespace VideoForensics.WebApp.Auth
{
    /// <summary>Requires the caller's OperatorRole to be at least <see cref="MinimumRole"/> (roles are ordered, so this is a numeric >= comparison - plan §5.10).</summary>
    public class MinimumRoleRequirement : IAuthorizationRequirement
    {
        public MinimumRoleRequirement(OperatorRole minimumRole) => MinimumRole = minimumRole;
        public OperatorRole MinimumRole { get; }
    }

    public class MinimumRoleHandler : AuthorizationHandler<MinimumRoleRequirement>
    {
        protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, MinimumRoleRequirement requirement)
        {
            var roleClaim = context.User.FindFirst(VideoForensicsClaimTypes.Role)?.Value;
            if (roleClaim != null && Enum.TryParse<OperatorRole>(roleClaim, out var role) && role >= requirement.MinimumRole)
            {
                context.Succeed(requirement);
            }

            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// ESCALATION-FLAGGED (plan §10): SuperAdmin-gated actions additionally require the request to
    /// have arrived via the Local network tier (physical presence at the server), regardless of
    /// role or which tiers are otherwise enabled - a SEPARATE, explicit check, never satisfied by
    /// role alone and never satisfied by merely hiding a button in the UI (§5.10). Deliberately its
    /// own requirement/handler rather than folded into MinimumRoleRequirement, so it's structurally
    /// impossible to satisfy this policy with role alone.
    /// </summary>
    public class RequireLocalTierRequirement : IAuthorizationRequirement
    {
    }

    public class RequireLocalTierHandler : AuthorizationHandler<RequireLocalTierRequirement>
    {
        protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, RequireLocalTierRequirement requirement)
        {
            var tierClaim = context.User.FindFirst(VideoForensicsClaimTypes.NetworkTier)?.Value;
            if (tierClaim != null && Enum.TryParse<NetworkTier>(tierClaim, out var tier) && tier == NetworkTier.Local)
            {
                context.Succeed(requirement);
            }

            return Task.CompletedTask;
        }
    }

    public static class VideoForensicsPolicies
    {
        public const string ReadOnly = "VF.ReadOnly";
        public const string Review = "VF.Review";
        public const string Admin = "VF.Admin";
        public const string SuperAdmin = "VF.SuperAdmin";

        /// <summary>SuperAdmin role AND the Local network tier - see RequireLocalTierRequirement's doc comment for why this is a separate, non-bypassable check.</summary>
        public const string SuperAdminLocal = "VF.SuperAdminLocal";

        public static void AddVideoForensicsPolicies(this AuthorizationOptions options)
        {
            options.AddPolicy(ReadOnly, p => p.Requirements.Add(new MinimumRoleRequirement(OperatorRole.ReadOnly)));
            options.AddPolicy(Review, p => p.Requirements.Add(new MinimumRoleRequirement(OperatorRole.Review)));
            options.AddPolicy(Admin, p => p.Requirements.Add(new MinimumRoleRequirement(OperatorRole.Admin)));
            options.AddPolicy(SuperAdmin, p => p.Requirements.Add(new MinimumRoleRequirement(OperatorRole.SuperAdmin)));
            options.AddPolicy(SuperAdminLocal, p =>
            {
                p.Requirements.Add(new MinimumRoleRequirement(OperatorRole.SuperAdmin));
                p.Requirements.Add(new RequireLocalTierRequirement());
            });
        }
    }
}
