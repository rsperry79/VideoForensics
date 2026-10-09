namespace VideoForensics.Api.Contracts
{
    /// <summary>
    /// Wire payload for the server's "DownloadProgress" LiveHub broadcast. Sent on a fixed tick
    /// so remote paired clients can show live download progress without polling.
    /// </summary>
    /// <param name="Progress">Per-file and cumulative download counters for the run in flight.</param>
    /// <param name="CurrentDeviceIndex">1-based index of the device currently being processed (0 when idle).</param>
    /// <param name="CurrentDeviceTotal">Total devices in the current run (0 when idle).</param>
    /// <param name="CurrentDeviceName">Display name of the device currently being processed, or null.</param>
    /// <param name="Activity">Activity lines drained since the previous tick.</param>
    /// <param name="PreScanCounts">Per-device matched-item counts keyed by provider device id.</param>
    /// <param name="LastError">Most recent download error message, or null if none.</param>
    /// <param name="RemainingReason">Why matched items were not downloaded (e.g. rate limit), or null if unknown or not applicable.</param>
    public record DownloadProgressDto(
        DownloadStatusDto Progress,
        int CurrentDeviceIndex,
        int CurrentDeviceTotal,
        string? CurrentDeviceName,
        IReadOnlyList<string> Activity,
        IReadOnlyDictionary<string, int> PreScanCounts,
        string? LastError,
        string? RemainingReason
    );
}
