using VideoForensics.Api.Contracts;
using VideoForensics.Client.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Hosting;
using VideoForensics.WebApp.Auth;
using VideoForensics.WebApp.Services;

namespace VideoForensics.WebApp.Api
{
    /// <summary>
    /// Handles chain-of-custody evidence validation and export. Per the plan's auth-default rule,
    /// EVERY endpoint here requires paired-device authentication (RequireAuthorization()) - there is
    /// no general exemption for read-only operations, only for endpoints that are inherently
    /// pre-authentication (e.g. AuthEndpoints' login). Read-only validation checks
    /// (VerifyLocalIntegrity, ReconcileWithProvider) additionally do NOT require step-up
    /// re-authentication or audit logging, since they only surface discrepancies without moving
    /// data. Export IS exposure-widening (data leaving the system) and therefore additionally
    /// requires RequireAuthorization(VideoForensicsPolicies.SuperAdminLocal) (a stricter role than
    /// the group default), StepUpEndpointFilter, and audit logging - matching the asymmetry
    /// documented in NetworkSettingsEndpoints: narrowing reduces exposure (allowed without
    /// step-up), widening increases exposure (requires step-up).
    /// </summary>
    public static class EvidenceEndpoints
    {
        public static void MapEvidenceEndpoints(this WebApplication app)
        {
            RouteGroupBuilder group = app.MapGroup("/api/v1/evidence").RequireAuthorization();

            _ = group.MapPost("/verify-local-integrity", async (
                VerifyLocalIntegrityRequest request,
                IEvidenceValidationService validationService,
                CancellationToken ct) =>
            {
                IReadOnlyList<MediaVerificationResult> results = await validationService.VerifyLocalIntegrityAsync(request.DeviceId, ct);
                return Results.Ok(results.Select(r => r.ToDto()).ToList());
            })
            .RequireRateLimiting("media")
            .WithSummary("Verify local file integrity")
            .WithDescription("Re-verifies SHA-256 hashes of downloaded files against stored hashes to detect corruption or tampering.");

            _ = group.MapPost("/reconcile", async (
                ReconcileWithProviderRequest request,
                IEvidenceValidationService validationService,
                CancellationToken ct) =>
            {
                IReadOnlyList<ReconciliationDiscrepancy> discrepancies = await validationService.ReconcileWithProviderAsync(
                    request.DeviceId,
                    request.ProviderDeviceId,
                    request.FromUtc,
                    request.ToUtc,
                    ct);
                return Results.Ok(discrepancies.Select(d => d.ToDto()).ToList());
            })
            .RequireRateLimiting("media")
            .WithSummary("Reconcile with provider")
            .WithDescription("Compares stored events against the provider's current records to detect changes, deletions, or new events that indicate potential tampering or service issues.");

            _ = group.MapPost("/export", async (
                ExportEvidenceRequest request,
                IEvidenceExportService exportService,
                ISecurityAuditLogger auditLog,
                INetworkTierResolver tierResolver,
                HttpContext context,
                CancellationToken ct) =>
            {
                ExportResult result = await exportService.ExportEvidenceAsync(
                    request.MediaItemIds,
                    request.OutputDirectory,
                    request.CaseReference,
                    request.RecipientDescription,
                    request.Passphrase,
                    ct);

                string? operatorIdClaim = context.User.FindFirst(VideoForensicsClaimTypes.OperatorId)?.Value;
                string description = "Evidence export" + (request.CaseReference != null ? $" for case {request.CaseReference}" : "");
                await auditLog.LogAsync(
                    SecurityAuditEventTypes.EvidenceExported,
                    Guid.TryParse(operatorIdClaim, out Guid actingOperatorId) ? actingOperatorId : null,
                    null,
                    tierResolver.ResolveClientIp(context),
                    description,
                    isUrgent: true,
                    ct);

                return Results.Ok(result.ToDto());
            })
            .RequireAuthorization(VideoForensicsPolicies.SuperAdminLocal)
            .AddEndpointFilter<StepUpEndpointFilter>()
            .RequireRateLimiting("media")
            .WithSummary("Export evidence to archive")
            .WithDescription("Exports selected media items into a password-protected archive with manifest and chain-of-custody records. This operation requires step-up authentication as it exposes evidence data beyond normal access controls.");

            _ = group.MapPost("/validate-all", async (
                BulkValidateAllRequest request,
                BulkValidationService bulkValidationService,
                CancellationToken ct) =>
            {
                BulkValidationResult result = await bulkValidationService.RunFullValidationAsync(
                    request.FromUtcOverride,
                    request.ToUtcOverride,
                    ct);
                return Results.Ok(result.ToDto());
            })
            .RequireRateLimiting("media")
            .WithSummary("Validate all devices")
            .WithDescription("Runs full provider reconciliation with auto-fix across all devices. Detects and fixes missing events, changed metadata, and integrity issues.");
        }
    }

