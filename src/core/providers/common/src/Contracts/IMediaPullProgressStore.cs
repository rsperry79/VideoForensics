namespace VideoForensics.Core.Providers.Common.Contracts
{
    /// <summary>
    /// Persists the pull watermark (the last time media was fully pulled for an account). The shared pull reads it to
    /// resume and writes it after each completed batch, so a run that stops mid-way keeps the progress it made.
    /// </summary>
    public interface IMediaPullProgressStore
    {
        /// <summary>The watermark, or null when no pull has completed yet (the initial backfill case).</summary>
        Task<DateTime?> GetWatermarkUtcAsync(CancellationToken cancellationToken);

        /// <summary>Stores the watermark. Called only after a batch is fully pulled, never past an unpulled event.</summary>
        Task SetWatermarkUtcAsync(DateTime watermarkUtc, CancellationToken cancellationToken);
    }
}
