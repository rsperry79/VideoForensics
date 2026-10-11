using Microsoft.AspNetCore.Http.HttpResults;
using VideoForensics.Api.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.WebApp.Auth;

namespace VideoForensics.WebApp.Api
{
    /// <summary>
    /// Read-only admin endpoint listing active banned IP ranges (manual and system auto-bans).
    /// Unban and create are not exposed here. Requires SuperAdmin+Local, same as the lockout policy routes.
    /// </summary>
    public static class BannedIpsEndpoints
    {
        public static void MapBannedIpsEndpoints(this WebApplication app)
        {
            RouteGroupBuilder group = app.MapGroup("/api/v1/banned-ips").RequireAuthorization(VideoForensicsPolicies.SuperAdminLocal);

            _ = group.MapGet("/", ListAsync).WithName("ListBannedIps");
        }

        internal static async Task<Ok<IReadOnlyList<BannedIpRangeDto>>> ListAsync(
            IBannedIpRangeRepository repository,
            CancellationToken ct)
        {
            var now = DateTime.UtcNow;
            IReadOnlyList<BannedIpRangeDto> bans = (await repository.GetActiveAsync(ct))
                .Where(range => range.ExpiresAtUtc is null || range.ExpiresAtUtc > now)
                .Select(range => range.ToDto())
                .ToList();
            return TypedResults.Ok(bans);
        }
    }
}
