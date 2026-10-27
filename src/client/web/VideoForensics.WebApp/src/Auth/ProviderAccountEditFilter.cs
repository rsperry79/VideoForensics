using System.Security.Claims;

using Microsoft.AspNetCore.Http;

using VideoForensics.Client.WebApp.Services;
using VideoForensics.Client.Ui.Shared.Layout;
using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Client.WebApp.Auth
{
    /// <summary>
    /// Applies the stored provider-account edit setting to a route. The route's own policy already requires Admin, so this
    /// only has to narrow the caller further when the setting is SuperAdmin. It runs per request, so the setting is re-read
    /// each time rather than baked into the route at startup.
    /// </summary>
    public sealed class ProviderAccountEditFilter(IProviderAccountEditPolicy policy) : IEndpointFilter
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
