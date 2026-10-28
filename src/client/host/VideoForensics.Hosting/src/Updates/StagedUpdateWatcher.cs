using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace VideoForensics.Client.Hosting.Updates
{
    /// <summary>
    /// Runs before the service exits for an update: stops new work and lets in-flight work checkpoint, so the restart
    /// resumes it. The scheduler provides the real implementation; until then <see cref="NoOpUpdateDrain"/> is used.
    /// </summary>
    public interface IUpdateDrain
    {
        /// <summary>Completes when the service is idle enough to restart.</summary>
        Task DrainAsync(CancellationToken ct);
    }

    /// <summary>Drain used before the scheduler supports it: nothing is waited for.</summary>
    public sealed class NoOpUpdateDrain : IUpdateDrain
    {
        /// <inheritdoc />
        public Task DrainAsync(CancellationToken ct) => Task.CompletedTask;
    }

    /// <summary>
    /// Notices a package staged while the service is running, drains, and exits so the service manager restarts it
    /// and <see cref="StartupUpdateApplier"/> applies the package. The exit is abrupt on purpose: a graceful stop
    /// reports "stopped" to the service manager, which then does not run the restart recovery action.
    /// </summary>
    public sealed class StagedUpdateWatcher : BackgroundService
    {
        private readonly string _updatesRoot;
        private readonly IUpdatePackageVerifier _verifier;
        private readonly IUpdateDrain _drain;
        private readonly bool _allowDevPackages;
        private readonly Func<string> _currentVersion;
        private readonly Action<int> _exit;
        private readonly TimeSpan _pollInterval;
        private readonly ILogger<StagedUpdateWatcher> _logger;
        private readonly HashSet<string> _ignored = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Creates the watcher. <paramref name="exit"/> is <see cref="Environment.Exit(int)"/> in production.</summary>
        public StagedUpdateWatcher(
            string updatesRoot,
            IUpdatePackageVerifier verifier,
            IUpdateDrain drain,
            bool allowDevPackages,
            Func<string> currentVersion,
            Action<int> exit,
            TimeSpan pollInterval,
            ILogger<StagedUpdateWatcher> logger)
        {
            _updatesRoot = updatesRoot ?? throw new ArgumentNullException(nameof(updatesRoot));
            _verifier = verifier ?? throw new ArgumentNullException(nameof(verifier));
            _drain = drain ?? throw new ArgumentNullException(nameof(drain));
            _allowDevPackages = allowDevPackages;
            _currentVersion = currentVersion ?? throw new ArgumentNullException(nameof(currentVersion));
            _exit = exit ?? throw new ArgumentNullException(nameof(exit));
            _pollInterval = pollInterval;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    if (await CheckOnceAsync(stoppingToken))
                    {
                        return;
                    }

                    await Task.Delay(_pollInterval, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (Exception ex)
                {
                    _logger.LogError("Staged update check failed: {Error} ({ExceptionType})", ex.Message, ex.GetType().Name);
                    await Task.Delay(_pollInterval, stoppingToken);
                }
            }
        }

        /// <summary>Checks the updates folder once. Returns true when a valid package was found and the exit was requested.</summary>
        internal async Task<bool> CheckOnceAsync(CancellationToken ct)
        {
            string? candidate = StartupUpdateApplier.FindNewestStaged(_updatesRoot);
            if (candidate is null || _ignored.Contains(candidate))
            {
                return false;
            }

            UpdatePackageVerification verification = await _verifier.VerifyAsync(candidate, _currentVersion(), _allowDevPackages, ct);
            if (!verification.IsValid)
            {
                // Left in place for the startup applier to reject and rename; not re-verified on every poll.
                _ = _ignored.Add(candidate);
                _logger.LogError("Staged update at '{Folder}' was not accepted: {Message}", Path.GetFileName(candidate), verification.Message);
                return false;
            }

            _logger.LogInformation("Staged update {Version} found; draining and restarting to apply it", verification.Manifest?.Version);
            await _drain.DrainAsync(ct);
            _exit(1);
            return true;
        }
    }
}
