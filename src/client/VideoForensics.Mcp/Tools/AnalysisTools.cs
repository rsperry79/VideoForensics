using System.ComponentModel;
using ModelContextProtocol.Server;
using VideoForensics.Client.Common;
using VideoForensics.Data.Core.Contracts;
using VideoForensics.Data.Core.Models;

namespace VideoForensics.Mcp.Tools
{
    /// <summary>MCP tools for generating forensic analysis reports and validating evidence.</summary>
    [McpServerToolType]
    public static class AnalysisTools
    {
        [McpServerTool, Description("Builds a comprehensive forensic analysis report summarizing evidence across devices and timeframe (media counts, integrity status, gaps).")]
        public static async Task<ForensicAnalysisReport> BuildForensicAnalysisReport(
            IReportGenerationService reportService,
            [Description("Restrict to a single device (data-layer Guid), or omit for all devices")] Guid? deviceId,
            [Description("Start of the reporting window (UTC)")] DateTime fromUtc,
            [Description("End of the reporting window (UTC)")] DateTime toUtc,
            CancellationToken cancellationToken)
        {
            return await reportService.BuildForensicAnalysisReportAsync(deviceId, fromUtc, toUtc, cancellationToken);
        }

        [McpServerTool, Description("Builds a signal anomaly report analyzing device RSSI health metrics, including any persisted jamming incidents/stats for the period. For a fresh jamming detection pass over raw events, use JammingTools.RunJammingDetection first — see the 'videoforensics://instructions/jamming-analysis' resource for guidance.")]
        public static async Task<SignalAnomalyReport> BuildSignalAnomalyReport(
            IReportGenerationService reportService,
            [Description("Restrict to a single device (data-layer Guid), or omit for all devices")] Guid? deviceId,
            [Description("Start of the reporting window (UTC)")] DateTime fromUtc,
            [Description("End of the reporting window (UTC)")] DateTime toUtc,
            CancellationToken cancellationToken)
        {
            return await reportService.BuildSignalAnomalyReportAsync(deviceId, fromUtc, toUtc, cancellationToken);
        }

        [McpServerTool, Description("Builds an access control report showing who accessed and exported evidence over the given period.")]
        public static async Task<AccessControlReport> BuildAccessControlReport(
            IReportGenerationService reportService,
            [Description("Restrict to a single device (data-layer Guid), or omit for all devices")] Guid? deviceId,
            [Description("Start of the reporting window (UTC)")] DateTime fromUtc,
            [Description("End of the reporting window (UTC)")] DateTime toUtc,
            CancellationToken cancellationToken)
        {
            return await reportService.BuildAccessControlReportAsync(deviceId, fromUtc, toUtc, cancellationToken);
        }

        [McpServerTool, Description("Builds a chain of custody report showing the complete audit trail for evidence over the given period.")]
        public static async Task<ChainOfCustodyReport> BuildChainOfCustodyReport(
            IReportGenerationService reportService,
            [Description("Restrict to a single device (data-layer Guid), or omit for all devices")] Guid? deviceId,
            [Description("Start of the reporting window (UTC)")] DateTime fromUtc,
            [Description("End of the reporting window (UTC)")] DateTime toUtc,
            CancellationToken cancellationToken)
        {
            return await reportService.BuildChainOfCustodyReportAsync(deviceId, fromUtc, toUtc, cancellationToken);
        }

        [McpServerTool, Description("Re-verifies the SHA-256 hash of downloaded evidence files against their stored hashes, detecting local tampering or corruption.")]
        public static async Task<IReadOnlyList<MediaVerificationResult>> ValidateEvidence(
            IEvidenceValidationService validationService,
            [Description("Restrict to a single device (data-layer Guid), or omit to verify all devices")] Guid? deviceId,
            CancellationToken cancellationToken)
        {
            return await validationService.VerifyLocalIntegrityAsync(deviceId, cancellationToken);
        }

        [McpServerTool, Description("Reconciles stored events for a device against the provider's current live record over a date range, detecting discrepancies such as deleted or modified events on the provider side (potential evidence tampering).")]
        public static async Task<IReadOnlyList<Data.Common.Entities.ReconciliationDiscrepancy>> ReconcileWithProvider(
            IEvidenceValidationService validationService,
            Data.Common.Contracts.IDeviceRepository deviceRepository,
            [Description("Data-layer device Guid to reconcile")] Guid deviceId,
            [Description("Start of the date range (UTC)")] DateTime fromUtc,
            [Description("End of the date range (UTC)")] DateTime toUtc,
            CancellationToken cancellationToken)
        {
            var device = await deviceRepository.GetAsync(deviceId, cancellationToken);
            if (device == null)
            {
                throw new InvalidOperationException($"Device {deviceId} not found.");
            }

            return await validationService.ReconcileWithProviderAsync(deviceId, device.ProviderDeviceId, fromUtc, toUtc, cancellationToken);
        }
    }
}
