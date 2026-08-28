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
            {
                // Prefer Videos folder if it exists in OneDrive, otherwise use Pictures, otherwise use root
                var videosPath = Path.Combine(oneDrivePath, "Videos");
                if (Directory.Exists(videosPath))
                    return Path.Combine(videosPath, "VideoForensics");

                var picturesPath = Path.Combine(oneDrivePath, "Pictures");
                if (Directory.Exists(picturesPath))
                    return Path.Combine(picturesPath, "VideoForensics");

                // Fallback to OneDrive root
                return Path.Combine(oneDrivePath, "VideoForensics");
            }

            // Fallback to UserProfile/Pictures
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Pictures",
                "VideoForensics");
        }

        /// <summary>Gets the OneDrive path if it exists. Returns null if OneDrive is not configured.</summary>
        public static string? GetOneDrivePath()
        {
            // Check environment variable (OneDrive sets this when OneDrive is running)
            var oneDriveEnv = Environment.GetEnvironmentVariable("OneDrive");
            if (!string.IsNullOrEmpty(oneDriveEnv) && Directory.Exists(oneDriveEnv))
                return oneDriveEnv;

            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return null;

            try
            {
                // Check User Shell Folders registry (Windows Explorer known folders mapped to OneDrive)
                using var shellFoldersKey = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\User Shell Folders");
                if (shellFoldersKey != null)
                {
                    // Try OneDrive first, then OneDriveCommercial
                    foreach (var valueName in new[] { "OneDrive", "OneDriveCommercial", "{F42EE2D3-909F-4907-8871-4C22FC0BF756}" })
                    {
                        if (shellFoldersKey.GetValue(valueName) is string folderPath && Directory.Exists(folderPath))
                            return folderPath;
                    }
                }

                // Check OneDrive Accounts registry for configured OneDrive paths
                using var accountsKey = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\OneDrive\Accounts");
                if (accountsKey != null)
                {
                    foreach (var accountName in accountsKey.GetSubKeyNames())
                    {
                        using var accountKey = accountsKey.OpenSubKey(accountName);
                        if (accountKey?.GetValue("UserFolder") is string userFolder && Directory.Exists(userFolder))
                            return userFolder;
                    }
                }

                // Also check Shell Folders (non-user-specific)
                using var shellKey = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Shell Folders");
                if (shellKey != null)
                {
                    if (shellKey.GetValue("OneDrive") is string oneDriveShell && Directory.Exists(oneDriveShell))
                        return oneDriveShell;
                }
            }
            catch
            {
                // Registry lookup failed, fall back to default
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
