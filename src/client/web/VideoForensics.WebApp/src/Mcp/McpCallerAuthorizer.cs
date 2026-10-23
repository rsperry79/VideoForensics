using System.Security.Claims;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;

using VideoForensics.WebApp.Contracts;

namespace VideoForensics.WebApp.Mcp
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
            if (user is null)
            {
                return false;
            }

            return (await authorization.AuthorizeAsync(user, null, policyName)).Succeeded;
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
