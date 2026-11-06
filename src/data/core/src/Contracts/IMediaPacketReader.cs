namespace VideoForensics.Data.Core.Contracts
{
    /// <summary>Reads the video packets of a media file, for the per-second bitrate series.</summary>
    public interface IMediaPacketReader
    {
        /// <summary>Start time (seconds) and size (bytes) of each video packet. Null when the file cannot be read.</summary>
        /// <param name="absolutePath">Full path of the media file.</param>
        /// <param name="ct">Cancellation token.</param>
        Task<IReadOnlyList<(double PtsSeconds, long SizeBytes)>?> ReadVideoPacketsAsync(string absolutePath, CancellationToken ct);
    }
}
