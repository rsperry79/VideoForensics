using System.IO.Abstractions;
using System.Threading.Tasks;
using Ring.Api.Video.Metadata.Models;

namespace Ring.Api.Video.Metadata
{
    /// <summary>
    /// Writes extracted metadata to video files using standard tagging formats.
    /// Supports MP4, MOV, and other container formats via FFmpeg.
    /// Platform-agnostic and testable through IFileSystem abstraction.
    /// </summary>
    public interface IMetadataWriter
    {
        /// <summary>
        /// Writes metadata to a video file.
        /// </summary>
        /// <param name="videoFilePath">Path to the video file to tag.</param>
        /// <param name="metadata">Metadata to write.</param>
        /// <returns>Result indicating success and any corrections applied.</returns>
        Task<MetadataWriteResult> WriteMetadataAsync(string videoFilePath, VideoMetadata metadata);

        /// <summary>
        /// Writes metadata to a video file (synchronous version).
        /// </summary>
        MetadataWriteResult WriteMetadata(string videoFilePath, VideoMetadata metadata);

        /// <summary>
        /// Verifies that a video file has compatible metadata and correct encoding.
        /// </summary>
        /// <param name="videoFilePath">Path to the video file to validate.</param>
        /// <returns>Result indicating validation status and any issues found.</returns>
        Task<MetadataWriteResult> ValidateVideoAsync(string videoFilePath);

        /// <summary>
        /// Verifies that a video file has compatible metadata and correct encoding (synchronous version).
        /// </summary>
        MetadataWriteResult ValidateVideo(string videoFilePath);
    }
}