    /// <summary>Request to verify local integrity of downloaded media files.</summary>
    /// <param name="DeviceId">Device to verify, or null to verify all.</param>
    public record VerifyLocalIntegrityRequest(Guid? DeviceId);

    /// <summary>Request to reconcile stored events with provider's current records.</summary>
    /// <param name="DeviceId">Database device ID.</param>
    /// <param name="ProviderDeviceId">Provider's device identifier.</param>
    /// <param name="FromUtc">Start of reconciliation date range.</param>
    /// <param name="ToUtc">End of reconciliation date range.</param>
    public record ReconcileWithProviderRequest(
        Guid DeviceId,
        string ProviderDeviceId,
        DateTime FromUtc,
        DateTime ToUtc
    );

    /// <summary>Request to export media items to an archive.</summary>
    /// <param name="MediaItemIds">IDs of media items to export.</param>
    /// <param name="OutputDirectory">Output directory for the archive.</param>
    /// <param name="CaseReference">Case reference number for the export metadata.</param>
    /// <param name="RecipientDescription">Description of the archive recipient/purpose.</param>
    /// <param name="Passphrase">Optional passphrase for AES-256 encryption.</param>
    public record ExportEvidenceRequest(
        IReadOnlyList<Guid> MediaItemIds,
        string OutputDirectory,
        string? CaseReference,
        string? RecipientDescription,
        string? Passphrase
    );

    /// <summary>Request to validate all devices with optional date range override.</summary>
    /// <param name="FromUtcOverride">Override default start date (default: 90 days ago).</param>
    /// <param name="ToUtcOverride">Override default end date (default: today).</param>
    public record BulkValidateAllRequest(
        DateTime? FromUtcOverride = null,
        DateTime? ToUtcOverride = null);

    /// <summary>Extension methods for converting service contract results to API DTOs.</summary>
    internal static class EvidenceResultDtoExtensions
    {
        public static MediaVerificationResultDto ToDto(this MediaVerificationResult result)
        {
            return new MediaVerificationResultDto(
                MediaItemId: result.MediaItemId,
                FileName: result.FileName,
                Status: result.Status,
                FailureReason: result.FailureReason
            );
        }

        public static ReconciliationDiscrepancyDto ToDto(this ReconciliationDiscrepancy entity)
        {
            return new ReconciliationDiscrepancyDto(
                Type: entity.Type,
                ProviderEventId: entity.ProviderEventId,
                FieldName: entity.FieldName,
                StoredValue: entity.StoredValue,
                ProviderValue: entity.ProviderValue
            );
        }

        public static ExportResultDto ToDto(this ExportResult result)
        {
            return new ExportResultDto(
                Success: result.Success,
                ArchivePath: result.ArchivePath,
                ArchiveSha256Hash: result.ArchiveSha256Hash,
                ItemsIncluded: result.ItemsIncluded,
                ItemsExcludedForFailedIntegrity: result.ItemsExcludedForFailedIntegrity,
                ErrorMessage: result.ErrorMessage
            );
        }

        public static BulkValidationResultDto ToDto(this BulkValidationResult result)
        {
            return new BulkValidationResultDto(
                TotalDevicesScanned: result.TotalDevicesScanned,
                TotalFilesVerified: result.TotalFilesVerified,
                TotalFilesIntact: result.TotalFilesIntact,
                TotalFilesFailed: result.TotalFilesFailed,
                TotalFilesMissing: result.TotalFilesMissing,
                TotalDiscrepanciesFound: result.TotalDiscrepanciesFound,
                TotalDiscrepanciesFixed: result.TotalDiscrepanciesFixed,
                StartedAtUtc: result.StartedAtUtc,
                CompletedAtUtc: result.CompletedAtUtc,
                ErrorMessage: result.ErrorMessage
            );
        }
    }
}
