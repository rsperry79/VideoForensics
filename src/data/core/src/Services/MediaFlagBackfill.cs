using Microsoft.Extensions.Logging;

using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Core.Contracts;

namespace VideoForensics.Data.Core.Services
{
    /// <summary>
    /// Raises BitrateCollapse review flags for clips that analysis flagged before flags existed. It reads the stored RedFlags
    /// string only, so no analysis runs again. Only clips recorded in the last <see cref="WindowDays"/> days are covered.
    /// </summary>
    /// <remarks>
    /// A clip that throws is logged and skipped, so one bad row does not stop the pass. Each upsert refreshes an existing
    /// Unreviewed row, so a second pass changes nothing.
    /// </remarks>
    public sealed class MediaFlagBackfill : IMediaFlagBackfill
    {
        /// <summary>How far back the backfill looks. A named constant for the testing phase; older clips get no flag.</summary>
        public const int WindowDays = 7;

        private readonly IMediaItemWriteRepository _items;
        private readonly IMediaFlagWriteRepository _flags;
        private readonly TimeProvider _time;
        private readonly ILogger<MediaFlagBackfill> _logger;

        public MediaFlagBackfill(IMediaItemWriteRepository items, IMediaFlagWriteRepository flags, TimeProvider time, ILogger<MediaFlagBackfill> logger)
        {
            _items = items;
            _flags = flags;
            _time = time;
            _logger = logger;
        }

        /// <inheritdoc />
        public async Task<int> BackfillAsync(CancellationToken ct)
        {
            DateTime now = _time.GetUtcNow().UtcDateTime;
            IReadOnlyList<MediaItem> clips = await _items.ListFlaggedSinceAsync(now.AddDays(-WindowDays), ct);
            int upserted = 0;
            int removed = 0;
            foreach (MediaItem clip in clips)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    // A still has no bit-rate flag to raise. Reconciling with an empty set removes any unreviewed one left
                    // from before; reviewed rows are kept by the repository.
                    if (MediaStillRules.IsStill(clip.FileName))
                    {
                        removed += await _flags.ReconcileForMediaAsync(clip.Id, new HashSet<MediaFlagType>(), ct);
                        continue;
                    }

                    _ = await _flags.UpsertRaisedAsync(new MediaFlag
                    {
                        Id = Guid.NewGuid(),
                        FlagType = MediaFlagType.BitrateCollapse,
                        DeviceId = clip.DeviceId,
                        MediaItemId = clip.Id,
                        WindowStartUtc = clip.RecordedAtUtc,
                        // A window that ends before it starts is rejected by the repository, so a missing or non-positive
                        // duration collapses the window to the recording instant.
                        WindowEndUtc = clip.DurationSeconds is { } seconds && seconds > 0
                            ? clip.RecordedAtUtc.AddSeconds(seconds)
                            : clip.RecordedAtUtc,
                        RaisedAtUtc = now,
                        Severity = MediaFlagSeverity.Warn,
                    }, ct);
                    upserted++;
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    // No stack trace and no GUID: the file name and exception type identify the failure for the operator.
                    _logger.LogWarning("Flag backfill could not raise a flag for {FileName} ({ExceptionType}): {Reason}",
                        clip.FileName, ex.GetType().Name, ex.Message);
                }
            }

            if (upserted > 0)
            {
                _logger.LogInformation("Flag backfill raised {Upserted} BitrateCollapse flag(s) for clips recorded in the last {WindowDays} day(s)",
                    upserted, WindowDays);
            }

            if (removed > 0)
            {
                _logger.LogInformation("Flag backfill removed {Removed} unreviewed BitrateCollapse flag(s) from still images", removed);
            }

            return upserted;
        }
    }
}
