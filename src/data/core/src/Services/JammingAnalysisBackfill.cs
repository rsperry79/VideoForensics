using Microsoft.Extensions.Logging;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Core.Services
{
    /// <summary>Runs media analysis on items that have never been analysed: old clips, and rows written without analysis.</summary>
    public interface IJammingAnalysisBackfill
    {
        /// <summary>Analyses one batch of unanalysed media. Returns how many items were analysed.</summary>
        Task<int> BackfillAsync(CancellationToken ct);
    }

    /// <summary>
    /// One batch per pass, oldest first. Analysis sets the baseline version on every result, so an analysed item drops out of
    /// the next pass. A single item that throws is logged and skipped, so one bad file does not stop the batch.
    /// </summary>
    public sealed class JammingAnalysisBackfill : IJammingAnalysisBackfill
    {
        /// <summary>Items per pass. Keeps one pass short; the next pass takes the next batch.</summary>
        public const int BatchSize = 50;

        private readonly IMediaItemRepository _items;
        private readonly IMediaAnalysisService _analysis;
        private readonly ILogger<JammingAnalysisBackfill> _logger;

        public JammingAnalysisBackfill(IMediaItemRepository items, IMediaAnalysisService analysis, ILogger<JammingAnalysisBackfill> logger)
        {
            _items = items;
            _analysis = analysis;
            _logger = logger;
        }

        /// <inheritdoc />
        public async Task<int> BackfillAsync(CancellationToken ct)
        {
            IReadOnlyList<MediaItem> batch = await _items.ListUnanalyzedAsync(BatchSize, ct);
            int analysed = 0;
            foreach (MediaItem item in batch)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    await _analysis.AnalyzeAsync(item.Id, ct);
                    analysed++;
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Analysis backfill could not analyse {FileName}; it will be retried on a later pass", item.FileName);
                }
            }

            if (analysed > 0)
            {
                _logger.LogInformation("Analysis backfill analysed {Analysed} of {Batch} unanalysed media item(s)", analysed, batch.Count);
            }

            return analysed;
        }
    }
}
