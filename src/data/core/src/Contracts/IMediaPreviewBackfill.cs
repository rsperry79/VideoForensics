namespace VideoForensics.Data.Core.Contracts
{
    /// <summary>Generates preview MP4s for archived videos that were downloaded before previews existed.</summary>
    public interface IMediaPreviewBackfill
    {
        /// <summary>
        /// Ensures a preview exists for every non-purged video that lacks one, working in batches of
        /// <paramref name="batchSize"/> until no unattempted video is left. A clip that fails is counted once and not retried
        /// within the same run.
        /// </summary>
        Task<MediaPreviewBackfillResult> RunAsync(int batchSize, CancellationToken ct);
    }

    /// <summary>Outcome of one preview backfill run.</summary>
    /// <param name="Processed">Videos attempted in this run.</param>
    /// <param name="Generated">Attempted videos that have a preview on disk afterwards.</param>
    /// <param name="Failed">Attempted videos whose preview could not be made, or that threw while being processed.</param>
    public sealed record MediaPreviewBackfillResult(int Processed, int Generated, int Failed);
}
