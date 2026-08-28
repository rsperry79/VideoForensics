using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using VideoForensics.Data.Common.Contracts;

namespace VideoForensics.Mcp.Tools
{
    /// <summary>Phase 1 forensics tools: Timeline & Pattern Analysis</summary>
    [McpServerToolType]
    public class TimelineTools
    {
        private readonly ITimelineRepository _timelineRepository;
        private readonly ILogger<TimelineTools> _logger;

        public TimelineTools(ITimelineRepository timelineRepository, ILogger<TimelineTools> logger)
        {
            _timelineRepository = timelineRepository;
            _logger = logger;
        }

        /// <summary>Get quick timeline health summary for fast forensic decisions. Use this first to decide if detailed analysis is needed.</summary>
        [McpServerTool]
        public async Task<TimelineSummary> GetTimelineSummary(
            Guid locationId,
            DateTime fromUtc,
            DateTime toUtc,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("GetTimelineSummary: location={LocationId}, period={FromUtc:yyyy-MM-dd} to {ToUtc:yyyy-MM-dd}",
                locationId, fromUtc, toUtc);
            return await _timelineRepository.GetTimelineSummaryAsync(locationId, fromUtc, toUtc, cancellationToken);
        }

        /// <summary>Get recording gaps with full details (offset-based pagination). Use after summary indicates anomalies.</summary>
        [McpServerTool]
        public async Task<PaginatedResult<TimelineGap>> GetRecordingGapsPaginated(
            Guid deviceId,
            DateTime fromUtc,
            DateTime toUtc,
            int minGapMinutes,
            int pageNumber = 1,
            int pageSize = 100,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("GetRecordingGapsPaginated: device={DeviceId}, page={PageNumber}/{PageSize}, minGap={MinGapMinutes}m",
                deviceId, pageNumber, pageSize, minGapMinutes);
            return await _timelineRepository.GetRecordingGapsPaginatedAsync(deviceId, fromUtc, toUtc, minGapMinutes, pageNumber, pageSize, cancellationToken);
        }

        /// <summary>Stream recording gaps with cursor pagination (for large datasets). Use cursor from previous call to continue streaming.</summary>
        [McpServerTool]
        public async Task<CursorPaginatedResult<TimelineGap>> GetRecordingGapsCursor(
            Guid deviceId,
            DateTime fromUtc,
            DateTime toUtc,
            int minGapMinutes,
            string? cursor = null,
            int pageSize = 1000,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("GetRecordingGapsCursor: device={DeviceId}, cursor={Cursor}, pageSize={PageSize}",
                deviceId, cursor ?? "null", pageSize);
            return await _timelineRepository.GetRecordingGapsCursorAsync(deviceId, fromUtc, toUtc, minGapMinutes, cursor, pageSize, cancellationToken);
        }

        /// <summary>Get hourly event distribution for activity heatmaps.</summary>
        [McpServerTool]
        public async Task<Dictionary<int, int>> GetEventCountByHour(
            Guid deviceId,
            DateTime fromUtc,
            DateTime toUtc,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("GetEventCountByHour: device={DeviceId}, period={FromUtc:yyyy-MM-dd} to {ToUtc:yyyy-MM-dd}",
                deviceId, fromUtc, toUtc);
            return await _timelineRepository.GetEventCountByHourAsync(deviceId, fromUtc, toUtc, cancellationToken);
        }

        /// <summary>Get daily event totals for trend analysis.</summary>
        [McpServerTool]
        public async Task<Dictionary<string, int>> GetEventCountByDay(
            Guid locationId,
            DateTime fromUtc,
            DateTime toUtc,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("GetEventCountByDay: location={LocationId}, period={FromUtc:yyyy-MM-dd} to {ToUtc:yyyy-MM-dd}",
                locationId, fromUtc, toUtc);
            return await _timelineRepository.GetEventCountByDayAsync(locationId, fromUtc, toUtc, cancellationToken);
        }

        /// <summary>Get peak activity periods (hours with most events).</summary>
        [McpServerTool]
        public async Task<IReadOnlyList<(int Hour, int Count)>> GetPeakActivityPeriods(
            Guid locationId,
            DateTime fromUtc,
            DateTime toUtc,
            int topN = 5,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("GetPeakActivityPeriods: location={LocationId}, topN={TopN}", locationId, topN);
            return await _timelineRepository.GetPeakActivityPeriodsAsync(locationId, fromUtc, toUtc, cancellationToken);
        }

        /// <summary>Verify timeline integrity: compute coverage %, detect significant gaps, analyze event distribution.</summary>
        [McpServerTool]
        public async Task<TimelineIntegrityReport> VerifyTimelineIntegrity(
            Guid locationId,
            DateTime fromUtc,
            DateTime toUtc,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("VerifyTimelineIntegrity: location={LocationId}", locationId);
            return await _timelineRepository.VerifyTimelineIntegrityAsync(locationId, fromUtc, toUtc, cancellationToken);
        }

        /// <summary>Find events from multiple devices occurring within a time window (coordinated activity clustering).</summary>
        [McpServerTool]
        public async Task<IReadOnlyList<CoordinatedEventCluster>> GetCoordinatedEvents(
            Guid locationId,
            DateTime fromUtc,
            DateTime toUtc,
            int timeWindowSeconds = 60,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("GetCoordinatedEvents: location={LocationId}, window={WindowSeconds}s", locationId, timeWindowSeconds);
            return await _timelineRepository.GetCoordinatedEventsAsync(locationId, fromUtc, toUtc, timeWindowSeconds, cancellationToken);
        }

        /// <summary>Flag suspicious coordinated activity patterns (multi-device simultaneous events, potential tampering).</summary>
        [McpServerTool]
        public async Task<IReadOnlyList<SuspiciousActivityFlag>> FindSuspiciousCoordinatedActivity(
            Guid locationId,
            DateTime fromUtc,
            DateTime toUtc,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("FindSuspiciousCoordinatedActivity: location={LocationId}", locationId);
            return await _timelineRepository.FindSuspiciousCoordinatedActivityAsync(locationId, fromUtc, toUtc, cancellationToken);
        }
    }
}
