namespace VideoForensics.Providers.Common.Contracts
{
    /// <summary>Platform-agnostic media download interface</summary>
    public interface IMediaDownloadService
    {
        /// <summary>Downloads videos for a device within a date range</summary>
        Task<DownloadResult> DownloadVideosAsync(
            string deviceId,
            string outputPath,
            DateTime startDate,
            DateTime endDate,
            CancellationToken cancellationToken = default
        );

        /// <summary>Downloads snapshots for a device within a date range</summary>
        Task<DownloadResult> DownloadSnapshotsAsync(
            string deviceId,
            string outputPath,
            DateTime startDate,
            DateTime endDate,
            CancellationToken cancellationToken = default
        );

        /// <summary>Gets current download status</summary>
        DownloadStatus GetStatus();

        /// <summary>
        /// Returns and clears any per-item activity messages (e.g. "downloaded X", "failed: Y")
        /// queued since the last call, so a caller can poll this alongside GetStatus() to show a
        /// live feed of individual file outcomes during a download in progress.
        /// </summary>
        IReadOnlyList<string> DrainActivityLog() => Array.Empty<string>();
    }

    /// <summary>Result of a download operation</summary>
    public record DownloadResult(
        bool Success,
        int FilesDownloaded = 0,
        long BytesDownloaded = 0,
        string? ErrorMessage = null,
        int MetadataFilesWritten = 0,
        int MediaFilesValidated = 0,
        int MediaErrorsDetected = 0,
        int MediaErrorsCorrected = 0,
        List<string>? ValidatedFiles = null,
        List<string>? CorrectedFiles = null,
        /// <summary>
        /// How many items matched the query (e.g. events in range for this device), independent of
        /// how many actually got downloaded. Lets a caller detect "some were skipped" (rate limit,
        /// cancellation) via FilesMatched - FilesDownloaded, even when Success is true.
        /// </summary>
        int FilesMatched = 0
    );

    /// <summary>Metadata about a downloaded media file</summary>
    public record MediaMetadata(
        string FileName,
        string DeviceId,
        string DeviceName,
        DateTime RecordedAt,
        long FileSizeBytes,
        string MediaFormat,
        string? LocationId = null,
        string? LocationName = null,
        string? EventType = null,
        Dictionary<string, string>? CustomProperties = null
    );

    /// <summary>Current download status</summary>
    public record DownloadStatus(
        bool IsDownloading,
        int FilesCompleted,
        int FilesTotal,
        long BytesDownloaded,
        string? CurrentFile = null,
        int TotalFilesCompleted = 0,
        int TotalFilesMatched = 0,
        long TotalBytesDownloaded = 0,
        int ActiveConnections = 1,
        double CurrentSpeedMbps = 0
    );
}
