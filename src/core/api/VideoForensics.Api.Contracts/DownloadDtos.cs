namespace VideoForensics.Api.Contracts
{
    /// <summary>
    /// Request DTO for triggering a video download operation.
    /// </summary>
    /// <param name="OutputPath">The file system path where downloaded videos will be stored.</param>
    /// <param name="StartDate">The earliest date/time to include in the download range (inclusive).</param>
    /// <param name="EndDate">The latest date/time to include in the download range (inclusive).</param>
    /// <param name="Force">When false (default), only downloads media since the last successful pull per device via watermark. When true, scans the entire requested date window regardless of watermark history (e.g., for explicit re-scans).</param>
    public record DownloadVideosRequestDto(
        string OutputPath,
        DateTime StartDate,
        DateTime EndDate,
        bool Force = false
    );

    /// <summary>
    /// Request DTO for triggering a snapshot download operation.
    /// </summary>
    /// <param name="OutputPath">The file system path where downloaded snapshots will be stored.</param>
    /// <param name="StartDate">The earliest date/time to include in the download range (inclusive).</param>
    /// <param name="EndDate">The latest date/time to include in the download range (inclusive).</param>
    public record DownloadSnapshotsRequestDto(
        string OutputPath,
        DateTime StartDate,
        DateTime EndDate
    );

    /// <summary>
    /// Request DTO for pre-scanning matched item counts across devices without downloading.
    /// </summary>
    /// <param name="OutputPath">The output path context (used to cache results if a subsequent DownloadVideosAsync uses the same path/dates/force).</param>
    /// <param name="StartDate">The earliest date/time to include in the scan range (inclusive).</param>
    /// <param name="EndDate">The latest date/time to include in the scan range (inclusive).</param>
    /// <param name="Force">When false (default), only scans media since the last successful pull per device via watermark. When true, scans the entire requested date window regardless of watermark history.</param>
    public record PreScanRequestDto(
        string OutputPath,
        DateTime StartDate,
        DateTime EndDate,
        bool Force = false
    );

    /// <summary>
    /// Response DTO representing the live progress of a download in flight.
    /// Mirrors the structure of Providers.Common.Contracts.DownloadStatus.
    /// </summary>
    /// <param name="IsDownloading">True if a download operation is currently active.</param>
    /// <param name="FilesCompleted">Number of files completed in the current device batch.</param>
    /// <param name="FilesTotal">Total number of files queued for the current device batch.</param>
    /// <param name="BytesDownloaded">Bytes downloaded in the current device batch.</param>
    /// <param name="CurrentFile">Name of the file currently being downloaded, or null if none in progress.</param>
    /// <param name="TotalFilesCompleted">Cumulative files completed across all devices processed so far.</param>
    /// <param name="TotalFilesMatched">Total matched items discovered during the scan phase (across all devices).</param>
    /// <param name="TotalBytesDownloaded">Cumulative bytes downloaded across all devices.</param>
    /// <param name="ActiveConnections">Number of concurrent download connections currently open.</param>
    /// <param name="CurrentSpeedMbps">Current download speed in megabits per second.</param>
    public record DownloadStatusDto(
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

    /// <summary>
    /// Response DTO for GetDownloadStatus() endpoint.
    /// </summary>
    /// <param name="Status">Human-readable status message (e.g., "Downloading media for device 2 of 5").</param>
    public record DownloadStatusResponseDto(string Status);

    /// <summary>
    /// Response DTO for GetPreScanCounts() endpoint.
    /// Per-device matched-item counts discovered during the most recent PreScanAsync/DownloadVideosAsync call.
    /// </summary>
    /// <param name="Counts">Dictionary mapping provider device ID to matched item count.</param>
    public record PreScanCountsDto(IReadOnlyDictionary<string, int> Counts);

    /// <summary>
    /// Response DTO for the remaining items endpoint, combining GetRemainingCount() and GetRemainingReason().
    /// </summary>
    /// <param name="Count">Number of matched items not downloaded (e.g., due to rate limit cutoff).</param>
    /// <param name="Reason">Why items were not downloaded (e.g., "Rate limited by Ring API"), or null if unknown/not applicable.</param>
    public record RemainingDto(int Count, string? Reason);

    /// <summary>
    /// Response DTO for GetCurrentDevice() endpoint, indicating which device is currently being processed.
    /// </summary>
    /// <param name="Index">1-based index of the device currently being processed.</param>
    /// <param name="Total">Total number of devices being processed in this run.</param>
    /// <param name="Name">User-friendly name/identifier of the current device.</param>
    public record CurrentDeviceDto(int Index, int Total, string Name);

    /// <summary>
    /// Response DTO for triggering download or pre-scan operations (fire-and-forget acknowledgement).
    /// </summary>
    /// <param name="Success">True if the operation was successfully queued as a background task.</param>
    /// <param name="Message">Descriptive message about the operation result.</param>
    public record DownloadOperationResponseDto(bool Success, string Message);
}
