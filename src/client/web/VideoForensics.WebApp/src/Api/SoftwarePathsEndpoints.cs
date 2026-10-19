using VideoForensics.Client.Common.Contracts;
using VideoForensics.Core.Providers.Core;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Hosting;
using VideoForensics.WebApp.Auth;

namespace VideoForensics.WebApp.Api
{
    /// <summary>
    /// Configured locations of external tools the server runs: the ffmpeg/ffprobe bin directory and the
    /// cloudflared executable. SuperAdmin-gated like the other infrastructure settings. Writes go through
    /// StepUpEndpointFilter because a bad path silently breaks every media or tunnel operation.
    /// </summary>
    public static class SoftwarePathsEndpoints
    {
        public static void MapSoftwarePathsEndpoints(this WebApplication app)
        {
            RouteGroupBuilder group = app.MapGroup("/api/v1/software-paths").RequireAuthorization(VideoForensicsPolicies.SuperAdminLocalOrPasskey);

            _ = group.MapGet("/", (IForensicsConfiguration config) => Results.Ok(new
            {
                ffmpegBinDirectory = config.FfmpegBinDirectory,
                cloudflaredPath = config.CloudflaredPath
            }));

            _ = group.MapPost("/", SetSoftwarePathsAsync).AddEndpointFilter<StepUpEndpointFilter>();
        }

        /// <summary>
        /// Extracted from the MapPost lambda so it can be unit-tested without a full host. Empty values clear
        /// a setting; a non-empty value must exist (ffmpeg: a directory holding both ffmpeg and ffprobe).
        /// On a validation failure nothing is changed or saved.
        /// </summary>
        public static async Task<IResult> SetSoftwarePathsAsync(
            SetSoftwarePathsRequest request,
            IForensicsConfiguration config,
            IForensicsConfigurationService configService,
            ISecurityAuditLogger auditLog,
            INetworkTierResolver tierResolver,
            HttpContext context,
            CancellationToken ct)
        {
            string? ffmpegDir = string.IsNullOrWhiteSpace(request.FfmpegBinDirectory) ? null : request.FfmpegBinDirectory.Trim();
            string? cloudflaredPath = string.IsNullOrWhiteSpace(request.CloudflaredPath) ? null : request.CloudflaredPath.Trim();

            // Relative paths resolve against the server's working directory, so they could point anywhere; only absolute paths are accepted.
            if (ffmpegDir != null && (!Path.IsPathRooted(ffmpegDir) || FfmpegBinDirectoryLocator.FindBinDirectory(new[] { ffmpegDir }) == null))
            {
                return Results.BadRequest(new { error = "FFmpeg bin directory must exist and contain both ffmpeg and ffprobe." });
            }

            if (cloudflaredPath != null && (!Path.IsPathRooted(cloudflaredPath) || !File.Exists(cloudflaredPath)))
            {
                return Results.BadRequest(new { error = "cloudflared path must point to an existing file." });
            }

            string? oldFfmpegDir = config.FfmpegBinDirectory;
            string? oldCloudflaredPath = config.CloudflaredPath;

            config.FfmpegBinDirectory = ffmpegDir;
            config.CloudflaredPath = cloudflaredPath;
            try
            {
                await configService.SaveConfigurationAsync(config, ct);
            }
            catch
            {
                // The config object is shared for the life of the server: a failed save must not leave it showing values that were never persisted.
                config.FfmpegBinDirectory = oldFfmpegDir;
                config.CloudflaredPath = oldCloudflaredPath;
                throw;
            }

            string? operatorIdClaim = context.User.FindFirst(VideoForensicsClaimTypes.OperatorId)?.Value;
            await auditLog.LogAsync(SecurityAuditEventTypes.SoftwarePathsChanged,
                Guid.TryParse(operatorIdClaim, out Guid actingOperatorId) ? actingOperatorId : null,
                null, tierResolver.ResolveClientIp(context),
                $"ffmpegBinDirectory: '{oldFfmpegDir}' -> '{ffmpegDir}'; cloudflaredPath: '{oldCloudflaredPath}' -> '{cloudflaredPath}'",
                isUrgent: true, ct);

            return Results.Ok(new
            {
                ffmpegBinDirectory = config.FfmpegBinDirectory,
                cloudflaredPath = config.CloudflaredPath
            });
        }
    }

    public record SetSoftwarePathsRequest(string? FfmpegBinDirectory, string? CloudflaredPath);
}
