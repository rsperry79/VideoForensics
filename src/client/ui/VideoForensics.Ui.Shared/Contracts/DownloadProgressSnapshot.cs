namespace VideoForensics.Ui.Shared.Contracts
{
    /// <summary>
    /// One host-agnostic sample of download progress, the data <c>DownloadProgressPanel</c> renders.
    /// <para>
    /// Fields are declared directly rather than reusing <c>Providers.Common.DownloadStatus</c>, because
    /// the remote (MAUI) source has the wire <c>DownloadStatusDto</c> and Ui.Shared must not depend on
    /// Hosting to convert it. The field names and types mirror <c>DownloadStatus</c> so a local source
    /// maps across with no renaming.
    /// </para>
    /// <para>
    /// <see cref="Activity"/> holds only the lines produced since the previous emission, so a consumer
    /// appends it once per emission. It is not cumulative state.
    /// </para>
    /// </summary>
    /// <param name="IsDownloading">True while a download run is in flight.</param>
    /// <param name="FilesCompleted">Files finished in the device currently being processed.</param>
    /// <param name="FilesTotal">Files matched in the device currently being processed.</param>
    /// <param name="BytesDownloaded">Bytes downloaded in the device currently being processed.</param>
    /// <param name="CurrentFile">File currently downloading, or null.</param>
    /// <param name="TotalFilesCompleted">Files finished across the whole run.</param>
    /// <param name="TotalFilesMatched">Files matched across the whole run.</param>
    /// <param name="TotalBytesDownloaded">Bytes downloaded across the whole run.</param>
    /// <param name="ActiveConnections">Concurrent transfer connections in use.</param>
    /// <param name="CurrentSpeedMbps">Current transfer speed in megabits per second.</param>
    /// <param name="CurrentDeviceIndex">1-based index of the device being processed (0 when idle).</param>
    /// <param name="CurrentDeviceTotal">Total devices in the run (0 when idle).</param>
    /// <param name="CurrentDeviceName">Display name of the device being processed, or empty.</param>
    /// <param name="PreScanCounts">Per-device matched-item counts keyed by provider device id.</param>
    /// <param name="Activity">Activity lines produced since the previous emission, oldest first.</param>
    /// <param name="LastError">Most recent download error message, or null.</param>
    /// <param name="RemainingReason">Why matched items were not downloaded, or null if unknown or not applicable.</param>
    public sealed record DownloadProgressSnapshot(
        bool IsDownloading,
        int FilesCompleted,
        int FilesTotal,
        long BytesDownloaded,
        string? CurrentFile,
        int TotalFilesCompleted,
        int TotalFilesMatched,
        long TotalBytesDownloaded,
        int ActiveConnections,
        double CurrentSpeedMbps,
        int CurrentDeviceIndex,
        int CurrentDeviceTotal,
        string CurrentDeviceName,
        IReadOnlyDictionary<string, int> PreScanCounts,
        IReadOnlyList<string> Activity,
        string? LastError,
        string? RemainingReason);
}
