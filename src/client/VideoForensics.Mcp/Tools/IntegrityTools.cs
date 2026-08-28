using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using VideoForensics.Data.Common.Contracts;

namespace VideoForensics.Mcp.Tools
{
    /// <summary>Phase 2 forensics tools: Evidence Integrity</summary>
    [McpServerToolType]
    public class IntegrityTools
    {
        private readonly IIntegrityRepository _integrityRepository;
        private readonly ILogger<IntegrityTools> _logger;

        public IntegrityTools(IIntegrityRepository integrityRepository, ILogger<IntegrityTools> logger)
        {
            _integrityRepository = integrityRepository;
            _logger = logger;
        }

        /// <summary>Get quick integrity summary for compliance decisions. Check this first.</summary>
        [McpServerTool]
        public async Task<IntegritySummary> GetIntegritySummary(
            Guid locationId,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("GetIntegritySummary: location={LocationId}", locationId);
            return await _integrityRepository.GetIntegritySummaryAsync(locationId, cancellationToken);
        }

        /// <summary>Get paginated tampering indicators ranked by suspicion score.</summary>
        [McpServerTool]
        public async Task<PaginatedResult<TamperingIndicator>> GetTamperingIndicatorsPaginated(
            Guid locationId,
            int pageNumber = 1,
            int pageSize = 100,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("GetTamperingIndicatorsPaginated: location={LocationId}, page={PageNumber}", locationId, pageNumber);
            return await _integrityRepository.GetTamperingIndicatorsPaginatedAsync(locationId, pageNumber, pageSize, cancellationToken);
        }

        /// <summary>Stream download audit history with cursor pagination.</summary>
        [McpServerTool]
        public async Task<CursorPaginatedResult<DownloadAuditRecord>> GetDownloadHistoryCursor(
            Guid deviceId,
            DateTime fromUtc,
            DateTime toUtc,
            string? cursor = null,
            int pageSize = 1000,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("GetDownloadHistoryCursor: device={DeviceId}, cursor={Cursor}", deviceId, cursor ?? "null");
            return await _integrityRepository.GetDownloadHistoryCursorAsync(deviceId, fromUtc, toUtc, cursor, pageSize, cancellationToken);
        }

        /// <summary>Compute overall integrity score for location (0-100%).</summary>
        [McpServerTool]
        public async Task<int> ComputeEventIntegrityScore(
            Guid locationId,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("ComputeEventIntegrityScore: location={LocationId}", locationId);
            return await _integrityRepository.ComputeEventIntegrityScoreAsync(locationId, cancellationToken);
        }

        /// <summary>Verify download completeness with missing event details.</summary>
        [McpServerTool]
        public async Task<DownloadCompletenessReport> VerifyDownloadCompleteness(
            Guid locationId,
            DateTime fromUtc,
            DateTime toUtc,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("VerifyDownloadCompleteness: location={LocationId}", locationId);
            return await _integrityRepository.VerifyDownloadCompletenessAsync(locationId, fromUtc, toUtc, cancellationToken);
        }

        /// <summary>Get all tampering indicators for location ranked by suspicion.</summary>
        [McpServerTool]
        public async Task<IReadOnlyList<TamperingIndicator>> GetTamperingIndicators(
            Guid locationId,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("GetTamperingIndicators: location={LocationId}", locationId);
            return await _integrityRepository.GetTamperingIndicatorsAsync(locationId, cancellationToken);
        }

        /// <summary>Verify event hashes for tampering detection.</summary>
        [McpServerTool]
        public async Task<IReadOnlyList<TamperingIndicator>> VerifyEventHashes(
            Guid deviceId,
            DateTime fromUtc,
            DateTime toUtc,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("VerifyEventHashes: device={DeviceId}", deviceId);
            return await _integrityRepository.VerifyEventHashesAsync(deviceId, fromUtc, toUtc, cancellationToken);
        }
    }
}
