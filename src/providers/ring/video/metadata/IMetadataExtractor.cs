using System.Threading.Tasks;
using VideoForensics.Providers.Ring.Entities;
using VideoForensics.Providers.Ring.Video.Metadata.Models;

#nullable enable

namespace VideoForensics.Providers.Ring.Video.Metadata
{
    /// <summary>
    /// Extracts standardized metadata from Ring event DTOs for writing to video files.
    /// </summary>
    public interface IMetadataExtractor
    {
        /// <summary>
        /// Extracts metadata from a Ring event and device information.
        /// </summary>
        /// <param name="ringEvent">The Ring event with CV properties and device data.</param>
        /// <returns>Extracted metadata ready for tagging.</returns>
        Task<VideoMetadata> ExtractMetadataAsync(DoorbotHistoryEvent ringEvent);

        /// <summary>
        /// Extracts metadata from a Ring event (synchronous version).
        /// </summary>
        VideoMetadata ExtractMetadata(DoorbotHistoryEvent ringEvent);
    }
}
