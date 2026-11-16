using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Client.Hosting;
using VideoForensics.Client.WebApp.Auth;
using VideoForensics.Client.WebApp.Infrastructure;
using VideoForensics.Client.WebApp.Services;
using VideoForensics.Core.Api.Contracts;
using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Client.WebApp.Api
{
    /// <summary>
    /// Chunked backup import. The client starts an upload, sends the zip in chunks of at most 1 MB, then asks the
    /// server to assemble and import it. Uses the same auth, step-up and rate limit as the single-request import.
    /// </summary>
    public static class BackupUploadEndpoints
    {
        public static void MapBackupUploadEndpoints(this WebApplication app)
        {
            RouteGroupBuilder group = app.MapGroup("/api/v1/backup/imports")
                .RequireAuthorization(VideoForensicsPolicies.SuperAdminLocalOrPasskey)
                .AddEndpointFilter<MaintenanceEndpointFilter>()
                .AddEndpointFilter<StepUpEndpointFilter>()
                .RequireRateLimiting("media");

            _ = group.MapPost("", (BackupUploadStore store, HttpContext context) =>
            {
                if (!TryOperator(context, out Guid operatorId))
                {
                    return Results.Unauthorized();
                }

                return Results.Ok(new BackupUploadCreatedDto(store.Create(operatorId)));
            })
            .WithSummary("Start a chunked backup import")
            .WithDescription("Starts an upload session for a backup archive. Send the chunks next, then complete. Requires step-up authentication.");

            _ = group.MapPut("/{uploadId:guid}/chunks/{index:int}", (
                Guid uploadId,
                int index,
                BackupUploadStore store,
                HttpContext context) =>
            {
                if (!TryOperator(context, out Guid operatorId))
                {
                    return Results.Unauthorized();
                }

                try
                {
                    store.WriteChunk(uploadId, operatorId, index, context.Request.Body);
                    return Results.NoContent();
                }
                catch (KeyNotFoundException)
                {
                    return Results.NotFound(new { error = "Backup upload not found." });
                }
                catch (UnauthorizedAccessException)
                {
                    return Results.Forbid();
                }
                catch (ArgumentException ex)
                {
                    return Results.BadRequest(new { error = ex.Message });
                }
                catch (InvalidOperationException ex)
                {
                    return Results.Json(new { error = ex.Message }, statusCode: StatusCodes.Status413RequestEntityTooLarge);
                }
            })
            .WithSummary("Send one backup chunk")
            .WithDescription("Stores one chunk of at most 1 MB. Re-sending an index replaces it.");

            _ = group.MapPost("/{uploadId:guid}/complete", async (
                Guid uploadId,
                CompleteBackupUploadRequest request,
                BackupUploadStore store,
                IBackupImportService importService,
                ISecurityAuditLogger auditLog,
                INetworkTierResolver tierResolver,
                HttpContext context,
                CancellationToken ct) =>
            {
                if (!TryOperator(context, out Guid operatorId))
                {
                    return Results.Unauthorized();
                }

                string assembled;
                try
                {
                    assembled = store.Assemble(uploadId, operatorId, request.ChunkCount);
                }
                catch (KeyNotFoundException)
                {
                    return Results.NotFound(new { error = "Backup upload not found." });
                }
                catch (UnauthorizedAccessException)
                {
                    return Results.Forbid();
                }
                catch (InvalidOperationException ex)
                {
                    return Results.BadRequest(new { error = ex.Message });
                }

                try
                {
                    BackupImportResult result = await importService.ImportBackupAsync(assembled, request.MediaRootPath, ct);
                    await auditLog.LogAsync(
                        SecurityAuditEventTypes.BackupImported,
                        operatorId,
                        null,
                        tierResolver.ResolveClientIp(context),
                        "Database backup import (chunked)",
                        isUrgent: true,
                        ct);
                    return Results.Ok(result.ToDto());
                }
                finally
                {
                    try
                    {
                        store.Delete(uploadId, operatorId);
                    }
                    catch (Exception ex) when (ex is IOException or KeyNotFoundException or UnauthorizedAccessException)
                    {
                        // The session is already gone or locked; nothing else to clean up.
                    }
                }
            })
            .WithSummary("Assemble and import a chunked backup")
            .WithDescription("Joins the uploaded chunks, imports the archive through the write gate, then removes the upload. Requires step-up authentication.");
        }

        private static bool TryOperator(HttpContext context, out Guid operatorId)
        {
            string? claim = context.User.FindFirst(VideoForensicsClaimTypes.OperatorId)?.Value;
            return Guid.TryParse(claim, out operatorId);
        }
    }
}
