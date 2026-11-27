using Microsoft.Extensions.Logging;

using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Core.Contracts;

namespace VideoForensics.Data.Core.Services
{
    /// <summary>
    /// Marks existing repeat placeholder snapshots: a snapshot whose hash equals the one just before it on the same device.
    /// </summary>
    /// <remarks>
    /// Each batch is one set-based statement bounded by <see cref="BatchSize"/>, so a long backfill never holds the write gate for
    /// long and an interrupted run resumes from the rows still unmarked. A repeat already marked is not selected again, so a
    /// second run marks nothing. A failure is logged with its exception type and message (no stack trace) and the total so far
    /// is returned, so the hosted wrapper does not crash.
    /// </remarks>
    public sealed class PlaceholderSnapshotBackfill : IPlaceholderSnapshotBackfill
    {
        /// <summary>Rows marked per repository call.</summary>
        internal const int BatchSize = 500;

        private readonly IMediaItemWriteRepository _items;
        private readonly ILogger<PlaceholderSnapshotBackfill> _logger;

        /// <summary>Creates the backfill over the write repository.</summary>
        public PlaceholderSnapshotBackfill(IMediaItemWriteRepository items, ILogger<PlaceholderSnapshotBackfill> logger)
        {
            _items = items;
            _logger = logger;
        }

        /// <inheritdoc />
        public async Task<int> BackfillAsync(CancellationToken ct)
        {
            int marked = 0;
            try
            {
                while (true)
                {
                    ct.ThrowIfCancellationRequested();
                    int batch = await _items.MarkRepeatSnapshotPlaceholdersAsync(BatchSize, ct);
                    if (batch == 0)
                    {
                        break;
                    }

                    marked += batch;
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError("Placeholder snapshot backfill failed after {Marked} row(s) ({ExceptionType}): {Reason}",
                    marked, ex.GetType().Name, ex.Message);
                return marked;
            }

            _logger.LogInformation("Placeholder snapshot backfill marked {Marked} repeat frame(s)", marked);
            return marked;
        }
    }
}
