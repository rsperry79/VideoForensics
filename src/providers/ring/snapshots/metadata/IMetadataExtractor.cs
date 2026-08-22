using System;
using System.Threading.Tasks;
using VideoForensics.Providers.Ring.Entities;
using VideoForensics.Providers.Ring.Snapshots.Metadata.Models;

namespace VideoForensics.Providers.Ring.Snapshots.Metadata
{
    /// <summary>
    /// Interface for extracting metadata from snapshot events.
    /// Implements both sync and async methods for flexible usage patterns.
    /// </summary>
    public interface IMetadataExtractor
    {
        /// <summary>
        /// Extracts metadata from a snapshot event asynchronously.
        /// </summary>
        /// <param name="snapshotEvent">The snapshot event to extract from.</param>
        /// <returns>Extracted snapshot metadata.</returns>
        Task<SnapshotMetadata> ExtractMetadataAsync(DoorbotHistoryEvent snapshotEvent);

        /// <summary>
        /// Extracts metadata from a snapshot event synchronously.
        /// </summary>
        /// <param name="snapshotEvent">The snapshot event to extract from.</param>
        /// <returns>Extracted snapshot metadata.</returns>
        SnapshotMetadata ExtractMetadata(DoorbotHistoryEvent snapshotEvent);
    }
}
