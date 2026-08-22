using System;

namespace VideoForensics.Providers.Ring.Snapshots
{
    /// <summary>
    /// Provides URIs for snapshot operations. Intended to be implemented by Session to keep
    /// URI construction centralized in the API core, where base URLs and authentication context live.
    /// </summary>
    public interface ISnapshotUriProvider
    {
        /// <summary>
        /// Constructs the URI for fetching the latest snapshot from a doorbot.
        /// </summary>
        /// <param name="doorbotId">The doorbot device ID</param>
        /// <returns>URI for fetching the latest snapshot</returns>
        Uri GetLatestSnapshotUri(long doorbotId);

        /// <summary>
        /// Constructs the URI for requesting a fresh snapshot from a doorbot.
        /// </summary>
        /// <param name="doorbotId">The doorbot device ID</param>
        /// <returns>URI for requesting a fresh snapshot</returns>
        Uri GetRefreshSnapshotUri(long doorbotId);
    }
}
