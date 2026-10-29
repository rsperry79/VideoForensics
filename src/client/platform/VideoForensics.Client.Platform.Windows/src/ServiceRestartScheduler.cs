using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.Versioning;

namespace VideoForensics.Client.Platform.Windows
{
    /// <summary>
    /// Starts the service again after this process has exited. Used when an update is applied at startup: the process has
    /// not connected to the service manager yet, so exiting counts as a failed start, which runs no recovery action and
    /// would leave the service stopped. A detached helper outlives this process and starts the service explicitly.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public static class ServiceRestartScheduler
    {
        /// <summary>Builds the cmd.exe arguments: wait for this process to exit, then start the service.</summary>
        public static string BuildArguments(string serviceName, int delaySeconds) =>
            $"/c timeout /t {delaySeconds} /nobreak >nul & sc.exe start {serviceName}";

        /// <summary>
        /// Launches the detached helper. Returns false when it could not be started, in which case the caller must not
        /// exit: the service would stay stopped.
        /// </summary>
        public static bool TrySchedule(string serviceName, int delaySeconds = 5)
        {
            try
            {
                using Process? helper = Process.Start(new ProcessStartInfo("cmd.exe")
                {
                    Arguments = BuildArguments(serviceName, delaySeconds),
                    UseShellExecute = false,
                    CreateNoWindow = true,
                });
                return helper is not null;
            }
            catch (Win32Exception)
            {
                return false;
            }
        }
    }
}
