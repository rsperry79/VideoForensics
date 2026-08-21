using System;
using System.Threading.Tasks;
using Ring.Api.Video.Metadata.Models;

namespace Ring.Api.Video.Metadata
{
    /// <summary>
    /// Interface for extracting and processing video thumbnails from snapshot data.
    /// Downloads snapshot frames and stores them as video thumbnails for DV evidence.
    /// </summary>
    public interface IVideoThumbnailExtractor
    {
        /// <summary>
        /// Downloads and saves a snapshot as the video thumbnail.
        /// Correlates the snapshot with video metadata for evidence documentation.
        /// </summary>
        /// <param name="snapshotUrl">URL to the Ring snapshot</param>
        /// <param name="videoMetadata">Video metadata for correlation</param>
        /// <param name="videoFilePath">Path to the associated video file</param>
        /// <param name="outputDirectory">Directory to save the thumbnail</param>
        /// <returns>Information about the saved thumbnail</returns>
        Task<VideoThumbnail?> ExtractAndSaveThumbnailAsync(
            string snapshotUrl,
            VideoMetadata videoMetadata,
            string videoFilePath,
            string outputDirectory);

        /// <summary>
        /// Synchronous version of ExtractAndSaveThumbnailAsync.
        /// </summary>
        VideoThumbnail? ExtractAndSaveThumbnail(
            string snapshotUrl,
            VideoMetadata videoMetadata,
            string videoFilePath,
            string outputDirectory);
    }

    /// <summary>
    /// Information about a video thumbnail extracted from a snapshot.
    /// Used for visual identification and evidence documentation.
    /// </summary>
    public class VideoThumbnail
    {
        /// <summary>
        /// Original Ring snapshot URL.
        /// </summary>
        public string? SnapshotUrl { get; set; }

        /// <summary>
        /// Local filename where thumbnail was saved.
        /// </summary>
        public string? FileName { get; set; }

        /// <summary>
        /// Full path to saved thumbnail file.
        /// </summary>
        public string? FilePath { get; set; }

        /// <summary>
        /// Size of thumbnail file in bytes.
        /// </summary>
        public long FileSizeBytes { get; set; }

        /// <summary>
        /// Image format (JPEG, WebP, PNG, etc.).
        /// </summary>
        public string? ImageFormat { get; set; }

        /// <summary>
        /// Image dimensions (width x height).
        /// </summary>
        public string? Dimensions { get; set; }

        /// <summary>
        /// Associated video file path.
        /// </summary>
        public string? VideoFilePath { get; set; }

        /// <summary>
        /// Whether extraction and save was successful.
        /// </summary>
        public bool ExtractionSuccessful { get; set; }

        /// <summary>
        /// Error message if extraction failed.
        /// </summary>
        public string? ExtractionError { get; set; }

        /// <summary>
        /// When the thumbnail was extracted (UTC).
        /// </summary>
        public DateTime ExtractedAt { get; set; }
    }
}
