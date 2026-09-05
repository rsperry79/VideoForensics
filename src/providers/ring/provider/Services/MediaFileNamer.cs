using System.Text.RegularExpressions;

namespace VideoForensics.Providers.Ring.Services
{
    /// <summary>
    /// Standardizes media file naming across all downloads: CameraName_date_time_type.ext
    /// Handles sanitization for OneDrive/path safety.
    /// </summary>
    public static class MediaFileNamer
    {
        /// <summary>
        /// Format: CameraName_yyyyMMdd_HHmmss_type.ext
        /// Examples:
        ///   Front Door_20260827_143022_video.mp4
        ///   Front Door_20260827_143022_snapshot.jpg
        ///   Front Door_20260827_143022_metadata.json
        /// </summary>
        public static string FormatMediaFileName(string cameraName, DateTime timestamp, string mediaType, string extension)
        {
            string sanitizedName = SanitizeForFilePath(cameraName);
            string dateTime = timestamp.ToString("yyyyMMdd_HHmmss");
            return $"{sanitizedName}_{dateTime}_{mediaType}.{extension.TrimStart('.')}";
        }

        /// <summary>
        /// Remove or replace characters that OneDrive/Windows don't allow in filenames:
        /// &lt; &gt; : " / \ | ? * and control characters.
        /// Replace spaces with underscore for clarity.
        /// </summary>
        public static string SanitizeForFilePath(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return "device";
            }

            // Characters forbidden in NTFS/OneDrive filenames: < > : " / \ | ? *
            // Also control characters 0x00-0x1F
            string sanitized = Regex.Replace(input, @"[\<\>\:""/\\|\?\*\x00-\x1F]", string.Empty);

            // Replace multiple spaces with single space, then trim
            sanitized = Regex.Replace(sanitized, @"\s+", " ").Trim();

            // Replace remaining spaces with underscore for clarity
            sanitized = sanitized.Replace(" ", "_");

            // If entirely empty after sanitization, use fallback
            return string.IsNullOrEmpty(sanitized) ? "device" : sanitized;
        }
    }
}
