using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using VideoForensics.Data.Common.Contracts;

namespace VideoForensics.Mcp.Tools
{
    /// <summary>Phase 3 forensics tools: Event Correlation & Device Health Analysis</summary>
    [McpServerToolType]
    public class CorrelationTools
    {
        private readonly ICorrelationRepository _correlationRepository;
        private readonly ILogger<CorrelationTools> _logger;

        public CorrelationTools(ICorrelationRepository correlationRepository, ILogger<CorrelationTools> logger)
        {
            _correlationRepository = correlationRepository;
            _logger = logger;
        }

        /// <summary>Get quick correlation and sync health summary for fast forensic decisions. Use this first to decide if detailed analysis is needed.</summary>
        [McpServerTool]
        public async Task<CorrelationSummary> GetCorrelationSummary(
            Guid locationId,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("GetCorrelationSummary: location={LocationId}", locationId);
            return await _correlationRepository.GetCorrelationSummaryAsync(locationId, cancellationToken);
        }

        /// <summary>Get health-related gaps with full details (offset-based pagination). Use after summary indicates anomalies.</summary>
        [McpServerTool]
        public async Task<PaginatedResult<HealthRelatedGap>> GetHealthRelatedGapsPaginated(
            Guid locationId,
            int pageNumber = 1,
            int pageSize = 100,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("GetHealthRelatedGapsPaginated: location={LocationId}, page={PageNumber}/{PageSize}",
                locationId, pageNumber, pageSize);
            return await _correlationRepository.GetHealthRelatedGapsPaginatedAsync(locationId, pageNumber, pageSize, cancellationToken);
        }

        /// <summary>Stream event health correlations with cursor pagination (for large datasets). Use cursor from previous call to continue streaming.</summary>
        [McpServerTool]
        public async Task<CursorPaginatedResult<EventWithHealthCorrelation>> GetEventHealthCorrelationCursor(
            Guid deviceId,
            DateTime fromUtc,
            DateTime toUtc,
            string? cursor = null,
            int pageSize = 1000,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("GetEventHealthCorrelationCursor: device={DeviceId}, cursor={Cursor}, pageSize={PageSize}",
                deviceId, cursor ?? "null", pageSize);
            return await _correlationRepository.GetEventHealthCorrelationCursorAsync(deviceId, fromUtc, toUtc, cursor, pageSize, cancellationToken);
        }

        /// <summary>Analyze overall sync health for a location including device reliability.</summary>
        [McpServerTool]
        public async Task<SyncHealthReport> AnalyzeSyncHealth(
            Guid locationId,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("AnalyzeSyncHealth: location={LocationId}", locationId);
            return await _correlationRepository.AnalyzeSyncHealthAsync(locationId, cancellationToken);
        }

        /// <summary>Identify gaps caused by device health issues (low battery, poor signal, offline).</summary>
        [McpServerTool]
        public async Task<IReadOnlyList<HealthRelatedGap>> IdentifyHealthRelatedGaps(
            Guid locationId,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("IdentifyHealthRelatedGaps: location={LocationId}", locationId);
            return await _correlationRepository.IdentifyHealthRelatedGapsAsync(locationId, cancellationToken);
        }

        /// <summary>Get device reliability analysis (uptime vs event capture rate).</summary>
        [McpServerTool]
        public async Task<DeviceReliabilityAnalysis> AnalyzeDeviceReliability(
            Guid deviceId,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("AnalyzeDeviceReliability: device={DeviceId}", deviceId);
            return await _correlationRepository.AnalyzeDeviceReliabilityAsync(deviceId, cancellationToken);
        }
    }
}
