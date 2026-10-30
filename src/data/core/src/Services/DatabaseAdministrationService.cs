using Microsoft.Extensions.Logging;

using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Core.Services
{
    /// <inheritdoc />
    public sealed class DatabaseAdministrationService : IDatabaseAdministrationService
    {
        // Bounds the repair loop so a pathological chain cannot append without limit. Real chains have a handful of gaps.
        private const int MaxBridgesPerRun = 1000;

        private readonly IDatabaseEngine _engine;
        private readonly IMediaItemRepository _mediaItems;
        private readonly ILegalHoldRepository _legalHolds;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IActionLogRepository _actionLog;
        private readonly IMediaPathResolver _paths;
        private readonly ILogger<DatabaseAdministrationService> _logger;

        /// <summary>Creates the service.</summary>
        public DatabaseAdministrationService(
            IDatabaseEngine engine,
            IMediaItemRepository mediaItems,
            ILegalHoldRepository legalHolds,
            IUnitOfWork unitOfWork,
            IActionLogRepository actionLog,
            ILogger<DatabaseAdministrationService> logger,
            IMediaPathResolver? paths = null)
        {
            _engine = engine;
            _mediaItems = mediaItems;
            _legalHolds = legalHolds;
            _unitOfWork = unitOfWork;
            _actionLog = actionLog;
            _logger = logger;
            _paths = paths ?? PassThroughMediaPathResolver.Instance;
        }

        /// <inheritdoc />
        public async Task<DatabaseStatsResult> GetStatsAsync(CancellationToken ct)
        {
            DatabaseFileStats file = await _engine.GetFileStatsAsync(ct);
            IReadOnlyList<MediaItem> items = await _mediaItems.ListAsync(ct);
            IReadOnlyList<ActionLogEntry> actionLog = await _actionLog.ListAsync(ct);
            return new DatabaseStatsResult(file, items.Count, items.Count(i => i.IsPurged), actionLog.Count);
        }

        /// <inheritdoc />
        public async Task CompactAsync(CancellationToken ct)
        {
            await _engine.CompactAsync(ct);
            _logger.LogInformation("Database compacted ({Engine})", _engine.Name);
        }

        /// <inheritdoc />
        public async Task<string> RepairAsync(CancellationToken ct)
        {
            string verdict = await _engine.CheckIntegrityAsync(ct);
            await _engine.RebuildIndexesAsync(ct);

            if (verdict == "ok")
            {
                _logger.LogInformation("Database repair finished: integrity check ok ({Engine})", _engine.Name);
            }
            else
            {
                _logger.LogError("Database repair finished: integrity check reported {Verdict} ({Engine})", verdict, _engine.Name);
            }

            return verdict;
        }

        /// <inheritdoc />
        public async Task<DatabaseValidationResult> ValidateAsync(CancellationToken ct)
        {
            var missing = new List<MissingMediaRecord>();
            foreach (MediaItem item in await _mediaItems.ListAsync(ct))
            {
                if (item.IsPurged)
                {
                    continue;
                }

                string resolved = await _paths.ToAbsoluteAsync(item.FilePath, ct);
                if (!File.Exists(resolved))
                {
                    _logger.LogError("Validation: media file missing for {FileName} at {Path}", item.FileName, resolved);
                    missing.Add(new MissingMediaRecord(item.Id, item.FileName, resolved));
                }
            }

            ChainIntegrityResult chain = await _actionLog.VerifyChainIntegrityAsync(ct);
            if (!chain.IsValid)
            {
                _logger.LogError("Validation: action log chain broken at entry {EntryId} ({Break})", chain.FailedEntryId, chain.Break);
            }

            return new DatabaseValidationResult(missing, chain);
        }

        /// <inheritdoc />
        public async Task<int> PurgeMissingMediaAsync(IReadOnlyCollection<Guid> mediaItemIds, CancellationToken ct)
        {
            // Same rule as retention: a row under legal hold is never purged.
            IReadOnlyList<LegalHold> holds = await _legalHolds.GetActiveByMediaItemIdsAsync(mediaItemIds, ct);
            var held = holds.Select(h => h.MediaItemId).ToHashSet();

            int purged = 0;
            foreach (Guid id in mediaItemIds.Where(id => !held.Contains(id)))
            {
                MediaItem? item = await _mediaItems.GetAsync(id, ct);
                if (item is null || item.IsPurged)
                {
                    continue;
                }

                // Re-check the file at purge time: a file restored since validation must not be purged.
                if (File.Exists(await _paths.ToAbsoluteAsync(item.FilePath, ct)))
                {
                    continue;
                }

                string reason = "Missing file: purged after database validation";
                _ = await _unitOfWork.ExecuteAsync(async context =>
                {
                    item.IsPurged = true;
                    item.PurgedAtUtc = DateTime.UtcNow;
                    item.PurgeReason = reason;

                    await context.MediaItems.UpdateAsync(item, ct);
                    _ = await context.ActionLog.AppendAsync(
                        Environment.UserName,
                        ActorType.Human,
                        "MediaPurged",
                        nameof(MediaItem),
                        item.Id,
                        $"Purge reason: {reason}",
                        ct);
                    return true;
                }, ct);

                purged++;
            }

            _logger.LogInformation("Validation purge: {Purged} of {Requested} missing media row(s) purged", purged, mediaItemIds.Count);
            return purged;
        }

        /// <inheritdoc />
        public async Task<ChainRepairResult> RepairChainAsync(CancellationToken ct)
        {
            int bridges = 0;
            while (bridges < MaxBridgesPerRun)
            {
                ChainIntegrityResult chain = await _actionLog.VerifyChainIntegrityAsync(ct);
                if (chain.IsValid)
                {
                    return new ChainRepairResult(bridges, null);
                }

                // Only a missing link is bridged. An entry whose own hash does not match its contents was altered, and a
                // bridge would hide that, so it is reported instead.
                if (chain.Break != ChainBreakKind.PreviousHashMismatch || chain.FailedEntryId is null)
                {
                    _logger.LogError("Chain repair stopped: entry {EntryId} was altered ({Break}); not bridged", chain.FailedEntryId, chain.Break);
                    return new ChainRepairResult(bridges, chain);
                }

                ChainRepairRecord.Bridge bridge = await BridgeFor(chain.FailedEntryId.Value, ct)
                    ?? throw new InvalidOperationException("Chain break is on the first entry, so there is no verified hash to bridge from.");

                _ = await _actionLog.AppendAsync(
                    Environment.UserName,
                    ActorType.Human,
                    ChainRepairRecord.Action,
                    nameof(ActionLogEntry),
                    null,
                    ChainRepairRecord.Serialize([bridge]),
                    ct);

                _logger.LogInformation("Chain repaired: gap before entry {EntryId} bridged by a logged repair", chain.FailedEntryId);
                bridges++;
            }

            return new ChainRepairResult(bridges, await _actionLog.VerifyChainIntegrityAsync(ct));
        }

        // The bridge names the last verified hash before the break and the hash the broken entry still records.
        private async Task<ChainRepairRecord.Bridge?> BridgeFor(Guid failedEntryId, CancellationToken ct)
        {
            // Same order the verifier walks the chain, so the entry before the break is the last one it verified.
            List<ActionLogEntry> ordered = (await _actionLog.ListAsync(ct))
                .OrderBy(e => e.TimestampUtc)
                .ThenBy(e => e.Id)
                .ToList();

            int index = ordered.FindIndex(e => e.Id == failedEntryId);
            if (index <= 0)
            {
                return null;
            }

            return new ChainRepairRecord.Bridge(ordered[index - 1].EntryHash, ordered[index].PreviousEntryHash ?? string.Empty);
        }
    }
}
