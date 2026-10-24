using System.Diagnostics;

namespace VideoForensics.Client.Platform.Windows
{
    /// <summary>Checks whether the WebApp answers its health endpoint.</summary>
    public interface IWebAppHealthProbe
    {
        /// <summary>Returns true when the health endpoint answers with a success status.</summary>
        Task<bool> IsHealthyAsync(CancellationToken ct);
    }

    /// <summary>Runs the hot-swap script elevated.</summary>
    public interface IHotswapScriptRunner
    {
        /// <summary>
        /// Runs the script and returns its exit code. Returns null when the script never started,
        /// for example when the user declined the elevation prompt.
        /// </summary>
        Task<int?> RunAsync(string scriptPath, CancellationToken ct);
    }

    /// <summary>How a launch attempt ended.</summary>
    public enum DevWebAppLaunchStatus
    {
        /// <summary>The script succeeded and the WebApp answered its health check.</summary>
        Ready,

        /// <summary>The script never ran (elevation declined or the process did not start).</summary>
        ScriptNotRun,

        /// <summary>The script ran and exited non-zero.</summary>
        ScriptFailed,

        /// <summary>The script succeeded but the health check did not pass before the timeout.</summary>
        NotReady,
    }

    /// <summary>Outcome of one launch attempt, with a message suitable for the debug log.</summary>
    public sealed record DevWebAppLaunchResult(DevWebAppLaunchStatus Status, string Message);

    /// <summary>
    /// Debug-only: hot-swaps the installed WebApp service and waits until it answers, so the MAUI app starts against a
    /// live server. The script runs on every call. A healthy probe does not skip it, because only the script copies
    /// new binaries into the service; probing first would leave the service on stale code.
    /// </summary>
    public sealed class DevWebAppLauncher
    {
        private readonly IWebAppHealthProbe _probe;
        private readonly IHotswapScriptRunner _runner;
        private readonly TimeSpan _readyTimeout;
        private readonly TimeSpan _pollInterval;

        /// <summary>Creates the launcher.</summary>
        /// <param name="probe">Health check used after the script succeeds.</param>
        /// <param name="runner">Runs the script elevated.</param>
        /// <param name="readyTimeout">How long to wait for health after the script succeeds.</param>
        /// <param name="pollInterval">Delay between health checks.</param>
        public DevWebAppLauncher(IWebAppHealthProbe probe, IHotswapScriptRunner runner, TimeSpan readyTimeout, TimeSpan pollInterval)
        {
            _probe = probe;
            _runner = runner;
            _readyTimeout = readyTimeout;
            _pollInterval = pollInterval;
        }

        /// <summary>Runs the hot-swap script, then polls health until it passes or the timeout expires.</summary>
        /// <param name="scriptPath">Full path to hotswap-service.ps1.</param>
        /// <param name="ct">Cancels the wait; the elevated script keeps running if it has already started.</param>
        public async Task<DevWebAppLaunchResult> EnsureReadyAsync(string scriptPath, CancellationToken ct)
        {
            int? exitCode = await _runner.RunAsync(scriptPath, ct);
            if (exitCode is null)
            {
                return new DevWebAppLaunchResult(DevWebAppLaunchStatus.ScriptNotRun,
                    "hot-swap script did not run (elevation declined or the process did not start)");
            }

            if (exitCode != 0)
            {
                return new DevWebAppLaunchResult(DevWebAppLaunchStatus.ScriptFailed,
                    $"hot-swap script exited with code {exitCode}");
            }

            // Stopwatch rather than DateTime arithmetic: the timeout measures elapsed time and should not jump with the wall clock.
            long start = Stopwatch.GetTimestamp();
            while (true)
            {
                if (await _probe.IsHealthyAsync(ct))
                {
                    return new DevWebAppLaunchResult(DevWebAppLaunchStatus.Ready, "WebApp is running after hot-swap");
                }

                if (Stopwatch.GetElapsedTime(start) >= _readyTimeout)
                {
                    return new DevWebAppLaunchResult(DevWebAppLaunchStatus.NotReady,
                        $"hot-swap succeeded but the WebApp did not answer its health check within {_readyTimeout.TotalSeconds:0} s");
                }

                await Task.Delay(_pollInterval, ct);
            }
        }
    }
}
