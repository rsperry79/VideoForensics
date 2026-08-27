namespace VideoForensics.Client.Common
{
    public interface IVideoDownloadService
    {
        Task<bool> AuthenticateAsync(string username, string password);
        /// <summary>
        /// Downloads videos in [startDate, endDate]. When force is false (default), each device's
        /// effective start date is narrowed to "since its last successful pull" via IWatermarkService,
        /// so a normal run only fetches what's new. Pass force=true to always scan the full requested
        /// window regardless of watermark (e.g. the operator explicitly wants to re-scan).
        /// </summary>
        Task<bool> DownloadVideosAsync(string outputPath, DateTime startDate, DateTime endDate, bool force = false);
        Task<bool> DownloadSnapshotsAsync(string outputPath, DateTime startDate, DateTime endDate);
        /// <summary>
        /// Resolves every device's effective start date and matched-item count up front, before any
        /// actual downloading starts. A UI can poll <see cref="GetPreScanCounts"/> while this runs to
        /// show live per-device counts (e.g. filling in an "Items" column) during the initial pull.
        /// A subsequent DownloadVideosAsync call for the same outputPath/date range/force reuses these
        /// results instead of re-scanning.
        /// </summary>
        Task PreScanAsync(string outputPath, DateTime startDate, DateTime endDate, bool force = false, CancellationToken cancellationToken = default);
        /// <summary>Per-device matched-item counts discovered so far by the most recent PreScanAsync/DownloadVideosAsync call, keyed by provider device id.</summary>
        IReadOnlyDictionary<string, int> GetPreScanCounts();
        string GetDownloadStatus();
        /// <summary>Live per-file progress for the download currently in flight (files completed/total, bytes, current file).</summary>
        VideoForensics.Providers.Common.Contracts.DownloadStatus GetProgress();
        /// <summary>Drains and returns any per-file activity messages queued since the last call (e.g. "✓ file.mp4 (5.2 MB)", "✗ event 123: 404").</summary>
        IReadOnlyList<string> DrainActivityLog();
        /// <summary>How many matched items from the last download call weren't actually downloaded (e.g. a rate limit cut the run short), aggregated across all devices processed.</summary>
        int GetRemainingCount();
        /// <summary>Which device (1-based index, total count, display name) is currently being processed, so a UI can explain why the per-device file count just reset instead of it looking like a glitch.</summary>
        (int Index, int Total, string Name) GetCurrentDevice();
        string? GetLastError();
    }
}
