using VideoForensics.Client.Common.Contracts;
using VideoForensics.Client.Hosting.Contracts;
using VideoForensics.Core.Api.Contracts;

namespace VideoForensics.Client.Hosting
{
    /// <summary>
    /// Builds the download-progress wire payload from the sync activity log and the sync progress store.
    /// <para>
    /// The download service's own progress getters are not used: each pull's download service is scoped, so a singleton
    /// reader cannot see it. Each pull publishes its readings to <see cref="ISyncProgressStore"/> instead, and the numeric
    /// fields come from there. Activity lines still come only from the activity log.
    /// </para>
    /// </summary>
    public static class DownloadProgressDtoMapping
    {
        /// <summary>
        /// Drains the sync activity log into a payload. Each call consumes the lines it returns, so only the
        /// broadcast sampler should call this; it is the single consumer whose lines fan out to all clients.
        /// The progress store is read, never consumed.
        /// </summary>
        public static DownloadProgressDto ToDownloadProgressDto(this ISyncActivityLog activity, ISyncProgressStore? progress = null)
        {
            return Build(progress, activity.Drain());
        }

        /// <summary>
        /// A payload with no activity and the current progress, for snapshot-on-connect. It reads nothing from the
        /// activity log, so it never consumes lines that other clients still need. Without a store the state is neutral.
        /// </summary>
        public static DownloadProgressDto IdleSnapshot(ISyncProgressStore? progress = null)
        {
            return Build(progress, Array.Empty<string>());
        }

        private static DownloadProgressDto Build(ISyncProgressStore? progress, IReadOnlyList<string> activity)
        {
            SyncProgress reading = progress?.Current ?? SyncProgress.Idle;
            return new DownloadProgressDto(
                new DownloadStatusDto(
                    reading.IsDownloading,
                    reading.FilesCompleted,
                    reading.FilesTotal,
                    reading.BytesDownloaded,
                    reading.CurrentFile,
                    0,
                    0,
                    0L,
                    reading.ActiveConnections,
                    0.0),
                reading.DeviceIndex,
                reading.DeviceTotal,
                reading.DeviceName,
                activity,
                new Dictionary<string, int>(),
                null,
                null);
        }
    }
}
