using System.Security.Claims;

using Microsoft.AspNetCore.Http;

using VideoForensics.Client.WebApp.Services;
using VideoForensics.Client.Ui.Shared.Layout;
using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Client.WebApp.Auth
{
    /// <summary>
    /// Applies the stored email-settings level to a route. The route's own policy already requires Admin, so this only
    /// has to narrow the caller further when the setting is SuperAdmin (the default). It re-reads the setting on each
    /// request, so a change on the Permissions page applies without a restart.
    /// </summary>
    public sealed class EmailSettingsFilter(IEmailSettingsPolicy policy) : IEndpointFilter
    {
        public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
        {
            AccessPolicy required = await policy.GetRequiredPolicyAsync(context.HttpContext.RequestAborted);
            OperatorRole? role = RoleOf(context.HttpContext.User);

            // A caller with no readable role is denied rather than treated as Admin.
            if (role is not { } operatorRole || !ProviderAccountEditRules.IsAllowed(required, operatorRole))
            {
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            return await next(context);
        }

        private static OperatorRole? RoleOf(ClaimsPrincipal user)
        {
            return Enum.TryParse(user.FindFirst(VideoForensicsClaimTypes.Role)?.Value, out OperatorRole role) ? role : null;
        }
    }
}
