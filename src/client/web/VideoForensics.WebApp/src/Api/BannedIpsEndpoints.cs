using System.Net;
using System.Net.Sockets;
using Microsoft.AspNetCore.Http.HttpResults;
using VideoForensics.Core.Api.Contracts;
using VideoForensics.Client.Common.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Hosting;
using VideoForensics.WebApp.Auth;
using VideoForensics.WebApp.Services;

namespace VideoForensics.WebApp.Api
{
    /// <summary>
    /// Admin endpoints for active banned IP ranges (manual and system auto-bans): list and unban.
    /// Create is not exposed here. Requires SuperAdmin (Local for password sign-in; passkey from any tier), same as the lockout policy routes.
    /// </summary>
    public static class BannedIpsEndpoints
    {
        public static void MapBannedIpsEndpoints(this WebApplication app)
        {
            RouteGroupBuilder group = app.MapGroup("/api/v1/banned-ips").RequireAuthorization(VideoForensicsPolicies.SuperAdminLocalOrPasskey);

            _ = group.MapGet("/", ListAsync).WithName("ListBannedIps");
            _ = group.MapDelete("/{id:guid}", UnbanAsync).WithName("UnbanBannedIp").AddEndpointFilter<StepUpEndpointFilter>();
        }

        /// <summary>
        /// Removes an active ban, audits it, and clears the tracker counter for a single-host ban so the
        /// address is not re-banned on its next lockout because of counts from before the unban.
        /// </summary>
        internal static async Task<Results<NoContent, NotFound>> UnbanAsync(
            Guid id,
            IBannedIpRangeRepository repository,
            ISecurityAuditLogger auditLog,
            IFailedLoginIpTracker failedLoginIpTracker,
            INetworkTierResolver tierResolver,
            HttpContext context,
            CancellationToken ct)
        {
            // RemoveAsync silently ignores an unknown id, so existence is checked against the same active
            // set the list shows. An expired ban is not in that set and answers 404.
            var now = DateTime.UtcNow;
            BannedIpRange? ban = (await repository.GetActiveAsync(ct))
                .FirstOrDefault(range => range.Id == id && (range.ExpiresAtUtc is null || range.ExpiresAtUtc > now));
            if (ban is null)
            {
                return TypedResults.NotFound();
            }

            await repository.RemoveAsync(id, ct);

            // Details carry the range and whether it was a system auto-ban, never the creating account's GUID.
            bool wasAutoBan = ban.CreatedByOperatorId == BannedIpRangeDtoMapping.SystemAutoBanOperatorId;
            string? operatorIdClaim = context.User.FindFirst(VideoForensicsClaimTypes.OperatorId)?.Value;
            await auditLog.LogAsync(SecurityAuditEventTypes.BannedIpRangeRemoved,
                Guid.TryParse(operatorIdClaim, out Guid actingOperatorId) ? actingOperatorId : null,
                null, tierResolver.ResolveClientIp(context),
                $"Unbanned {ban.CidrRange}{(wasAutoBan ? " (system auto-ban)" : string.Empty)}",
                isUrgent: true, ct);

            if (TryGetSingleHost(ban.CidrRange, out string hostAddress))
            {
                await failedLoginIpTracker.ClearAsync(hostAddress, ct);
            }

            return TypedResults.NoContent();
        }

        /// <summary>
        /// True only for an exact host prefix (/32 IPv4, /128 IPv6). A wider range covers many addresses
        /// whose counters the tracker must keep, so it is never cleared.
        /// </summary>
        private static bool TryGetSingleHost(string cidr, out string hostAddress)
        {
            hostAddress = string.Empty;
            string[] parts = cidr.Split('/');
            if (parts.Length != 2 || !IPAddress.TryParse(parts[0], out IPAddress? address))
            {
                return false;
            }

            int hostPrefix = address.AddressFamily == AddressFamily.InterNetworkV6 ? 128 : 32;
            if (!int.TryParse(parts[1], out int prefix) || prefix != hostPrefix)
            {
                return false;
            }

            hostAddress = parts[0];
            return true;
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
