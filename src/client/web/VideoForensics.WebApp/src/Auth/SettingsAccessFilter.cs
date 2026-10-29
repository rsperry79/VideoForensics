using System.Security.Claims;

using Microsoft.AspNetCore.Http;

using VideoForensics.Client.WebApp.Services;
using VideoForensics.Client.Ui.Shared.Layout;
using VideoForensics.Core.Api.Contracts;
using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Client.WebApp.Auth
{
    /// <summary>
    /// Applies the stored level of one restricted settings page to a route. The route's own policy already requires Admin,
    /// so this only has to narrow the caller further when the setting is SuperAdmin (the default). It re-reads the setting
    /// on each request, so a change on the Permissions page applies without a restart.
    /// </summary>
    public static class SettingsAccessFilter
    {
        /// <summary>Adds the check for <paramref name="area"/> to the route.</summary>
        public static TBuilder RequireSettingsAccess<TBuilder>(this TBuilder builder, RestrictedSettings area)
            where TBuilder : IEndpointConventionBuilder
        {
            return builder.AddEndpointFilterFactory((_, next) => async invocation =>
            {
                HttpContext http = invocation.HttpContext;
                ISettingsAccessPolicy policy = http.RequestServices.GetRequiredService<ISettingsAccessPolicy>();
                AccessPolicy required = await policy.GetRequiredPolicyAsync(area, http.RequestAborted);

                // A caller with no readable role is denied rather than treated as Admin.
                return RoleOf(http.User) is { } role && ProviderAccountEditRules.IsAllowed(required, role)
                    ? await next(invocation)
                    : Results.StatusCode(StatusCodes.Status403Forbidden);
            });
        }

        private static OperatorRole? RoleOf(ClaimsPrincipal user)
        {
            return Enum.TryParse(user.FindFirst(VideoForensicsClaimTypes.Role)?.Value, out OperatorRole role) ? role : null;
        }
    }
}
