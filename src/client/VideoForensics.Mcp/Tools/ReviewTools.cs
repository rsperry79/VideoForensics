using System.ComponentModel;
using ModelContextProtocol.Server;
using VideoForensics.Client.Common;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Core.Contracts;
using VideoForensics.Data.Core.Models;

namespace VideoForensics.Mcp.Tools
{
    /// <summary>MCP tools for reviewing and exporting evidence.</summary>
    [McpServerToolType]
    public static class ReviewTools
    {
        [McpServerTool, Description("Builds an evidence review listing media items and their integrity status for a device/date range.")]
        public static async Task<EvidenceReviewReport> BuildEvidenceReview(
            IReportGenerationService reportService,
            [Description("Restrict to a single device (data-layer Guid), or omit for all devices")] Guid? deviceId,
            [Description("Start of the reporting window (UTC)")] DateTime fromUtc,
            [Description("End of the reporting window (UTC)")] DateTime toUtc,
            CancellationToken cancellationToken)
        {
            return await reportService.BuildEvidenceReviewAsync(deviceId, fromUtc, toUtc, cancellationToken);
        }

        [McpServerTool, Description("DESTRUCTIVE-ADJACENT / WRITES TO DISK: Exports evidence for a device/date range into a password-protected, integrity-verified archive on disk. This performs a real filesystem write and should be confirmed with the user before calling — pass confirm=true only after the user has explicitly agreed; otherwise this call does nothing and returns an error.")]
        public static async Task<string> ExportEvidence(
            IEvidenceExportService exportService,
            IMediaItemRepository mediaItemRepository,
            [Description("Must be true, confirming the user explicitly agreed to this export. If false or omitted, no export is performed.")] bool confirm,
            [Description("Case reference to embed in the export manifest")] string? caseReference,
            [Description("Restrict to a single device (data-layer Guid), or omit for all devices")] Guid? deviceId,
            [Description("Start of the date range (UTC)")] DateTime fromUtc,
            [Description("End of the date range (UTC)")] DateTime toUtc,
            [Description("Directory to write the export archive into")] string outputDirectory,
            [Description("Optional description of the intended recipient, embedded in the manifest")] string? recipientDescription = null,
            [Description("Optional passphrase for AES-256 encryption of the archive; if omitted, the archive is not encrypted")] string? passphrase = null)
        {
            if (!confirm)
            {
                return "Export not performed: confirm=true is required. Ask the user to explicitly confirm before calling ExportEvidence again with confirm=true.";
            }

            IReadOnlyList<MediaItem> mediaItems;
            if (deviceId.HasValue)
            {
                mediaItems = await mediaItemRepository.GetByDeviceAndDateRangeAsync(deviceId.Value, fromUtc, toUtc, CancellationToken.None);
            }
            else
            {
                var allItems = await mediaItemRepository.ListAsync(CancellationToken.None);
                mediaItems = allItems.Where(m => m.RecordedAtUtc >= fromUtc && m.RecordedAtUtc <= toUtc).ToList();
            }

            var mediaItemIds = mediaItems.Select(m => m.Id).ToList();
            if (mediaItemIds.Count == 0)
            {
                return "No media items found for the specified criteria; nothing exported.";
            }

            var result = await exportService.ExportEvidenceAsync(
                mediaItemIds, outputDirectory, caseReference, recipientDescription, passphrase, CancellationToken.None);

            if (!result.Success)
            {
                return $"Export failed: {result.ErrorMessage}";
            }

            var summary = $"Export completed. Archive: {result.ArchivePath}, SHA-256: {result.ArchiveSha256Hash}, items included: {result.ItemsIncluded}.";
            if (result.ItemsExcludedForFailedIntegrity.Count > 0)
            {
                summary += $" {result.ItemsExcludedForFailedIntegrity.Count} item(s) excluded for failed integrity verification.";
            }
            return summary;
        }
    }
}
