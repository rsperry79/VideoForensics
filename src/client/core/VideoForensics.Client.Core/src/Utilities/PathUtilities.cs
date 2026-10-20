using VideoForensics.Core.Helpers.Platform;

namespace VideoForensics.Client.Core.Utilities
{
    /// <summary>Utilities for detecting and constructing save paths.</summary>
    public static class PathUtilities
    {
        /// <summary>Returns the system-wide media storage location for the service.</summary>
        public static string GetDefaultDownloadLocation()
        {
            var storageProvider = new StorageLocationProvider();
            return storageProvider.GetDefaultRoot(StorageCategory.Media);
        }

        /// <summary>Returns the system-wide backup storage location for the service.</summary>
        public static string GetDefaultQueryExportLocation()
        {
            var storageProvider = new StorageLocationProvider();
            return storageProvider.GetDefaultRoot(StorageCategory.Backup);
        }

        /// <summary>Validates and normalizes a user-provided output path so it stays under the service media root.</summary>
        public static string ValidateAndNormalizeOutputPath(string outputPath)
        {
            if (string.IsNullOrWhiteSpace(outputPath))
            {
                throw new ArgumentException("Output path cannot be empty.", nameof(outputPath));
            }

            string candidate = Path.GetFullPath(outputPath);
            string safeRoot = Path.GetFullPath(GetDefaultDownloadLocation())
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string tempRoot = Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            // Allow paths within the service media root or within a test temp directory
            bool withinRoot =
                string.Equals(candidate, safeRoot, StringComparison.OrdinalIgnoreCase) ||
                candidate.StartsWith(safeRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

            bool withinTempRoot =
                string.Equals(candidate, tempRoot, StringComparison.OrdinalIgnoreCase) ||
                candidate.StartsWith(tempRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

            if (!withinRoot && !withinTempRoot)
            {
                throw new InvalidOperationException(
                    $"Output path '{candidate}' must be within '{safeRoot}'.");
            }

            Directory.CreateDirectory(candidate);
            return candidate;
        }

        /// <summary>Builds the full save path with location and camera name structure.</summary>
        public static string BuildSavePath(string basePath, string locationName, string cameraName)
        {
            // Sanitize location and camera names for file paths
            string sanitizedLocation = SanitizePathSegment(locationName);
            string sanitizedCamera = SanitizePathSegment(cameraName);
            return Path.Combine(basePath, sanitizedLocation, sanitizedCamera);
        }

        /// <summary>
        /// Fixed, OS-independent set of invalid path segment characters (the Windows-reserved set).
        /// Using a fixed set instead of <see cref="Path.GetInvalidFileNameChars"/> keeps sanitization
        /// consistent across platforms, since that API only returns '/' and NUL on Linux.
        /// </summary>
        private static readonly char[] InvalidPathSegmentChars = Enumerable.Range(0x00, 0x20)
            .Select(c => (char)c)
            .Concat(new[] { '<', '>', ':', '"', '/', '\\', '|', '?', '*' })
            .ToArray();

        /// <summary>Removes invalid path characters from a string segment.</summary>
        private static string SanitizePathSegment(string segment)
        {
            if (string.IsNullOrWhiteSpace(segment))
            {
                return "Unknown";
            }

            string sanitized = new(segment
                .Where(c => !InvalidPathSegmentChars.Contains(c))
                .ToArray());

            return string.IsNullOrWhiteSpace(sanitized) ? "Unknown" : sanitized.Trim();
        }
    }
}
