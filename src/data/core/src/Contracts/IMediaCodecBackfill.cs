namespace VideoForensics.Data.Core.Contracts
{
    /// <summary>Fills the codec, resolution and frame rate of existing media rows from an ffprobe pass.</summary>
    public interface IMediaCodecBackfill
    {
        /// <summary>Runs one batch. Returns the number of rows filled.</summary>
        Task<int> BackfillAsync(CancellationToken ct);
    }
}
