using CliWrap;

using CliWrap.Buffered;

using Microsoft.Extensions.Logging;

using System.Runtime.Versioning;

namespace VideoForensics.Client.Platform.Windows
{
    /// <summary>Outcome of <see cref="WindowsServiceRecoveryConfigurator.EnsureAsync"/>.</summary>
    /// <param name="Success">True when the service restarts itself after it stops unexpectedly or exits non-zero.</param>
    /// <param name="Changed">True when the recovery settings were rewritten, false when they were already right.</param>
    /// <param name="Message">Human-readable detail for the log.</param>
    public sealed record ServiceRecoveryResult(bool Success, bool Changed, string Message);

    /// <summary>
    /// Makes sure the service restarts itself after a non-zero exit. In-place updates depend on this: the process that applies
    /// an update still has the old files mapped, so it exits and relies on the service manager to start the new version.
    /// <para>
    /// The installer sets the restart actions on a fresh install, but an exit with a non-zero code only triggers them when
    /// <c>failureflag</c> is 1, and installs that predate that setting (or were changed by hand) may lack either. The service
    /// runs as SYSTEM and may configure its own recovery, so no admin step is needed.
    /// </para>
    /// </summary>
    [SupportedOSPlatform("windows")]
    public class WindowsServiceRecoveryConfigurator
    {
        private readonly ILogger<WindowsServiceRecoveryConfigurator> _logger;

        /// <summary>Creates the configurator.</summary>
        public WindowsServiceRecoveryConfigurator(ILogger<WindowsServiceRecoveryConfigurator> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>Builds the <c>sc.exe</c> arguments that set three restart actions, same shape as the installer's.</summary>
        public static string BuildFailureArguments(string serviceName) =>
            $"failure {serviceName} reset= 86400 actions= restart/5000/restart/5000/restart/5000";

        /// <summary>Builds the <c>sc.exe</c> arguments that make a non-zero exit (not only a crash) trigger the recovery actions.</summary>
        public static string BuildFailureFlagArguments(string serviceName) => $"failureflag {serviceName} 1";

        /// <summary>Builds the <c>sc.exe</c> arguments that read the failure actions.</summary>
        public static string BuildQueryFailureArguments(string serviceName) => $"qfailure {serviceName}";

        /// <summary>Builds the <c>sc.exe</c> arguments that read the failure flag.</summary>
        public static string BuildQueryFailureFlagArguments(string serviceName) => $"qfailureflag {serviceName}";

        /// <summary>True when <c>sc qfailure</c> output lists a restart action.</summary>
        public static bool HasRestartAction(string qfailureOutput) =>
            qfailureOutput.Contains("RESTART", StringComparison.OrdinalIgnoreCase);

        /// <summary>True when <c>sc qfailureflag</c> output says non-crash failures trigger the actions.</summary>
        public static bool IsFailureFlagEnabled(string qfailureFlagOutput) =>
            qfailureFlagOutput.Contains("TRUE", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Reads the service's recovery settings and rewrites them when they would not restart it after a non-zero exit.
        /// Never throws: a failure is returned so the caller can skip applying an update.
        /// </summary>
        public async Task<ServiceRecoveryResult> EnsureAsync(string serviceName, CancellationToken ct)
        {
            try
            {
                BufferedCommandResult actions = await RunScAsync(BuildQueryFailureArguments(serviceName), ct);
                BufferedCommandResult flag = await RunScAsync(BuildQueryFailureFlagArguments(serviceName), ct);
                if (actions.ExitCode == 0 && flag.ExitCode == 0 && HasRestartAction(actions.StandardOutput) && IsFailureFlagEnabled(flag.StandardOutput))
                {
                    _logger.LogDebug("Service '{ServiceName}' already restarts itself after a non-zero exit", serviceName);
                    return new ServiceRecoveryResult(true, false, "recovery already configured");
                }

                BufferedCommandResult setActions = await RunScAsync(BuildFailureArguments(serviceName), ct);
                BufferedCommandResult setFlag = await RunScAsync(BuildFailureFlagArguments(serviceName), ct);
                if (setActions.ExitCode != 0 || setFlag.ExitCode != 0)
                {
                    string detail = $"sc.exe failed (failure={setActions.ExitCode}, failureflag={setFlag.ExitCode}): {setActions.StandardOutput}{setFlag.StandardOutput}".Trim();
                    _logger.LogError("Could not configure recovery for service '{ServiceName}': {Detail}", serviceName, detail);
                    return new ServiceRecoveryResult(false, false, detail);
                }

                _logger.LogInformation("Configured service '{ServiceName}' to restart itself after a non-zero exit", serviceName);
                return new ServiceRecoveryResult(true, true, "recovery configured");
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError("Could not configure recovery for service '{ServiceName}': {Error} ({ExceptionType})", serviceName, ex.Message, ex.GetType().Name);
                return new ServiceRecoveryResult(false, false, ex.Message);
            }
        }

        private static Task<BufferedCommandResult> RunScAsync(string arguments, CancellationToken ct) =>
            Cli.Wrap("sc.exe")
                .WithArguments(arguments)
                .WithValidation(CommandResultValidation.None)
                .ExecuteBufferedAsync(ct);
    }
}
