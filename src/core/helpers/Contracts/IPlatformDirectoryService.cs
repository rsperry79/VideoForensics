#nullable enable

namespace VideoForensics.Providers.Common.Helpers.Contracts
{
    /// <summary>
    /// Provides platform-agnostic access to standard application directories,
    /// following XDG Base Directory conventions on Linux and standard conventions on Windows/macOS.
    /// </summary>
    public interface IPlatformDirectoryService
    {
        /// <summary>
        /// Returns the user's application data directory.
        /// - Windows: %APPDATA%\RingVideosData
        /// - Linux: $XDG_DATA_HOME/ringvideos or ~/.local/share/ringvideos
        /// - macOS: ~/Library/Application Support/RingVideos
        /// </summary>
        string GetApplicationDataDirectory();

        /// <summary>
        /// Returns the user's log directory.
        /// - Windows: %APPDATA%\RingVideosData\Logs
        /// - Linux: $XDG_STATE_HOME/ringvideos or ~/.local/state/ringvideos
        /// - macOS: ~/Library/Logs/RingVideos
        /// </summary>
        string GetLogsDirectory();

        /// <summary>
        /// Returns the user's configuration directory.
        /// - Windows: %APPDATA%\RingVideosData
        /// - Linux: $XDG_CONFIG_HOME/ringvideos or ~/.config/ringvideos
        /// - macOS: ~/Library/Preferences/RingVideos
        /// </summary>
        string GetConfigDirectory();
    }
}
