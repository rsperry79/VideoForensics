using System;
using System.Threading.Tasks;
using Ring.Api.Snapshots.Metadata.Models;

namespace Ring.Api.Snapshots.Metadata
{
    /// <summary>
    /// Interface for writing EXIF metadata to snapshot files.
    /// Implements both sync and async methods for flexible usage patterns.
    /// </summary>
    public interface IMetadataWriter
    {
        /// <summary>
        /// Writes metadata to a snapshot file asynchronously.
        /// </summary>
        /// <param name="snapshotFilePath">Path to the snapshot file.</param>
        /// <param name="metadata">Metadata to write.</param>
        /// <returns>Result of the write operation.</returns>
        Task<MetadataWriteResult> WriteMetadataAsync(string snapshotFilePath, SnapshotMetadata metadata);

        /// <summary>
        /// Writes metadata to a snapshot file synchronously.
        /// </summary>
        /// <param name="snapshotFilePath">Path to the snapshot file.</param>
        /// <param name="metadata">Metadata to write.</param>
        /// <returns>Result of the write operation.</returns>
        MetadataWriteResult WriteMetadata(string snapshotFilePath, SnapshotMetadata metadata);

        /// <summary>
        /// Validates a snapshot file asynchronously.
        /// </summary>
        /// <param name="snapshotFilePath">Path to the snapshot file.</param>
        /// <returns>Validation result.</returns>
        Task<MetadataWriteResult> ValidateImageAsync(string snapshotFilePath);

        /// <summary>
        /// Validates a snapshot file synchronously.
        /// </summary>
        /// <param name="snapshotFilePath">Path to the snapshot file.</param>
        /// <returns>Validation result.</returns>
        MetadataWriteResult ValidateImage(string snapshotFilePath);
    }
}
