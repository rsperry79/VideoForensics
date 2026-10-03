using System;
using VideoForensics.Utils.LoggerViewer.Maui.Platforms.Linux;
using VideoForensics.Utils.LoggerViewer.Maui.Platforms.Windows;

namespace VideoForensics.Utils.LoggerViewer.Maui.Platforms
{
    /// <summary>
    /// Factory for creating platform-specific IPC clients.
    /// </summary>
    public static class IpcClientFactory
    {
        /// <summary>
        /// Creates an IPC client appropriate for the current platform.
        /// </summary>
        public static IIpcClient CreateClient()
        {
            if (OperatingSystem.IsWindows())
            {
                return new WindowsNamedPipeClient();
            }
            else if (OperatingSystem.IsLinux())
            {
                return new LinuxUnixSocketClient();
            }
            else
            {
                throw new NotSupportedException("Logger Viewer is only supported on Windows and Linux.");
            }
        }
    }
}
