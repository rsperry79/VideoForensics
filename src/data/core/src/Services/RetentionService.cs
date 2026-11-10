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
        private readonly ILegalHoldRepository _legalHoldRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IActionLogger _actionLogger;
        private readonly IActionLogRepository _actionLogRepository;
        private readonly ILogger<RetentionService> _logger;
        private readonly int _retentionDays;
        private readonly IMediaPathResolver _paths;

        public RetentionService(
            IMediaItemRepository mediaItemRepository,
            ILegalHoldRepository legalHoldRepository,
            IUnitOfWork unitOfWork,
            IActionLogger actionLogger,
            ILogger<RetentionService> logger,
            IActionLogRepository actionLogRepository,
            int retentionDays = 90,
            IMediaPathResolver? paths = null)
        {
            _mediaItemRepository = mediaItemRepository;
            _legalHoldRepository = legalHoldRepository;
            _unitOfWork = unitOfWork;
            _actionLogger = actionLogger;
            _logger = logger;
            _actionLogRepository = actionLogRepository;
            _retentionDays = retentionDays;
            _paths = paths ?? PassThroughMediaPathResolver.Instance;
        }

        public async Task<int> PurgeExpiredAsync(CancellationToken ct)
        {
            try
            {
                IReadOnlyList<MediaItem> allItems = await _mediaItemRepository.ListAsync(ct);
                DateTime cutoffDate = DateTime.UtcNow.AddDays(-_retentionDays);
                var itemsToPurge = allItems
                    .Where(m => !m.IsPurged && m.DownloadedAtUtc < cutoffDate)
                    .ToList();

                IReadOnlyList<LegalHold> activeHolds = await _legalHoldRepository.GetActiveByMediaItemIdsAsync(itemsToPurge.Select(i => i.Id), ct);
                var heldMediaItemIds = activeHolds.Select(h => h.MediaItemId).ToHashSet();

                if (heldMediaItemIds.Count > 0)
                {
                    int skippedCount = itemsToPurge.Count(i => heldMediaItemIds.Contains(i.Id));
                    _logger.LogInformation("Skipping {SkippedCount} item(s) under active legal hold during retention purge", skippedCount);
                    itemsToPurge = itemsToPurge.Where(i => !heldMediaItemIds.Contains(i.Id)).ToList();
                }

                int purgedCount = 0;

                foreach (MediaItem? item in itemsToPurge)
                {
                    try
                    {
                        string? clipPath = null;
                        string? thumbnailPath = null;

                        _ = await _unitOfWork.ExecuteAsync(async context =>
                        {
                            clipPath = await _paths.ToAbsoluteAsync(item.FilePath, ct);
                            // The stored thumbnail is derived from the clip, so it goes too, even if the clip was already gone.
                            thumbnailPath = MediaThumbnailService.ThumbnailPathFor(clipPath);

                            item.IsPurged = true;
                            item.PurgedAtUtc = DateTime.UtcNow;
                            item.PurgeReason = $"Retention policy: older than {_retentionDays} days";

                            await _mediaItemRepository.UpdateAsync(item, ct);
                            _ = await _actionLogRepository.AppendAsync(
                                Environment.UserName,
                                ActorType.Human,
                                "MediaPurged",
                                nameof(MediaItem),
                                item.Id,
                                $"Purge reason: {item.PurgeReason}",
                                ct);

                            _logger.LogDebug("Purged media item {MediaItemId}: {FileName}", item.Id, item.FileName);
                            purgedCount++;

                            return true;
                        }, ct);

                        // Files are removed only after the commit, so a rolled-back purge never deletes evidence.
                        if (clipPath is not null && thumbnailPath is not null)
                        {
                            if (DeletePurgedFile(item, clipPath))
                            {
                                _logger.LogDebug("Deleted file during retention purge: {FilePath}", clipPath);
                            }

                            _ = DeletePurgedFile(item, thumbnailPath);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError("Error purging media item {MediaItemId}: {Message} ({ErrorType})", item.Id, ex.Message, ex.GetType().Name);
                    }
                }

                _logger.LogInformation("Retention purge completed: {PurgedCount} items removed (retention threshold: {Days} days)",
                    purgedCount, _retentionDays);

                return purgedCount;
            }
            catch (Exception ex)
            {
                _logger.LogError("Error during retention purge: {Message} ({ErrorType})", ex.Message, ex.GetType().Name);
                throw;
            }
        }

        /// <summary>
        /// Deletes a file whose purge has already committed. Returns true if a file was deleted.
        /// A failure is logged, not thrown: the purge stands, and the leftover file is an orphan that a later sweep can remove.
        /// </summary>
        private bool DeletePurgedFile(MediaItem item, string path)
        {
            try
            {
                if (!File.Exists(path))
                {
                    return false;
                }

                File.Delete(path);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Could not delete {FileName} after purging media item {MediaItemId}: {Message} ({ErrorType})",
                    Path.GetFileName(path), item.Id, ex.Message, ex.GetType().Name);
                return false;
            }
        }
    }
}
