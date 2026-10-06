using System;
using System.IO;
using System.IO.Abstractions;

using VideoForensics.Providers.Common.Helpers.Contracts;

namespace VideoForensics.Providers.Common.Helpers.Platform
{
    /// <summary>Provides platform-agnostic access to standard application directories</summary>
    public class PlatformDirectoryService : IPlatformDirectoryService
    {
        private const string AppName = "RingVideos";
        private const string AppDirName = "ringvideos";

        private readonly IFileSystem _fileSystem;

        /// <summary>Initializes a new instance of PlatformDirectoryService with a real filesystem</summary>
        public PlatformDirectoryService() : this(new FileSystem())
        {
        }

        /// <summary>Initializes a new instance of PlatformDirectoryService with an abstracted filesystem for testing</summary>
        public PlatformDirectoryService(IFileSystem fileSystem)
        {
            _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        }

        public string GetApplicationDataDirectory()
        {
            return OperatingSystem.IsWindows()
                ? _fileSystem.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    "VideoForensics")
                : OperatingSystem.IsLinux()
                ? GetXdgDataHome()
                : OperatingSystem.IsMacOS()
                ? _fileSystem.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    "Library", "Application Support", AppName)
                : throw new PlatformNotSupportedException($"Unsupported platform");
        }

        public string GetLogsDirectory()
        {
            return OperatingSystem.IsWindows()
                ? _fileSystem.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    "VideoForensics", "Logs")
                : OperatingSystem.IsLinux()
                ? GetXdgStateHome()
                : OperatingSystem.IsMacOS()
                ? _fileSystem.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    "Library", "Logs", AppName)
                : throw new PlatformNotSupportedException($"Unsupported platform");
        }

        public string GetConfigDirectory()
        {
            return OperatingSystem.IsWindows()
                ? _fileSystem.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    "VideoForensics")
                : OperatingSystem.IsLinux()
                ? GetXdgConfigHome()
                : OperatingSystem.IsMacOS()
                ? _fileSystem.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    "Library", "Preferences", AppName)
                : throw new PlatformNotSupportedException($"Unsupported platform");
        }

        private string GetXdgDataHome()
        {
            string? xdgDataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
            return !string.IsNullOrEmpty(xdgDataHome)
                ? _fileSystem.Path.Combine(xdgDataHome, AppDirName)
                : _fileSystem.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".local", "share", AppDirName);
        }

        private string GetXdgStateHome()
        {
            string? xdgStateHome = Environment.GetEnvironmentVariable("XDG_STATE_HOME");
            return !string.IsNullOrEmpty(xdgStateHome)
                ? _fileSystem.Path.Combine(xdgStateHome, AppDirName)
                : _fileSystem.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".local", "state", AppDirName);
        }

        private string GetXdgConfigHome()
        {
            string? xdgConfigHome = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
            return !string.IsNullOrEmpty(xdgConfigHome)
                ? _fileSystem.Path.Combine(xdgConfigHome, AppDirName)
                : _fileSystem.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".config", AppDirName);
        }
    }
}
