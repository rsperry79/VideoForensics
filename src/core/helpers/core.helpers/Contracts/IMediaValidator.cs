#nullable enable

namespace VideoForensics.Providers.Common.Helpers.Contracts
{
    /// <summary>Validates media files exist and match expected specifications</summary>
    public interface IMediaValidator
    {
        /// <summary>Validates that a media file exists and optionally matches the expected size</summary>
        /// <param name="filePath">Path to the media file</param>
        /// <param name="expectedSize">Expected file size in bytes, or null to skip size validation</param>
        /// <returns>True if file exists and size matches (if provided); otherwise false</returns>
        bool ValidateMediaExists(string filePath, long? expectedSize);
    }
}
