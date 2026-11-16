using Microsoft.AspNetCore.Mvc;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Client.Hosting;
using VideoForensics.Client.WebApp.Api;
using VideoForensics.Client.WebApp.Auth;
using VideoForensics.Core.Api.Contracts;
using VideoForensics.Core.Helpers.Platform;
using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Client.WebApp.Api
{
    /// <summary>
    /// Handles database backup export and import. Per the plan's auth-default rule,
    /// EVERY endpoint here requires paired-device authentication (RequireAuthorization()) - there is
    /// no general exemption for any backup operation. Both export and import are exposure-widening
    /// operations that additionally require RequireAuthorization(VideoForensicsPolicies.SuperAdminLocalOrPasskey),
    /// StepUpEndpointFilter re-authentication, and security audit logging (plan §5.4/§5.10).
    /// </summary>
    public static class BackupEndpoints
    {
        /// <summary>Resolves the DB-configured Backup folder (QueryExportLocation), or the storage default when unset.</summary>
        public static string ResolveBackupExportRoot(IStorageLocationProvider storageLocations, IForensicsConfiguration config)
        {
            return storageLocations.GetEffectiveRoot(StorageCategory.Backup, config.QueryExportLocation);
        }

        public static void MapBackupEndpoints(this WebApplication app)
        {
            RouteGroupBuilder group = app.MapGroup("/api/v1/backup").RequireAuthorization();

            _ = group.MapPost("/prepare-export", async (
                IBackupExportService exportService,
                CancellationToken ct) =>
            {
                PrepareExportResult result = await exportService.PrepareForExportAsync(ct);
                return Results.Ok(result.ToDto());
            })
            .RequireRateLimiting("media")
            .WithSummary("Prepare export validation")
            .WithDescription("Validates that every downloaded event's media and sidecar entries are present and correctly tagged before export.");

            _ = group.MapPost("/export", async (
                IBackupExportService exportService,
                IForensicsConfigurationService configService,
                IStorageLocationProvider storageLocations,
                ISecurityAuditLogger auditLog,
                INetworkTierResolver tierResolver,
                HttpContext context,
                CancellationToken ct) =>
            {
                // The archive goes to the DB-configured Backup folder (QueryExportLocation), never a client-chosen path.
                IForensicsConfiguration config = await configService.LoadConfigurationAsync(string.Empty, ct);
                string backupRoot = ResolveBackupExportRoot(storageLocations, config);

                BackupExportResult result = await exportService.ExportBackupAsync(
                    backupRoot,
                    ct);

                string? operatorIdClaim = context.User.FindFirst(VideoForensicsClaimTypes.OperatorId)?.Value;
                string description = "Database backup export";
                await auditLog.LogAsync(
                    SecurityAuditEventTypes.BackupExported,
                    Guid.TryParse(operatorIdClaim, out Guid actingOperatorId) ? actingOperatorId : null,
                    null,
                    tierResolver.ResolveClientIp(context),
                    description,
                    isUrgent: true,
                    ct);

                if (result.Success && result.ArchivePath != null && System.IO.File.Exists(result.ArchivePath))
                {
                    // Stream the backup file back to the client
                    Stream stream = System.IO.File.OpenRead(result.ArchivePath);
                    string fileName = Path.GetFileName(result.ArchivePath);
                    return Results.Stream(stream, "application/zip", fileName, enableRangeProcessing: false);
                }

                // If no file was created, return the result summary
                return Results.Ok(result.ToDto());
            })
            .AddEndpointFilter<MaintenanceEndpointFilter>()
            .RequireAuthorization(VideoForensicsPolicies.SuperAdminLocalOrPasskey)
            .AddEndpointFilter<StepUpEndpointFilter>()
            .RequireRateLimiting("media")
            .WithSummary("Export database backup")
            .WithDescription("Exports the entire database (accounts, locations, devices, events, media metadata) as a JSON+zip backup archive. Requires step-up authentication. This operation is sensitive as it creates a complete database snapshot.");
        }
    }
}
