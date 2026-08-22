using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using VideoForensics.Providers.Ring.Entities;

namespace VideoForensics.Providers.Ring.Snapshots
{
    /// <summary>
    /// High-level interface for managing Ring device snapshots. Coordinates snapshot operations
    /// (fetching, updating, saving) across the Ring API infrastructure.
    /// </summary>
    public interface ISnapshotManager
    {
        /// <summary>
        /// Fetches the latest snapshot from a doorbot device and saves it to disk.
        /// </summary>
        /// <param name="doorbot">The doorbot device to fetch a snapshot from</param>
        /// <param name="outputPath">Full file path where the snapshot should be saved</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if successful, false otherwise</returns>
        Task<bool> SaveLatestSnapshotAsync(Doorbot doorbot, string outputPath, CancellationToken cancellationToken = default);

        /// <summary>
        /// Fetches the latest snapshot from a doorbot device and saves it to disk.
        /// </summary>
        /// <param name="doorbotId">The ID of the doorbot device to fetch a snapshot from</param>
        /// <param name="outputPath">Full file path where the snapshot should be saved</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if successful, false otherwise</returns>
        Task<bool> SaveLatestSnapshotAsync(long doorbotId, string outputPath, CancellationToken cancellationToken = default);

        /// <summary>
        /// Fetches the latest snapshot from a doorbot device as a stream.
        /// </summary>
        /// <param name="doorbot">The doorbot device to fetch a snapshot from</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Stream containing the snapshot image data</returns>
        Task<Stream> GetLatestSnapshotAsync(Doorbot doorbot, CancellationToken cancellationToken = default);

        /// <summary>
        /// Fetches the latest snapshot from a doorbot device as a stream.
        /// </summary>
        /// <param name="doorbotId">The ID of the doorbot device to fetch a snapshot from</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Stream containing the snapshot image data</returns>
        Task<Stream> GetLatestSnapshotAsync(long doorbotId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Requests the Ring API to capture a fresh snapshot from a doorbot device.
        /// The snapshot may not be immediately available; use GetLatestSnapshotAsync after
        /// a delay to retrieve the freshly captured snapshot.
        /// </summary>
        /// <param name="doorbotId">The ID of the doorbot device to capture a snapshot from</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if the request succeeded, false otherwise</returns>
        Task<bool> RefreshSnapshotAsync(long doorbotId, CancellationToken cancellationToken = default);
    }
}
