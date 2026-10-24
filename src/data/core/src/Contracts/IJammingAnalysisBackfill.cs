namespace VideoForensics.Data.Core.Contracts
{
    /// <summary>Runs media analysis on items that have never been analysed: old clips, and rows written without analysis.</summary>
    public interface IJammingAnalysisBackfill
    {
        /// <summary>Analyses one batch of unanalysed media. Returns how many items were analysed.</summary>
        Task<int> BackfillAsync(CancellationToken ct);
    }
}
