using System;
using System.IO;

using VideoForensics.Providers.Common.Helpers.Contracts;

namespace VideoForensics.Providers.Common.Helpers.Platform
{
    /// <summary>Provides platform-agnostic access to standard application directories</summary>
    public class PlatformDirectoryService : IPlatformDirectoryService
    {
        private const string AppName = "RingVideos";
        private const string AppDirName = "ringvideos";

        public string GetApplicationDataDirectory()
        {
            if (OperatingSystem.IsWindows())
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    "VideoForensics");
            }

            if (OperatingSystem.IsLinux())
            {
                return GetXdgDataHome();
            }

            return OperatingSystem.IsMacOS()
                ? Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    "Library", "Application Support", AppName)
                : throw new PlatformNotSupportedException($"Unsupported platform");
        }

        public string GetLogsDirectory()
        {
            if (OperatingSystem.IsWindows())
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    "VideoForensics", "Logs");
            }

            if (OperatingSystem.IsLinux())
            {
                return GetXdgStateHome();
            }

            return OperatingSystem.IsMacOS()
                ? Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    "Library", "Logs", AppName)
                : throw new PlatformNotSupportedException($"Unsupported platform");
        }

        public string GetConfigDirectory()
        {
            if (OperatingSystem.IsWindows())
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    "VideoForensics");
            }

            if (OperatingSystem.IsLinux())
            {
                return GetXdgConfigHome();
            }

            return OperatingSystem.IsMacOS()
                ? Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    "Library", "Preferences", AppName)
                : throw new PlatformNotSupportedException($"Unsupported platform");
        }

        private string GetXdgDataHome()
        {
            string? xdgDataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
            return !string.IsNullOrEmpty(xdgDataHome)
                ? Path.Combine(xdgDataHome, AppDirName)
                : Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".local", "share", AppDirName);
        }

        private string GetXdgStateHome()
        {
            string? xdgStateHome = Environment.GetEnvironmentVariable("XDG_STATE_HOME");
            return !string.IsNullOrEmpty(xdgStateHome)
                ? Path.Combine(xdgStateHome, AppDirName)
                : Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".local", "state", AppDirName);
        }

        private string GetXdgConfigHome()
        {
            string? xdgConfigHome = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
            return !string.IsNullOrEmpty(xdgConfigHome)
                ? Path.Combine(xdgConfigHome, AppDirName)
                : Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".config", AppDirName);
        }
    }
}
