namespace VideoForensics.Client.Hosting.Contracts
{
    /// <summary>
    /// Latest per-file progress of the running account sync, shared process-wide. The download service is scoped per
    /// pull, so the pull writes its readings here and the progress broadcaster and snapshot reader pick them up.
    /// </summary>
    public interface ISyncProgressStore
    {
        /// <summary>The latest reading. Reading never consumes it.</summary>
        SyncProgress Current { get; }

        /// <summary>Replaces the latest reading. Safe to call from any thread.</summary>
        void Set(SyncProgress progress);

        /// <summary>Returns the store to <see cref="SyncProgress.Idle"/>, when no pull is running.</summary>
        void Clear();
    }

    /// <summary>
    /// One immutable reading of sync progress. Immutable so a reader always sees a consistent set of fields.
    /// </summary>
    /// <param name="IsDownloading">True while a media download is in flight.</param>
    /// <param name="FilesTotal">Files matched in the device currently being downloaded.</param>
    /// <param name="FilesCompleted">Files finished in the device currently being downloaded.</param>
    /// <param name="BytesDownloaded">Bytes downloaded in the device currently being downloaded.</param>
    /// <param name="CurrentFile">File currently downloading, or null.</param>
    /// <param name="ActiveConnections">Concurrent transfer connections in use.</param>
    /// <param name="DeviceIndex">1-based index of the device being processed (0 when idle).</param>
    /// <param name="DeviceTotal">Devices in the pull (0 when idle).</param>
    /// <param name="DeviceName">Display name of the device being processed, or null.</param>
    public sealed record SyncProgress(
        bool IsDownloading,
        int FilesTotal,
        int FilesCompleted,
        long BytesDownloaded,
        string? CurrentFile,
        int ActiveConnections,
        int DeviceIndex,
        int DeviceTotal,
        string? DeviceName)
    {
        /// <summary>The reading when no sync is running: nothing downloading, no device, all counters zero.</summary>
        public static readonly SyncProgress Idle = new(false, 0, 0, 0L, null, 0, 0, 0, null);
    }
}
