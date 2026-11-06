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
            string sanitizedType = SanitizeForFilePath(mediaType);
            string dateTime = timestamp.ToString("yyyyMMdd_HHmmss");
            return $"{sanitizedName}_{dateTime}_{sanitizedType}.{extension.TrimStart('.')}";
        }

        // Ring CV detection_type -> (name suffix, priority). Lower priority wins. other_motion and unknown values have no
        // sub-category, so a motion event with only those stays plain "motion". The priority order matches the one the stored
        // event types use, so file names and the event type filter agree.
        private static readonly Dictionary<string, (string Suffix, int Priority)> DetectionSuffixes = new(StringComparer.Ordinal)
        {
            ["human"] = ("person", 10),
            ["animal"] = ("animal", 20),
            ["vehicle"] = ("vehicle", 30),
            ["package_delivery"] = ("package", 40),
        };

        /// <summary>
        /// The type segment of a media file name. A motion event names its most important detection, for example
        /// <c>motion-person</c>, and falls back to plain <c>motion</c> when Ring gave no sub-category. Any other event kind
        /// keeps its own name, and a missing kind is <c>video</c>.
        /// </summary>
        /// <param name="rawKind">The event kind as Ring reports it (for example "motion" or "ding").</param>
        /// <param name="detectionTypes">Ring CV detection types for the event. Null and unknown values are ignored.</param>
        public static string FormatEventTypeSegment(string? rawKind, IEnumerable<string?> detectionTypes)
        {
            if (string.IsNullOrWhiteSpace(rawKind))
            {
                return "video";
            }

            if (!string.Equals(rawKind, "motion", StringComparison.OrdinalIgnoreCase))
            {
                return rawKind;
            }

            string? best = null;
            int bestPriority = int.MaxValue;
            foreach (string? type in detectionTypes)
            {
                if (type is not null && DetectionSuffixes.TryGetValue(type, out (string Suffix, int Priority) entry) && entry.Priority < bestPriority)
                {
                    best = entry.Suffix;
                    bestPriority = entry.Priority;
                }
            }

            return best is null ? rawKind : rawKind + "-" + best;
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
