using Microsoft.AspNetCore.Mvc;

using VideoForensics.Api.Contracts;
using VideoForensics.Client.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Hosting;
using VideoForensics.WebApp.Auth;

namespace VideoForensics.WebApp.Api
{
    /// <summary>
    /// Handles database backup export and import. Per the plan's auth-default rule,
    /// EVERY endpoint here requires paired-device authentication (RequireAuthorization()) - there is
    /// no general exemption for any backup operation. Both export and import are exposure-widening
    /// operations that additionally require RequireAuthorization(VideoForensicsPolicies.SuperAdminLocal),
    /// StepUpEndpointFilter re-authentication, and security audit logging (plan §5.4/§5.10).
    /// </summary>
    public static class BackupEndpoints
    {
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
                ExportBackupRequest request,
                IBackupExportService exportService,
                ISecurityAuditLogger auditLog,
                INetworkTierResolver tierResolver,
                HttpContext context,
                CancellationToken ct) =>
            {
                BackupExportResult result = await exportService.ExportBackupAsync(
                    request.OutputDirectory,
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
            .RequireAuthorization(VideoForensicsPolicies.SuperAdminLocal)
            .AddEndpointFilter<StepUpEndpointFilter>()
            .RequireRateLimiting("media")
            .WithSummary("Export database backup")
            .WithDescription("Exports the entire database (accounts, locations, devices, events, media metadata) as a JSON+zip backup archive. Requires step-up authentication. This operation is sensitive as it creates a complete database snapshot.");

            _ = group.MapPost("/import", async (
                IFormFile backupFile,
                [FromForm] string mediaRootPath,
                IBackupImportService importService,
                ISecurityAuditLogger auditLog,
                INetworkTierResolver tierResolver,
                HttpContext context,
                CancellationToken ct) =>
            {
                // Save uploaded file to temporary location
                string tempBackupPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".zip");
                try
                {
                    using (FileStream fileStream = System.IO.File.Create(tempBackupPath))
                    {
                        await backupFile.CopyToAsync(fileStream, ct);
                    }

                    BackupImportResult result = await importService.ImportBackupAsync(
                        tempBackupPath,
                        mediaRootPath,
                        ct);

                    string? operatorIdClaim = context.User.FindFirst(VideoForensicsClaimTypes.OperatorId)?.Value;
                    string description = "Database backup import";
                    await auditLog.LogAsync(
                        SecurityAuditEventTypes.BackupImported,
                        Guid.TryParse(operatorIdClaim, out Guid actingOperatorId) ? actingOperatorId : null,
                        null,
                        tierResolver.ResolveClientIp(context),
                        description,
                        isUrgent: true,
                        ct);

                    return Results.Ok(result.ToDto());
                }
                finally
                {
                    // Clean up temporary file
                    if (System.IO.File.Exists(tempBackupPath))
                    {
                        try
                        {
                            System.IO.File.Delete(tempBackupPath);
                        }
                        catch
                        {
                            // Ignore cleanup errors
                        }
                    }
                }
            })
            .RequireAuthorization(VideoForensicsPolicies.SuperAdminLocal)
            .AddEndpointFilter<StepUpEndpointFilter>()
            .RequireRateLimiting("media")
            .Accepts<IFormFile>("multipart/form-data")
            .WithSummary("Import database backup")
            .WithDescription("Imports a backup archive produced by the export endpoint back into the database. Skips duplicate records and orphaned entries. Media files are rehomed under the specified mediaRootPath and re-verified. Requires step-up authentication. This operation overwrites/merges database contents and is sensitive.");
        }
    }

    /// <summary>Request to export the database to a backup archive.</summary>
    /// <param name="OutputDirectory">Output directory where the backup archive will be created.</param>
    public record ExportBackupRequest(string OutputDirectory);
}
