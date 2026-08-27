using System;
using System.IO;
using VideoForensics.Providers.Common.Helpers.Contracts;

#nullable enable

namespace VideoForensics.Providers.Common.Helpers.Media
{
    /// <summary>Validates media files exist and match expected specifications</summary>
    public class MediaValidator : IMediaValidator
    {
        /// <summary>
        /// Validates that a media file exists and optionally matches the expected size.
        /// When expectedSize is provided, the file size must match exactly.
        /// When expectedSize is null, the file must exist and have size greater than zero.
        /// </summary>
        /// <param name="filePath">Path to the media file</param>
        /// <param name="expectedSize">Expected file size in bytes, or null to skip size validation</param>
        /// <returns>True if file exists and size matches (if provided); otherwise false</returns>
        public bool ValidateMediaExists(string filePath, long? expectedSize)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                return false;

            if (expectedSize.HasValue)
                return new FileInfo(filePath).Length == expectedSize.Value;

            return new FileInfo(filePath).Length > 0;
        }
    }
}
