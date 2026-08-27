using Microsoft.Extensions.Logging;
using VideoForensics.Core.Logging.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Core.Contracts;

namespace VideoForensics.Data.Core.Services
{
    /// <summary>Service for purging media files based on retention policy.</summary>
    internal class RetentionService : IRetentionService
    {
        private readonly IMediaItemRepository _mediaItemRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IActionLogger _actionLogger;
        private readonly ILogger<RetentionService> _logger;
        private readonly int _retentionDays;

        public RetentionService(
            IMediaItemRepository mediaItemRepository,
            IUnitOfWork unitOfWork,
            IActionLogger actionLogger,
            ILogger<RetentionService> logger,
            int retentionDays = 90)
        {
            _mediaItemRepository = mediaItemRepository;
            _unitOfWork = unitOfWork;
            _actionLogger = actionLogger;
            _logger = logger;
            _retentionDays = retentionDays;
        }

        public async Task<int> PurgeExpiredAsync(CancellationToken ct)
        {
            try
            {
                var allItems = await _mediaItemRepository.ListAsync(ct);
                var cutoffDate = DateTime.UtcNow.AddDays(-_retentionDays);
                var itemsToPurge = allItems
                    .Where(m => !m.IsPurged && m.DownloadedAtUtc < cutoffDate)
                    .ToList();

                int purgedCount = 0;

                foreach (var item in itemsToPurge)
                {
                    try
                    {
                        await _unitOfWork.ExecuteAsync(async context =>
                        {
                            if (File.Exists(item.FilePath))
                            {
                                File.Delete(item.FilePath);
                                _logger.LogInformation("Deleted file during retention purge: {FilePath}", item.FilePath);
                            }

                            item.IsPurged = true;
                            item.PurgedAtUtc = DateTime.UtcNow;
                            item.PurgeReason = $"Retention policy: older than {_retentionDays} days";

                            await context.MediaItems.UpdateAsync(item, ct);
                            await context.ActionLog.AppendAsync(
                                Environment.UserName,
                                ActorType.Human,
                                "MediaPurged",
                                nameof(MediaItem),
                                item.Id,
                                $"Purge reason: {item.PurgeReason}",
                                ct);

                            _logger.LogInformation("Purged media item {MediaItemId}: {FileName}", item.Id, item.FileName);
                            purgedCount++;

                            return true;
                        }, ct);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error purging media item {MediaItemId}", item.Id);
                    }
                }

                _logger.LogInformation("Retention purge completed: {PurgedCount} items removed (retention threshold: {Days} days)",
                    purgedCount, _retentionDays);

                return purgedCount;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during retention purge");
                throw;
            }
        }
    }
}
