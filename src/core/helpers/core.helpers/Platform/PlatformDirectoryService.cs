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
            return OperatingSystem.IsWindows()
                ? Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    "VideoForensics")
                : OperatingSystem.IsLinux()
                ? GetXdgDataHome()
                : OperatingSystem.IsMacOS()
                ? Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    "Library", "Application Support", AppName)
                : throw new PlatformNotSupportedException($"Unsupported platform");
        }

        public string GetLogsDirectory()
        {
            return OperatingSystem.IsWindows()
                ? Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    "VideoForensics", "Logs")
                : OperatingSystem.IsLinux()
                ? GetXdgStateHome()
                : OperatingSystem.IsMacOS()
                ? Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    "Library", "Logs", AppName)
                : throw new PlatformNotSupportedException($"Unsupported platform");
        }

        public string GetConfigDirectory()
        {
            return OperatingSystem.IsWindows()
                ? Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    "VideoForensics")
                : OperatingSystem.IsLinux()
                ? GetXdgConfigHome()
                : OperatingSystem.IsMacOS()
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
