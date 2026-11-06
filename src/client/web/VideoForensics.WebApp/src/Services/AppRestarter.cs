using System.Diagnostics;

using VideoForensics.Client.Platform.Windows;

namespace VideoForensics.Client.WebApp.Services
{
    /// <summary>Restarts the running server after a change that only takes effect at startup.</summary>
    public interface IAppRestarter
    {
        /// <summary>Starts a fresh copy of the server, then stops this one.</summary>
        void Restart();
    }

    /// <summary>
    /// Restarts the server. Running as the Windows service, it schedules the service start and stops. Running any other way
    /// (for example from a dev run), it launches a delayed copy of its own command line and stops, so the server comes back.
    /// </summary>
    public sealed class AppRestarter(
        IHostApplicationLifetime lifetime,
        Action<int, string> launch,
        Func<bool> isWindowsService,
        Func<string, bool> scheduleServiceStart,
        string exePath,
        IReadOnlyList<string> args) : IAppRestarter
    {
        private const int WaitSeconds = 120;
        private const string ServiceName = "VideoForensics";

        public void Restart()
        {
            // Exit code 1 tells the service recovery action to restart the service.
            Environment.ExitCode = 1;
            if (isWindowsService())
            {
                // Exiting alone leaves the service stopped, so a detached helper starts it after this process has gone.
                if (!scheduleServiceStart(ServiceName))
                {
                    Serilog.Log.Error("Could not schedule the service start; start the service manually");
                }
            }
            else
            {
                // The copy starts only after this process has exited, so it never races this one for the port.
                launch(Environment.ProcessId, string.Join(" ", new[] { exePath }.Concat(args).Select(Quote)));
            }

            lifetime.StopApplication();
        }

        /// <summary>Builds the restarter for the process that is running now.</summary>
        public static AppRestarter ForCurrentProcess(IHostApplicationLifetime lifetime, Func<bool> isWindowsService)
        {
            string exe = Environment.ProcessPath ?? string.Empty;
            string[] cli = Environment.GetCommandLineArgs();

            // Under "dotnet <app>.dll" the entry point is the first argument after dotnet itself, so it has to be kept.
            List<string> args = Path.GetFileNameWithoutExtension(exe).Equals("dotnet", StringComparison.OrdinalIgnoreCase)
                ? cli.ToList()
                : cli.Skip(1).ToList();

            return new AppRestarter(lifetime, LaunchDetached, isWindowsService, name => ServiceRestartScheduler.TrySchedule(name), exe, args);
        }

        /// <summary>Starts a shell that waits for the process to exit, then runs the command. Gives up waiting after two minutes.</summary>
        private static void LaunchDetached(int processId, string command)
        {
            ProcessStartInfo info = OperatingSystem.IsWindows()
                ? new ProcessStartInfo("cmd.exe") { Arguments = $"/c \"powershell -NoProfile -WindowStyle Hidden -Command \"Wait-Process -Id {processId} -Timeout {WaitSeconds} -ErrorAction SilentlyContinue\" & {command}\"" }
                : new ProcessStartInfo("/bin/sh") { ArgumentList = { "-c", $"while kill -0 {processId} 2>/dev/null; do sleep 1; done; {command}" } };

            info.UseShellExecute = false;
            info.CreateNoWindow = true;
            _ = Process.Start(info);
        }

        private static string Quote(string value)
        {
            return "\"" + value + "\"";
        }
    }
}
