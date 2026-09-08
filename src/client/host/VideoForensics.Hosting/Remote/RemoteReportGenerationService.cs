using System.Net.Http.Json;
using System.Text.Json;

using VideoForensics.Api.Contracts;
using VideoForensics.Data.Core.Contracts;
using VideoForensics.Data.Core.Models;

namespace VideoForensics.Hosting.Remote
{
    /// <summary>
    /// HTTP-backed <see cref="IReportGenerationService"/> that calls the server's Minimal API
    /// (see VideoForensics.WebApp/Api/ReportEndpoints.cs) instead of a local database - the MAUI
    /// client's implementation of the "thin client talks to a server API" half of the plan's client/
    /// server split (§4/M5). All report generation methods call corresponding `/api/v1/reports/...`
    /// GET endpoints; WriteReportAsync calls POST `/api/v1/reports/write`.
    /// </summary>
    public class RemoteReportGenerationService : IReportGenerationService
    {
        private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

        private readonly HttpClient _httpClient;

        public RemoteReportGenerationService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        /// <inheritdoc />
        public async Task<EvidenceReviewReport> BuildEvidenceReviewAsync(
            Guid? deviceId,
            DateTime fromUtc,
            DateTime toUtc,
            CancellationToken ct)
        {
            var queryString = BuildQueryString(deviceId, fromUtc, toUtc);
            HttpResponseMessage response = await _httpClient.GetAsync($"/api/v1/reports/evidence-review{queryString}", ct);
            _ = response.EnsureSuccessStatusCode();
            EvidenceReviewReportDto? dto = await response.Content.ReadFromJsonAsync<EvidenceReviewReportDto>(JsonOptions, ct);
            return (dto ?? throw new InvalidOperationException("Response was null")).ToDomain();
        }

        /// <inheritdoc />
        public async Task<ForensicAnalysisReport> BuildForensicAnalysisReportAsync(
            Guid? deviceId,
            DateTime fromUtc,
            DateTime toUtc,
            CancellationToken ct)
        {
            var queryString = BuildQueryString(deviceId, fromUtc, toUtc);
            HttpResponseMessage response = await _httpClient.GetAsync($"/api/v1/reports/forensic-analysis{queryString}", ct);
            _ = response.EnsureSuccessStatusCode();
            ForensicAnalysisReportDto? dto = await response.Content.ReadFromJsonAsync<ForensicAnalysisReportDto>(JsonOptions, ct);
            return (dto ?? throw new InvalidOperationException("Response was null")).ToDomain();
        }

        /// <inheritdoc />
        public async Task<SignalAnomalyReport> BuildSignalAnomalyReportAsync(
            Guid? deviceId,
            DateTime fromUtc,
            DateTime toUtc,
            CancellationToken ct)
        {
            var queryString = BuildQueryString(deviceId, fromUtc, toUtc);
            HttpResponseMessage response = await _httpClient.GetAsync($"/api/v1/reports/signal-anomaly{queryString}", ct);
            _ = response.EnsureSuccessStatusCode();
            SignalAnomalyReportDto? dto = await response.Content.ReadFromJsonAsync<SignalAnomalyReportDto>(JsonOptions, ct);
            return (dto ?? throw new InvalidOperationException("Response was null")).ToDomain();
        }

        /// <inheritdoc />
        public async Task<AccessControlReport> BuildAccessControlReportAsync(
            Guid? deviceId,
            DateTime fromUtc,
            DateTime toUtc,
            CancellationToken ct)
        {
            var queryString = BuildQueryString(deviceId, fromUtc, toUtc);
            HttpResponseMessage response = await _httpClient.GetAsync($"/api/v1/reports/access-control{queryString}", ct);
            _ = response.EnsureSuccessStatusCode();
            AccessControlReportDto? dto = await response.Content.ReadFromJsonAsync<AccessControlReportDto>(JsonOptions, ct);
            return (dto ?? throw new InvalidOperationException("Response was null")).ToDomain();
        }

        /// <inheritdoc />
        public async Task<ChainOfCustodyReport> BuildChainOfCustodyReportAsync(
            Guid? deviceId,
            DateTime fromUtc,
            DateTime toUtc,
            CancellationToken ct)
        {
            var queryString = BuildQueryString(deviceId, fromUtc, toUtc);
            HttpResponseMessage response = await _httpClient.GetAsync($"/api/v1/reports/chain-of-custody{queryString}", ct);
            _ = response.EnsureSuccessStatusCode();
            ChainOfCustodyReportDto? dto = await response.Content.ReadFromJsonAsync<ChainOfCustodyReportDto>(JsonOptions, ct);
            return (dto ?? throw new InvalidOperationException("Response was null")).ToDomain();
        }

