using System.Runtime.InteropServices;

namespace VideoForensics.Client.Core.Utilities
{
    /// <summary>Utilities for detecting and constructing save paths.</summary>
    public static class PathUtilities
    {
        /// <summary>Detects the OneDrive path if available, otherwise returns UserProfile/Pictures/VideoForensics.</summary>
        public static string GetDefaultDownloadLocation()
        {
            // Try to detect OneDrive path first (Windows)
            var oneDrivePath = GetOneDrivePath();
            if (!string.IsNullOrEmpty(oneDrivePath))
                return Path.Combine(oneDrivePath, "VideoForensics");

            // Fallback to UserProfile/Pictures
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Pictures",
                "VideoForensics");
        }

        /// <summary>Gets the OneDrive path if it exists. Returns null if OneDrive is not configured.</summary>
        public static string? GetOneDrivePath()
        {
            // Check environment variable (OneDrive sets this)
            var oneDriveEnv = Environment.GetEnvironmentVariable("OneDrive");
            if (!string.IsNullOrEmpty(oneDriveEnv) && Directory.Exists(oneDriveEnv))
                return oneDriveEnv;

            // Check registry on Windows for OneDrive path
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                try
                {
                    using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\OneDrive\Accounts\Business1");
                    if (key?.GetValue("UserFolder") is string businessOneDrive && Directory.Exists(businessOneDrive))
                        return businessOneDrive;

                    using var personalKey = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\OneDrive\Accounts\Personal");
                    if (personalKey?.GetValue("UserFolder") is string personalOneDrive && Directory.Exists(personalOneDrive))
                        return personalOneDrive;
                }
                catch
                {
                    // Registry lookup failed, fall back to default
                }
            }

            return null;
        }

        /// <summary>Builds the full save path with location and camera name structure.</summary>
        public static string BuildSavePath(string basePath, string locationName, string cameraName)
        {
            // Sanitize location and camera names for file paths
            var sanitizedLocation = SanitizePathSegment(locationName);
            var sanitizedCamera = SanitizePathSegment(cameraName);
            return Path.Combine(basePath, sanitizedLocation, sanitizedCamera);
        }

        /// <summary>Removes invalid path characters from a string segment.</summary>
        private static string SanitizePathSegment(string segment)
        {
            if (string.IsNullOrWhiteSpace(segment))
                return "Unknown";

            var invalidChars = Path.GetInvalidPathChars();
            var sanitized = new string(segment
                .Where(c => !invalidChars.Contains(c) && c != ':' && c != '|' && c != '?')
                .ToArray());

            return string.IsNullOrWhiteSpace(sanitized) ? "Unknown" : sanitized.Trim();
        }
    }
}
