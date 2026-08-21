using System;
using System.IO;
using Ring.Api.Common.Interfaces;

namespace Ring.Api.Common
{
    public class PlatformDirectoryService : IPlatformDirectoryService
    {
        private const string AppName = "RingVideos";
        private const string AppDirName = "ringvideos";

        public string GetApplicationDataDirectory()
        {
            if (OperatingSystem.IsWindows())
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "RingVideosData");
            }

            if (OperatingSystem.IsLinux())
            {
                return GetXdgDataHome();
            }

            if (OperatingSystem.IsMacOS())
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    "Library", "Application Support", AppName);
            }

            throw new PlatformNotSupportedException($"Unsupported platform");
        }

        public string GetLogsDirectory()
        {
            if (OperatingSystem.IsWindows())
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "RingVideosData", "Logs");
            }

            if (OperatingSystem.IsLinux())
            {
                return GetXdgStateHome();
            }

            if (OperatingSystem.IsMacOS())
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    "Library", "Logs", AppName);
            }

            throw new PlatformNotSupportedException($"Unsupported platform");
        }

        public string GetConfigDirectory()
        {
            if (OperatingSystem.IsWindows())
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "RingVideosData");
            }

            if (OperatingSystem.IsLinux())
            {
                return GetXdgConfigHome();
            }

            if (OperatingSystem.IsMacOS())
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    "Library", "Preferences", AppName);
            }

            throw new PlatformNotSupportedException($"Unsupported platform");
        }

        private string GetXdgDataHome()
        {
            var xdgDataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
            if (!string.IsNullOrEmpty(xdgDataHome))
            {
                return Path.Combine(xdgDataHome, AppDirName);
            }

            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".local", "share", AppDirName);
        }

        private string GetXdgStateHome()
        {
            var xdgStateHome = Environment.GetEnvironmentVariable("XDG_STATE_HOME");
            if (!string.IsNullOrEmpty(xdgStateHome))
            {
                return Path.Combine(xdgStateHome, AppDirName);
            }

            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".local", "state", AppDirName);
        }

        private string GetXdgConfigHome()
        {
            var xdgConfigHome = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
            if (!string.IsNullOrEmpty(xdgConfigHome))
            {
                return Path.Combine(xdgConfigHome, AppDirName);
            }

            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".config", AppDirName);
        }
    }
}
