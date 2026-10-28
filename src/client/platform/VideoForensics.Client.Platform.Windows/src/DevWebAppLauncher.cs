using System.Diagnostics;

namespace VideoForensics.Client.Platform.Windows
{
    /// <summary>Checks whether the WebApp answers its health endpoint.</summary>
    public interface IWebAppHealthProbe
    {
        /// <summary>Returns true when the health endpoint answers with a success status.</summary>
        Task<bool> IsHealthyAsync(CancellationToken ct);
    }

    /// <summary>Runs the update stage script as the current user.</summary>
    public interface IStageScriptRunner
    {
        /// <summary>
        /// Runs the script and returns its exit code. Returns null when the script never started,
        /// for example when PowerShell could not be launched.
        /// </summary>
        Task<int?> RunAsync(string scriptPath, CancellationToken ct);
    }

    /// <summary>How a launch attempt ended.</summary>
    public enum DevWebAppLaunchStatus
    {
        /// <summary>The package was staged, the service applied it, and the WebApp answered its health check.</summary>
        Ready,

        /// <summary>The script never ran (the process did not start).</summary>
        ScriptNotRun,

        /// <summary>The script ran and exited non-zero.</summary>
        ScriptFailed,

        /// <summary>The package was staged, but the service did not consume it and answer its health check before the timeout.</summary>
        NotReady,
    }

    /// <summary>Outcome of one launch attempt, with a message suitable for the debug log.</summary>
    public sealed record DevWebAppLaunchResult(DevWebAppLaunchStatus Status, string Message);

    /// <summary>
    /// Debug-only: stages the current source as an update for the installed WebApp service and waits until the service has
    /// applied it and answers, so the MAUI app starts against a live server on the latest build. The script runs on every
    /// call; a healthy probe does not skip it, because probing first would leave the service on stale code. A health
    /// check alone is not enough afterwards either: the old instance keeps answering until it restarts, so the launcher
    /// also waits for the staged package to be consumed.
    /// </summary>
    public sealed class DevWebAppLauncher
    {
        private readonly IWebAppHealthProbe _probe;
        private readonly IStageScriptRunner _runner;
        private readonly Func<bool> _hasPendingUpdate;
        private readonly TimeSpan _readyTimeout;
        private readonly TimeSpan _pollInterval;

        /// <summary>Creates the launcher.</summary>
        /// <param name="probe">Health check used after the script succeeds.</param>
        /// <param name="runner">Runs the stage script.</param>
        /// <param name="hasPendingUpdate">True while a staged package is waiting for the service to apply it.</param>
        /// <param name="readyTimeout">How long to wait for health after the script succeeds.</param>
        /// <param name="pollInterval">Delay between health checks.</param>
        public DevWebAppLauncher(IWebAppHealthProbe probe, IStageScriptRunner runner, Func<bool> hasPendingUpdate, TimeSpan readyTimeout, TimeSpan pollInterval)
        {
            _probe = probe;
            _runner = runner;
            _hasPendingUpdate = hasPendingUpdate;
            _readyTimeout = readyTimeout;
            _pollInterval = pollInterval;
        }

        /// <summary>Runs the stage script, then waits until the service has applied the package and passes its health check.</summary>
        /// <param name="scriptPath">Full path to stage-update.ps1.</param>
        /// <param name="ct">Cancels the wait; the staged package stays in place and is still applied by the service.</param>
        public async Task<DevWebAppLaunchResult> EnsureReadyAsync(string scriptPath, CancellationToken ct)
        {
            int? exitCode = await _runner.RunAsync(scriptPath, ct);
            if (exitCode is null)
            {
                return new DevWebAppLaunchResult(DevWebAppLaunchStatus.ScriptNotRun,
                    "stage script did not run (PowerShell could not be started)");
            }

            if (exitCode != 0)
            {
                return new DevWebAppLaunchResult(DevWebAppLaunchStatus.ScriptFailed,
                    $"stage script exited with code {exitCode}");
            }

            // Stopwatch rather than DateTime arithmetic: the timeout measures elapsed time and should not jump with the wall clock.
            long start = Stopwatch.GetTimestamp();
            while (true)
            {
                // Pending is checked first: while the package is still staged the answering instance is the old build.
                bool pending = _hasPendingUpdate();
                if (!pending && await _probe.IsHealthyAsync(ct))
                {
                    return new DevWebAppLaunchResult(DevWebAppLaunchStatus.Ready, "WebApp is running the staged update");
                }

                if (Stopwatch.GetElapsedTime(start) >= _readyTimeout)
                {
                    string reason = pending
                        ? "the service did not apply the staged update (is enable-dev-updates.ps1 run, and the service running?)"
                        : "the WebApp did not answer its health check";
                    return new DevWebAppLaunchResult(DevWebAppLaunchStatus.NotReady,
                        $"update staged but {reason} within {_readyTimeout.TotalSeconds:0} s");
                }

                await Task.Delay(_pollInterval, ct);
            }
        }
    }
}
