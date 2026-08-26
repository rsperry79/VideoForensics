using Microsoft.Extensions.Logging;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Core.Contracts;

namespace VideoForensics.Data.Core.Services
{
    /// <summary>Service for resolving incremental download start dates using watermarks.</summary>
    internal class WatermarkService : IWatermarkService
    {
        private readonly IDownloadEventRepository _downloadEventRepository;
        private readonly ILogger<WatermarkService> _logger;

        /// <summary>Buffer time (1 hour) subtracted from the watermark to catch in-flight events.</summary>
        private static readonly TimeSpan WatermarkBuffer = TimeSpan.FromHours(1);

        public WatermarkService(IDownloadEventRepository downloadEventRepository, ILogger<WatermarkService> logger)
        {
            _downloadEventRepository = downloadEventRepository;
            _logger = logger;
        }

        public async Task<DateTime> ResolveStartDateAsync(Guid deviceId, DateTime requestedStartDate, bool force, CancellationToken ct)
        {
            if (force)
            {
                _logger.LogInformation("Watermark resolution forced; using requested start date {RequestedDate:O}", requestedStartDate);
                return requestedStartDate;
            }

            var lastSuccessfulTime = await _downloadEventRepository.GetLatestSuccessfulEventTimeAsync(deviceId, ct);
            if (lastSuccessfulTime == null)
            {
                _logger.LogInformation("No prior successful download found for device {DeviceId}; using requested start date {RequestedDate:O}", deviceId, requestedStartDate);
                return requestedStartDate;
            }

            var watermarkWithBuffer = lastSuccessfulTime.Value.Subtract(WatermarkBuffer);
            var effectiveDate = new[] { requestedStartDate, watermarkWithBuffer }.Max();

            _logger.LogInformation(
                "Watermark resolution for device {DeviceId}: last successful={LastSuccessful:O}, buffer={Buffer}, effective={EffectiveDate:O}",
                deviceId,
                lastSuccessfulTime.Value,
                WatermarkBuffer,
                effectiveDate);

            return effectiveDate;
        }
    }
}
