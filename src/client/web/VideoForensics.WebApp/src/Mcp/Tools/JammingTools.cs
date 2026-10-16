using System.Security.Claims;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;

using ModelContextProtocol.Server;

using VideoForensics.Client.Core.Tools;
using VideoForensics.Data.Common.Entities;
using VideoForensics.WebApp.Auth;

namespace VideoForensics.WebApp.Mcp.Tools
{
    /// <summary>RF jamming / signal interference detection tools. See the jamming-analysis resource for the recommended call sequence and interpretation guidance.</summary>
    [McpServerToolType]
    public class JammingTools : ForensicsToolBase
    {
        private readonly JammingToolsOrchestrator _orchestrator;
        private readonly IAuthorizationService _authorization;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public JammingTools(
            JammingToolsOrchestrator orchestrator,
            IAuthorizationService authorization,
            IHttpContextAccessor httpContextAccessor,
            ILogger<JammingTools> logger) : base(logger)
        {
            _orchestrator = orchestrator;
            _authorization = authorization;
            _httpContextAccessor = httpContextAccessor;
        }

        /// <summary>
        /// Both writing tools persist incidents (and can create cases), which the HTTP jamming endpoints allow only to
        /// Admin. MCP requests carry only baseline paired-device auth, so the caller's role is checked here.
        /// </summary>
        private async Task RequireAdminAsync()
        {
            ClaimsPrincipal? user = _httpContextAccessor.HttpContext?.User;
            if (user is null || !(await _authorization.AuthorizeAsync(user, null, VideoForensicsPolicies.Admin)).Succeeded)
            {
                throw new UnauthorizedAccessException("Recording jamming incidents requires Admin.");
            }
        }

        /// <summary>Detects jamming/interference from the device's RSSI health-snapshot history over the given window, persists any incidents found, and returns the full analysis. Fetch the jamming-analysis resource first for interpretation guidance.</summary>
        [McpServerTool]
        public async Task<JammingAnalysisReport> RunJammingDetection(
            Guid deviceId,
            DateTime fromUtc,
            DateTime toUtc,
            CancellationToken cancellationToken = default)
        {
            Logger.LogInformation("RunJammingDetection: device={DeviceId}, from={FromUtc}, to={ToUtc}", deviceId, fromUtc, toUtc);
            await RequireAdminAsync();
            return await _orchestrator.AnalyzeJammingAsync(deviceId, fromUtc, toUtc, cancellationToken);
        }

        /// <summary>Manually records or corrects a jamming incident based on independent human review. Tracked separately (Source=ManuallyRecorded) from auto-detected incidents for chain of custody.</summary>
        [McpServerTool]
        public async Task<RecordIncidentResult> RecordJammingIncident(
            Guid deviceId,
            DateTime startUtc,
            DateTime endUtc,
            int affectedEventCount,
            double averageDegradationDb,
            JammingConfidenceLevel confidence,
            string? notes = null,
            CancellationToken cancellationToken = default)
        {
            Logger.LogInformation("RecordJammingIncident: device={DeviceId}", deviceId);
            await RequireAdminAsync();
            (bool success, string? message, JammingIncidentRecord? record) = await _orchestrator.RecordJammingIncidentAsync(
                deviceId, startUtc, endUtc, affectedEventCount, averageDegradationDb, confidence, notes, cancellationToken);
            return new RecordIncidentResult { Success = success, Message = message, Record = record };
        }

        /// <summary>Gets the device's jamming summary (incident count, total duration, confidence breakdown). Confirm this reflects expectations before citing it anywhere.</summary>
        [McpServerTool]
        public async Task<JammingStatsSummary?> GetJammingStats(
            Guid deviceId,
            CancellationToken cancellationToken = default)
        {
            Logger.LogInformation("GetJammingStats: device={DeviceId}", deviceId);
            (bool _, JammingStatsSummary? stats) = await _orchestrator.GetJammingStatsAsync(deviceId, cancellationToken);
            return stats;
        }

        /// <summary>Lists jamming incidents, optionally filtered by device and/or time window.</summary>
        [McpServerTool]
        public async Task<IReadOnlyList<JammingIncidentRecord>> GetJammingIncidents(
            Guid? deviceId = null,
            DateTime? fromUtc = null,
            DateTime? toUtc = null,
            CancellationToken cancellationToken = default)
        {
            Logger.LogInformation("GetJammingIncidents: device={DeviceId}", deviceId);
            (bool _, IReadOnlyList<JammingIncidentRecord>? incidents) = await _orchestrator.GetJammingIncidentsAsync(deviceId, fromUtc, toUtc, cancellationToken);
            return incidents ?? [];
        }

        /// <summary>Result of a manual jamming incident record attempt.</summary>
        public class RecordIncidentResult
        {
            public bool Success { get; set; }
            public string Message { get; set; } = string.Empty;
            public JammingIncidentRecord? Record { get; set; }
        }
    }
}
