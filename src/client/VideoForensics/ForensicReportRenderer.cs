namespace VideoForensics
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using Spectre.Console;
    using VideoForensics.Client.Common;
    using VideoForensics.Data.Core.Contracts;

    public class ForensicReportRenderer : IForensicReportRenderer
    {
        private readonly IForensicsConfiguration _forensicsConfig;
        private readonly IReportGenerationService _reportService;
        private readonly ILogger<ForensicReportRenderer> _logger;

        public ForensicReportRenderer(
            IForensicsConfiguration forensicsConfig,
            IReportGenerationService reportService,
            ILogger<ForensicReportRenderer> logger)
        {
            _forensicsConfig = forensicsConfig;
            _reportService = reportService;
            _logger = logger;
        }

        public async Task ShowEvidenceAsync(CancellationToken ct)
        {
            AnsiConsole.MarkupLine("[bold cyan]Forensic Evidence[/]");

            try
            {
                var report = await _reportService.BuildEvidenceReviewAsync(
                    deviceId: null,
                    fromUtc: DateTime.UtcNow.AddDays(-90),
                    toUtc: DateTime.UtcNow,
                    ct);

                if (report.MediaItems.Count == 0)
                {
                    AnsiConsole.MarkupLine("[yellow]ℹ No evidence found in the last 90 days[/]");
                    return;
                }

                var table = new Table();
                table.AddColumn("Evidence ID");
                table.AddColumn("Device ID");
                table.AddColumn("Format");
                table.AddColumn("Status");
                table.Border = TableBorder.Rounded;

                foreach (var item in report.MediaItems)
                {
                    var status = DetermineMediaStatus(item.Id, report.IntegrityRecords);
                    table.AddRow(
                        item.Id.ToString("N").Substring(0, 8),
                        item.DeviceId.ToString("N").Substring(0, 8),
                        item.MediaFormat ?? "Unknown",
                        status);
                }

                AnsiConsole.Write(table);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load evidence report");
                AnsiConsole.MarkupLine("[red]✗ Failed to load report data[/]");
            }
        }

        public async Task ShowForensicReportsAsync(CancellationToken ct)
        {
            AnsiConsole.MarkupLine("[bold cyan]Forensic Report Generation[/]");

            var reportStatus = new Table();
            reportStatus.AddColumn("Report Type");
            reportStatus.AddColumn("Status");
            reportStatus.Border = TableBorder.Rounded;

            reportStatus.AddRow(
                "Forensic Analysis",
                _forensicsConfig.EnableForensicAnalysisReports ? "[green]✓ Enabled[/]" : "[red]✗ Disabled[/]");
            reportStatus.AddRow(
                "Chain of Custody",
                _forensicsConfig.EnableChainOfCustodyReports ? "[green]✓ Enabled[/]" : "[red]✗ Disabled[/]");
            reportStatus.AddRow(
                "Evidence Validation",
                _forensicsConfig.EnableEvidenceValidationReports ? "[green]✓ Enabled[/]" : "[red]✗ Disabled[/]");
            reportStatus.AddRow(
                "Signal Anomaly",
                _forensicsConfig.EnableSignalAnomalyReports ? "[green]✓ Enabled[/]" : "[red]✗ Disabled[/]");

            AnsiConsole.Write(reportStatus);

            if (!_forensicsConfig.EnableForensicAnalysisReports)
            {
                AnsiConsole.MarkupLine("[yellow]⚠ Forensic analysis reporting is disabled in configuration[/]");
                return;
            }

            try
            {
                var report = await _reportService.BuildForensicAnalysisReportAsync(
                    deviceId: null,
                    fromUtc: DateTime.UtcNow.AddDays(-90),
                    toUtc: DateTime.UtcNow,
                    ct);

                AnsiConsole.MarkupLine("");
                AnsiConsole.MarkupLine("[yellow]Report Period:[/] {0} to {1}",
                    report.ReportFromUtc.ToString("yyyy-MM-dd HH:mm"),
                    report.ReportToUtc.ToString("yyyy-MM-dd HH:mm"));
                AnsiConsole.MarkupLine("[yellow]Generated:[/] {0}[/]", report.GeneratedAtUtc.ToString("yyyy-MM-dd HH:mm"));

                if (!string.IsNullOrEmpty(report.Summary))
                {
                    AnsiConsole.MarkupLine("");
                    AnsiConsole.MarkupLine("[yellow]Summary:[/]");
                    AnsiConsole.MarkupLine("{0}", report.Summary);
                }

                if (report.EvidenceItems.Count > 0)
                {
                    AnsiConsole.MarkupLine("");
                    AnsiConsole.MarkupLine("[yellow]Evidence Items: {0}[/]", report.EvidenceItems.Count);
                }

                if (report.AnomalousHealthSnapshots.Count > 0)
                {
                    AnsiConsole.MarkupLine("[yellow]Anomalous Health Events: {0}[/]", report.AnomalousHealthSnapshots.Count);
                }

                if (report.SignificantActions.Count > 0)
                {
                    AnsiConsole.MarkupLine("[yellow]Significant Actions: {0}[/]", report.SignificantActions.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load forensic analysis report");
                AnsiConsole.MarkupLine("[red]✗ Failed to load report data[/]");
            }

            AnsiConsole.MarkupLine("");
            AnsiConsole.MarkupLine("[yellow]Reports saved to:[/] {0}",
                string.IsNullOrEmpty(_forensicsConfig.ReportsDirectory)
                    ? "[red]Not configured[/]"
                    : _forensicsConfig.ReportsDirectory);
            AnsiConsole.MarkupLine("[yellow]Format:[/] {0}", _forensicsConfig.ReportOutputFormat);
        }

        public async Task ShowSignalAnomaliesAsync(CancellationToken ct)
        {
            AnsiConsole.MarkupLine("[bold cyan]Signal Strength Analysis[/]");

            try
            {
                var report = await _reportService.BuildSignalAnomalyReportAsync(
                    deviceId: null,
                    fromUtc: DateTime.UtcNow.AddDays(-30),
                    toUtc: DateTime.UtcNow,
                    ct);

                if (report.AnomaliesByDevice.Count == 0)
                {
                    AnsiConsole.MarkupLine("[yellow]ℹ No signal anomalies detected in the last 30 days[/]");
                    return;
                }

                foreach (var deviceAnomaly in report.AnomaliesByDevice)
                {
                    AnsiConsole.MarkupLine("[yellow]Device: {0}[/]", deviceAnomaly.DeviceName);

                    if (deviceAnomaly.Anomalies.Count == 0)
                    {
                        AnsiConsole.MarkupLine("  [green]✓ No anomalies[/]");
                    }
                    else
                    {
                        foreach (var anomaly in deviceAnomaly.Anomalies)
                        {
                            var icon = anomaly.AnomalyType switch
                            {
                                "DegradedSignal" => "[orange3]⚠[/]",
                                "ConnectionLoss" => "[red][/]",
                                _ => "[yellow]●[/]"
                            };
                            AnsiConsole.MarkupLine("  {0} {1} - {2} (RSSI: {3})",
                                icon,
                                anomaly.AnomalyType,
                                anomaly.OccurredAtUtc.ToString("yyyy-MM-dd HH:mm"),
                                anomaly.RssiValue?.ToString() ?? "N/A");
                        }
                    }
                    AnsiConsole.MarkupLine("");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load signal anomaly report");
                AnsiConsole.MarkupLine("[red]✗ Failed to load report data[/]");
            }
        }

        public async Task ShowAccessControlAsync(CancellationToken ct)
        {
            AnsiConsole.MarkupLine("[bold cyan]Evidence Access Monitoring[/]");

            if (!_forensicsConfig.EnableAccessControlMonitoring)
            {
                AnsiConsole.MarkupLine("[yellow]⚠ Access control monitoring is disabled in configuration[/]");
                return;
            }

            try
            {
                var report = await _reportService.BuildAccessControlReportAsync(
                    deviceId: null,
                    fromUtc: DateTime.UtcNow.AddDays(-30),
                    toUtc: DateTime.UtcNow,
                    ct);

                if (report.AccessEvents.Count == 0 && report.ExportEvents.Count == 0)
                {
                    AnsiConsole.MarkupLine("[yellow]ℹ No access or export events in the last 30 days[/]");
                    return;
                }

                // Show access events
                if (report.AccessEvents.Count > 0)
                {
                    AnsiConsole.MarkupLine("[yellow]Access Events:[/]");
                    var accessTable = new Table();
                    accessTable.AddColumn("Actor");
                    accessTable.AddColumn("Action");
                    accessTable.AddColumn("Entity Type");
                    accessTable.AddColumn("Timestamp");
                    accessTable.Border = TableBorder.Rounded;

                    foreach (var evt in report.AccessEvents)
                    {
                        accessTable.AddRow(
                            evt.Actor,
                            evt.Action,
                            evt.EntityType,
                            evt.AccessedAtUtc.ToString("yyyy-MM-dd HH:mm"));
                    }
                    AnsiConsole.Write(accessTable);
                    AnsiConsole.MarkupLine("");
                }

                // Show export events
                if (report.ExportEvents.Count > 0)
                {
                    AnsiConsole.MarkupLine("[yellow]Export Events:[/]");
                    var exportTable = new Table();
                    exportTable.AddColumn("User");
                    exportTable.AddColumn("Case Reference");
                    exportTable.AddColumn("Item Count");
                    exportTable.AddColumn("Timestamp");
                    exportTable.Border = TableBorder.Rounded;

                    foreach (var evt in report.ExportEvents)
                    {
                        exportTable.AddRow(
                            evt.ExportedByUserName,
                            evt.CaseReference ?? "N/A",
                            evt.ItemCount.ToString(),
                            evt.ExportedAtUtc.ToString("yyyy-MM-dd HH:mm"));
                    }
                    AnsiConsole.Write(exportTable);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load access control report");
                AnsiConsole.MarkupLine("[red]✗ Failed to load report data[/]");
            }
        }

        public async Task ShowChainOfCustodyAsync(CancellationToken ct)
        {
            AnsiConsole.MarkupLine("[bold cyan]Chain of Custody Management[/]");

            if (!_forensicsConfig.EnableChainOfCustodyReports)
            {
                AnsiConsole.MarkupLine("[yellow]⚠ Chain of Custody reporting is disabled in configuration[/]");
                return;
            }

            try
            {
                var report = await _reportService.BuildChainOfCustodyReportAsync(
                    deviceId: null,
                    fromUtc: DateTime.UtcNow.AddDays(-90),
                    toUtc: DateTime.UtcNow,
                    ct);

                // Show chain verification status prominently
                if (!report.ChainIntegrityVerified)
                {
                    AnsiConsole.MarkupLine("[red]CHAIN INTEGRITY FAILED[/]");
                    AnsiConsole.MarkupLine("[red]Status: {0}[/]", report.ChainVerificationStatus ?? "Verification failed");
                    AnsiConsole.MarkupLine("");
                }
                else
                {
                    AnsiConsole.MarkupLine("[green]✓ Chain integrity verified[/]");
                    if (!string.IsNullOrEmpty(report.ChainVerificationStatus))
                    {
                        AnsiConsole.MarkupLine("[green]Status: {0}[/]", report.ChainVerificationStatus);
                    }
                    AnsiConsole.MarkupLine("");
                }

                // Show audit trail
                if (report.AuditTrail.Count == 0)
                {
                    AnsiConsole.MarkupLine("[yellow]ℹ No audit trail entries in the last 90 days[/]");
                    return;
                }

                var table = new Table();
                table.AddColumn("ID");
                table.AddColumn("Actor");
                table.AddColumn("Action");
                table.AddColumn("Timestamp");
                table.Border = TableBorder.Rounded;

                foreach (var entry in report.AuditTrail)
                {
                    table.AddRow(
                        entry.Id.ToString("N").Substring(0, 8),
                        entry.Actor ?? "Unknown",
                        entry.Action ?? "Unknown",
                        entry.TimestampUtc.ToString("yyyy-MM-dd HH:mm"));
                }

                AnsiConsole.Write(table);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load chain of custody report");
                AnsiConsole.MarkupLine("[red]✗ Failed to load report data[/]");
            }
        }

        private string DetermineMediaStatus(Guid mediaId, IReadOnlyList<VideoForensics.Data.Common.Entities.IntegrityRecord> integrityRecords)
        {
            var record = integrityRecords.FirstOrDefault(r => r.MediaItemId == mediaId);

            if (record == null)
            {
                return "[yellow]⚠ Not verified[/]";
            }

            if (!record.Passed)
            {
                return "[red]Integrity failed[/]";
            }

            return "[green]✓ Verified[/]";
        }
    }
}
