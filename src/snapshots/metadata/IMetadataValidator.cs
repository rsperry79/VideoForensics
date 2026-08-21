using System;
using System.Threading.Tasks;

namespace Ring.Api.Snapshots.Metadata
{
    /// <summary>
    /// Interface for validating snapshot image integrity and format.
    /// Detects format, corruption, and quality issues.
    /// </summary>
    public interface IMetadataValidator
    {
        /// <summary>
        /// Validates an image file asynchronously.
        /// </summary>
        /// <param name="snapshotFilePath">Path to the snapshot file.</param>
        /// <returns>True if the image is valid, false otherwise.</returns>
        Task<bool> ValidateAsync(string snapshotFilePath);

        /// <summary>
        /// Validates an image file synchronously.
        /// </summary>
        /// <param name="snapshotFilePath">Path to the snapshot file.</param>
        /// <returns>True if the image is valid, false otherwise.</returns>
        bool Validate(string snapshotFilePath);

        /// <summary>
        /// Detects the image format based on file header.
        /// </summary>
        /// <param name="snapshotFilePath">Path to the snapshot file.</param>
        /// <returns>Image format (JPEG, PNG, WebP) or null if unknown.</returns>
        string? DetectFormat(string snapshotFilePath);

        /// <summary>
        /// Checks if a file appears to be corrupted based on header inspection.
        /// </summary>
        /// <param name="snapshotFilePath">Path to the snapshot file.</param>
        /// <returns>True if file appears corrupted, false otherwise.</returns>
        bool IsCorrupted(string snapshotFilePath);
    }
}
