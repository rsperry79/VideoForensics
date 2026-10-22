using Microsoft.Win32;

namespace VideoForensics.Client.Platform.Windows
{
    /// <summary>
    /// Windows-only lookups for OneDrive-synced folders and the legacy download and export locations.
    /// Moved out of the shared Client.Core <c>PathUtilities</c> so shared code carries no registry access.
    /// </summary>
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    public static class OneDrivePaths
    {
        /// <summary>Detects the OneDrive path if available, otherwise returns UserProfile/Pictures/VideoForensics (legacy location).</summary>
        public static string GetLegacyDownloadLocation()
        {
            // Try to detect OneDrive path first (Windows)
            string? oneDrivePath = GetOneDrivePath();
            if (!string.IsNullOrEmpty(oneDrivePath))
            {
                // Prefer Videos folder if it exists in OneDrive, otherwise use Pictures, otherwise use root
                string videosPath = Path.Combine(oneDrivePath, "Videos");
                if (Directory.Exists(videosPath))
                {
                    return Path.Combine(videosPath, "VideoForensics");
                }

                string picturesPath = Path.Combine(oneDrivePath, "Pictures");
                if (Directory.Exists(picturesPath))
                {
                    return Path.Combine(picturesPath, "VideoForensics");
                }

                // Fallback to OneDrive root
                return Path.Combine(oneDrivePath, "VideoForensics");
            }

            // Fallback to UserProfile/Pictures
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Pictures",
                "VideoForensics");
        }

        /// <summary>Detects the OneDrive path if available, otherwise returns UserProfile/Documents/VideoForensics (legacy location).</summary>
        public static string GetLegacyQueryExportLocation()
        {
            string? oneDrivePath = GetOneDrivePath();
            if (!string.IsNullOrEmpty(oneDrivePath))
            {
                string documentsPath = Path.Combine(oneDrivePath, "Documents");
                if (Directory.Exists(documentsPath))
                {
                    return Path.Combine(documentsPath, "VideoForensics");
                }

                // Fallback to OneDrive root
                return Path.Combine(oneDrivePath, "VideoForensics");
            }

            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "VideoForensics");
        }

        /// <summary>Gets the OneDrive path if it exists. Returns null if OneDrive is not configured.</summary>
        public static string? GetOneDrivePath()
        {
            // Check environment variable (OneDrive sets this when OneDrive is running)
            string? oneDriveEnv = Environment.GetEnvironmentVariable("OneDrive");
            if (!string.IsNullOrEmpty(oneDriveEnv) && Directory.Exists(oneDriveEnv))
            {
                return oneDriveEnv;
            }

            try
            {
                // Check User Shell Folders registry (Windows Explorer known folders mapped to OneDrive)
                using RegistryKey? shellFoldersKey = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\User Shell Folders");
                if (shellFoldersKey != null)
                {
                    // Try OneDrive first, then OneDriveCommercial
                    foreach (string? valueName in new[] { "OneDrive", "OneDriveCommercial", "{F42EE2D3-909F-4907-8871-4C22FC0BF756}" })
                    {
                        if (shellFoldersKey.GetValue(valueName) is string folderPath && Directory.Exists(folderPath))
                        {
                            return folderPath;
                        }
                    }
                }

                // Check OneDrive Accounts registry for configured OneDrive paths
                using RegistryKey? accountsKey = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\OneDrive\Accounts");
                if (accountsKey != null)
                {
                    foreach (string accountName in accountsKey.GetSubKeyNames())
                    {
                        using RegistryKey? accountKey = accountsKey.OpenSubKey(accountName);
                        if (accountKey?.GetValue("UserFolder") is string userFolder && Directory.Exists(userFolder))
                        {
                            return userFolder;
                        }
                    }
                }

                // Also check Shell Folders (non-user-specific)
                using RegistryKey? shellKey = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Shell Folders");
                if (shellKey != null)
                {
                    if (shellKey.GetValue("OneDrive") is string oneDriveShell && Directory.Exists(oneDriveShell))
                    {
                        return oneDriveShell;
                    }
                }
            }
            catch
            {
                // Registry lookup failed, fall back to default
            }

            return null;
        }
    }
}
