using VideoForensics.Client.WebApp.Auth;
using VideoForensics.Data.Common.Contracts;

namespace VideoForensics.Client.WebApp.Api
{
    /// <summary>Read-only security audit log viewer (plan §5.5) - Admin role may view, filterable by Operator.</summary>
    public static class SecurityAuditLogEndpoints
    {
        public static void MapSecurityAuditLogEndpoints(this WebApplication app)
        {
            _ = app.MapGet("/api/security-audit-log", async (
                Guid? operatorId,
                int? maxResults,
                ISecurityAuditLogRepository repository,
                CancellationToken ct) =>
                Results.Ok(await repository.ListAsync(operatorId, maxResults ?? 200, ct)))
                .RequireAuthorization(VideoForensicsPolicies.Admin);
        }
    }
}