        /// <inheritdoc />
        public async Task WriteReportAsync(
            object reportDto,
            string format,
            CancellationToken ct)
        {
            // Anonymous type matching WriteReportRequest from ReportEndpoints
            var request = new { ReportDto = reportDto, Format = format };
            HttpResponseMessage response = await _httpClient.PostAsJsonAsync("/api/v1/reports/write", request, JsonOptions, ct);
            _ = response.EnsureSuccessStatusCode();
        }

        private static string BuildQueryString(Guid? deviceId, DateTime fromUtc, DateTime toUtc)
        {
            var parameters = new List<string>();
            if (deviceId.HasValue)
            {
                parameters.Add($"deviceId={deviceId.Value:D}");
            }
            parameters.Add($"fromUtc={fromUtc:O}");
            parameters.Add($"toUtc={toUtc:O}");
            return "?" + string.Join("&", parameters);
        }
    }

    /// <summary>Extension methods for mapping report DTOs back to domain models.</summary>
    internal static class ReportDtoToDomainMapping
    {
        /// <summary>Converts a DeviceHealthSnapshotDto to a DeviceHealthSnapshot entity.</summary>
        internal static VideoForensics.Data.Common.Entities.DeviceHealthSnapshot ToDomain(this DeviceHealthSnapshotDto dto)
        {
            return new VideoForensics.Data.Common.Entities.DeviceHealthSnapshot
            {
                Id = dto.Id,
                DeviceId = dto.DeviceId,
                DownloadEventId = dto.DownloadEventId,
                Connected = dto.Connected,
                BatteryPercentage = dto.BatteryPercentage,
                Rssi = dto.Rssi,
                WifiName = dto.WifiName,
                FirmwareVersion = dto.FirmwareVersion,
                CapturedAtUtc = dto.CapturedAtUtc
            };
        }

        /// <summary>Converts an ActionLogEntryDto to an ActionLogEntry entity.</summary>
        internal static VideoForensics.Data.Common.Entities.ActionLogEntry ToDomain(this ActionLogEntryDto dto)
        {
            return new VideoForensics.Data.Common.Entities.ActionLogEntry
            {
                Id = dto.Id,
                Actor = dto.Actor,
                ActorType = Enum.Parse<VideoForensics.Data.Common.Entities.ActorType>(dto.ActorType),
                Action = dto.Action,
                EntityType = dto.EntityType,
                EntityId = dto.EntityId,
                DetailsJson = dto.DetailsJson,
                TimestampUtc = dto.TimestampUtc,
                PreviousEntryHash = dto.PreviousEntryHash,
                EntryHash = dto.EntryHash
            };
        }

        /// <summary>Converts an EvidenceReviewReportDto to an EvidenceReviewReport model.</summary>
        internal static EvidenceReviewReport ToDomain(this EvidenceReviewReportDto dto)
        {
            return new EvidenceReviewReport
            {
                GeneratedAtUtc = dto.GeneratedAtUtc,
                ReportFromUtc = dto.ReportFromUtc,
                ReportToUtc = dto.ReportToUtc,
                MediaItems = dto.MediaItems.Select(x => x.ToDomain()).ToList().AsReadOnly(),
                IntegrityRecords = dto.IntegrityRecords.Select(x => x.ToDomain()).ToList().AsReadOnly(),
                TotalItemCount = dto.TotalItemCount,
                VerifiedItemCount = dto.VerifiedItemCount,
                FailedVerificationCount = dto.FailedVerificationCount
            };
        }

        /// <summary>Converts a ForensicAnalysisReportDto to a ForensicAnalysisReport model.</summary>
        internal static ForensicAnalysisReport ToDomain(this ForensicAnalysisReportDto dto)
        {
            return new ForensicAnalysisReport
            {
                GeneratedAtUtc = dto.GeneratedAtUtc,
                ReportFromUtc = dto.ReportFromUtc,
                ReportToUtc = dto.ReportToUtc,
                EvidenceItems = dto.EvidenceItems.Select(x => x.ToDomain()).ToList().AsReadOnly(),
                AnomalousHealthSnapshots = dto.AnomalousHealthSnapshots.Select(x => x.ToDomain()).ToList().AsReadOnly(),
                SignificantActions = dto.SignificantActions.Select(x => x.ToDomain()).ToList().AsReadOnly(),
                Summary = dto.Summary
            };
        }

