using Microsoft.AspNetCore.Authorization;

using System.Security.Claims;

using VideoForensics.Client.WebApp.Contracts;

namespace VideoForensics.Client.WebApp.Mcp
{
    /// <summary>
    /// Evaluates the server's own authorization policies against the caller of the current MCP request. A request
    /// with no caller identity is denied.
    /// </summary>
    public sealed class McpCallerAuthorizer(IAuthorizationService authorization, IHttpContextAccessor httpContextAccessor) : IMcpCallerAuthorizer
    {
        public async Task<bool> IsAllowedAsync(string policyName)
        {
            ClaimsPrincipal? user = httpContextAccessor.HttpContext?.User;
            return user is not null && (await authorization.AuthorizeAsync(user, null, policyName)).Succeeded;
        }

        public async Task RequireAsync(string policyName)
        {
            if (!await IsAllowedAsync(policyName))
            {
                throw new UnauthorizedAccessException($"This MCP tool requires {policyName}.");
            }
        }
    }
}
