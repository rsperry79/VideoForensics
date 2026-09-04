namespace VideoForensics.Data.Common.Contracts
{
    /// <summary>
    /// Abstraction over where downloaded media file bytes actually live. Today's (M5) only
    /// implementation, LocalDiskMediaStorageProvider, is a thin wrapper over local-disk File I/O -
    /// the download pipeline itself (RingMediaDownloadService etc.) keeps writing files directly to
    /// disk exactly as it does today, unchanged. This seam exists specifically so the server's
    /// media-streaming API endpoint reads bytes through one abstraction rather than a hardcoded
    /// File.OpenRead call, so a future second implementation (secure network storage, plan §4.2/M8)
    /// only has to be plugged in here - the API surface and everything else stays the same.
    /// </summary>
    public interface IMediaStorageProvider
    {
        /// <summary>Opens a read stream for the media file at the given path (today: an absolute local-disk path, taken directly from MediaItem.FilePath).</summary>
        Task<Stream> OpenReadStreamAsync(string path, CancellationToken ct);

        /// <summary>True if the media file exists at the given path.</summary>
        Task<bool> ExistsAsync(string path, CancellationToken ct);

        /// <summary>Saves content to the given path, creating any needed directories. Not used by today's download pipeline (which still writes directly); exists for a future write path (e.g. a download-trigger API endpoint) to go through this same abstraction from day one.</summary>
        Task SaveAsync(string path, Stream content, CancellationToken ct);

        /// <summary>Deletes the media file at the given path, if it exists.</summary>
        Task DeleteAsync(string path, CancellationToken ct);
    }
}
