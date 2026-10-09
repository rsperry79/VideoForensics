using VideoForensics.Api.Contracts;
using VideoForensics.Client.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Hosting;
using VideoForensics.WebApp.Auth;

namespace VideoForensics.WebApp.Api
{
    /// <summary>
    /// The update release channel (Stable or Testing). Readable by any Admin so the update page
    /// can show which channel is active, but writable only by a SuperAdmin on the Local tier - the
    /// same gate as every other update/infrastructure-exposure setting. A change is audited as
    /// urgent because it alters which builds the server will install.
    /// </summary>
    public static class ReleaseChannelEndpoints
    {
        public static void MapReleaseChannelEndpoints(this WebApplication app)
        {
            RouteGroupBuilder group = app.MapGroup("/api/v1/update-settings/release-channel");

            _ = group.MapGet("/", (IForensicsConfiguration config) => Results.Ok(GetReleaseChannel(config)))
                .RequireAuthorization(VideoForensicsPolicies.Admin);

            _ = group.MapPut("/", SetReleaseChannelAsync)
                .RequireAuthorization(VideoForensicsPolicies.SuperAdminLocal);
        }

        /// <summary>
        /// Extracted from the MapGet lambda so the mapping from the enum to its wire name is directly unit-testable.
        /// </summary>
        public static ReleaseChannelDto GetReleaseChannel(IForensicsConfiguration config)
        {
            return new ReleaseChannelDto(config.ReleaseChannel.ToString());
        }

        /// <summary>
        /// Extracted from the MapPut lambda so it's directly unit-testable (see
        /// ReleaseChannelEndpointsTests.cs) without spinning up a full WebApplicationFactory host.
        /// </summary>
        public static async Task<IResult> SetReleaseChannelAsync(
            SetReleaseChannelRequest request,
            IForensicsConfiguration config,
            IForensicsConfigurationService configService,
            ISecurityAuditLogger auditLog,
            INetworkTierResolver tierResolver,
            HttpContext context,
            CancellationToken ct)
        {
            // Enum.TryParse accepts numeric strings ("5") and out-of-range values even with a valid
            // name check, so IsDefined is required to reject anything that isn't a real channel.
            if (!Enum.TryParse(request.Channel, ignoreCase: true, out UpdateReleaseChannel requested)
                || !Enum.IsDefined(requested))
            {
                return Results.BadRequest(new { error = $"Unknown release channel '{request.Channel}'. Expected 'Stable' or 'Testing'." });
            }

            UpdateReleaseChannel oldChannel = config.ReleaseChannel;
            config.ReleaseChannel = requested;
            await configService.SaveConfigurationAsync(config, ct);

            string? operatorIdClaim = context.User.FindFirst(VideoForensicsClaimTypes.OperatorId)?.Value;
            await auditLog.LogAsync(SecurityAuditEventTypes.ReleaseChannelChanged,
                Guid.TryParse(operatorIdClaim, out Guid actingOperatorId) ? actingOperatorId : null,
                null, tierResolver.ResolveClientIp(context), $"Release channel changed from '{oldChannel}' to '{requested}'", isUrgent: true, ct);

            return Results.Ok(new ReleaseChannelDto(requested.ToString()));
        }
    }
}