        /// <summary>Converts a SignalAnomalyReportDto to a SignalAnomalyReport model.</summary>
        internal static SignalAnomalyReport ToDomain(this SignalAnomalyReportDto dto)
        {
            return new SignalAnomalyReport
            {
                GeneratedAtUtc = dto.GeneratedAtUtc,
                ReportFromUtc = dto.ReportFromUtc,
                ReportToUtc = dto.ReportToUtc,
                AnomaliesByDevice = dto.AnomaliesByDevice.Select(x => x.ToDomain()).ToList().AsReadOnly(),
                JammingByDevice = dto.JammingByDevice.Select(x => x.ToDomain()).ToList().AsReadOnly()
            };
        }

        private static SignalAnomalyReport.AnomalyFindings ToDomain(this AnomalyFindingsDto dto)
        {
            return new SignalAnomalyReport.AnomalyFindings
            {
                DeviceId = dto.DeviceId,
                DeviceName = dto.DeviceName,
                Anomalies = dto.Anomalies.Select(x => x.ToDomain()).ToList().AsReadOnly()
            };
        }

        private static SignalAnomalyReport.SignalAnomaly ToDomain(this SignalAnomalyDto dto)
        {
            return new SignalAnomalyReport.SignalAnomaly
            {
                OccurredAtUtc = dto.OccurredAtUtc,
                AnomalyType = dto.AnomalyType,
                Description = dto.Description,
                RssiValue = dto.RssiValue
            };
        }

        private static SignalAnomalyReport.JammingSummaryEntry ToDomain(this JammingSummaryEntryDto dto)
        {
            return new SignalAnomalyReport.JammingSummaryEntry
            {
                DeviceId = dto.DeviceId,
                DeviceName = dto.DeviceName,
                IncidentCount = dto.IncidentCount,
                TotalJammedDurationMinutes = dto.TotalJammedDurationMinutes,
                AverageDegradationDb = dto.AverageDegradationDb,
                MaxDegradationDb = dto.MaxDegradationDb,
                FirstIncidentUtc = dto.FirstIncidentUtc,
                LastIncidentUtc = dto.LastIncidentUtc
            };
        }

        /// <summary>Converts an AccessControlReportDto to an AccessControlReport model.</summary>
        internal static AccessControlReport ToDomain(this AccessControlReportDto dto)
        {
            return new AccessControlReport
            {
                GeneratedAtUtc = dto.GeneratedAtUtc,
                ReportFromUtc = dto.ReportFromUtc,
                ReportToUtc = dto.ReportToUtc,
                AccessEvents = dto.AccessEvents.Select(x => x.ToDomain()).ToList().AsReadOnly(),
                ExportEvents = dto.ExportEvents.Select(x => x.ToDomain()).ToList().AsReadOnly()
            };
        }

        private static AccessControlReport.AccessEvent ToDomain(this AccessEventDto dto)
        {
            return new AccessControlReport.AccessEvent
            {
                AccessedAtUtc = dto.AccessedAtUtc,
                Actor = dto.Actor,
                Action = dto.Action,
                EntityType = dto.EntityType,
                EntityId = dto.EntityId,
                Details = dto.Details
            };
        }

        private static AccessControlReport.ExportEvent ToDomain(this ExportEventDto dto)
        {
            return new AccessControlReport.ExportEvent
            {
                ExportedAtUtc = dto.ExportedAtUtc,
                ExportedByUserName = dto.ExportedByUserName,
                CaseReference = dto.CaseReference,
                RecipientDescription = dto.RecipientDescription,
                ItemCount = dto.ItemCount
            };
        }

        /// <summary>Converts a ChainOfCustodyReportDto to a ChainOfCustodyReport model.</summary>
        internal static ChainOfCustodyReport ToDomain(this ChainOfCustodyReportDto dto)
        {
            return new ChainOfCustodyReport
            {
                GeneratedAtUtc = dto.GeneratedAtUtc,
                ReportFromUtc = dto.ReportFromUtc,
                ReportToUtc = dto.ReportToUtc,
                AuditTrail = dto.AuditTrail.Select(x => x.ToDomain()).ToList().AsReadOnly(),
                ChainIntegrityVerified = dto.ChainIntegrityVerified,
                ChainVerificationStatus = dto.ChainVerificationStatus
            };
        }
    }
}
