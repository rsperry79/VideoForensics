using VideoForensics.Client.Common.Contracts;
using VideoForensics.Core.Api.Contracts;

namespace VideoForensics.Client.Hosting
{
    /// <summary>
    /// Builds the download-progress wire payload from the sync activity log, the only live progress source.
    /// <para>
    /// The download service's own progress getters are not used: sync runs through a different
    /// <c>IMediaDownloadService</c> instance, so those getters never update during a sync. Numeric and status
    /// fields are therefore always neutral, and only the activity lines carry live data.
    /// </para>
    /// </summary>
    public static class DownloadProgressDtoMapping
    {
        /// <summary>
        /// Drains the sync activity log into a payload. Each call consumes the lines it returns, so only the
        /// broadcast sampler should call this; it is the single consumer whose lines fan out to all clients.
        /// </summary>
        public static DownloadProgressDto ToDownloadProgressDto(this ISyncActivityLog activity)
        {
            return new DownloadProgressDto(
                IdleStatus(),
                0,
                0,
                null,
                activity.Drain(),
                new Dictionary<string, int>(),
                null,
                null);
        }

        /// <summary>
        /// A payload with no activity and neutral state, for snapshot-on-connect. It reads nothing, so it never
        /// consumes lines that other clients still need.
        /// </summary>
        public static DownloadProgressDto IdleSnapshot()
        {
            return new DownloadProgressDto(
                IdleStatus(),
                0,
                0,
                null,
                Array.Empty<string>(),
                new Dictionary<string, int>(),
                null,
                null);
        }

        // Explicit zeros: the DTO's own defaults are not neutral (ActiveConnections defaults to 1).
        private static DownloadStatusDto IdleStatus()
        {
            return new DownloadStatusDto(false, 0, 0, 0L, null, 0, 0, 0L, 0, 0.0);
        }
    }
}
