using CliWrap;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using System.Diagnostics;
using System.Runtime.InteropServices;

using VideoForensics.Client.Common.Contracts;

namespace VideoForensics.Hosting.BackgroundServices
{
    /// <summary>
    /// Interface for the platform-specific update installer.
    /// Windows: downloads to temp, launches with /passive flag (non-interactive).
    /// Debian/Linux: downloads to a pending-update location; does not auto-invoke dpkg (requires root + manual admin action).
    /// </summary>
    public interface IUpdateInstaller
    {
        /// <summary>Downloads the release asset and invokes the installer on the current platform.</summary>
        Task DownloadAndInvokeAsync(GitHubReleaseAsset asset, CancellationToken ct);
    }

    /// <summary>
    /// Periodically checks GitHub for a newer release (Stable or Dev channel per config)
    /// and either just reports it's available (NotifyOnly) or downloads and launches the installer
    /// (AutoDownloadAndInstall) based on user configuration.
    ///
    /// Windows: the installer is launched with /passive for a non-interactive update.
    /// Debian/Linux: the installer is downloaded to a pending-update location; the admin
    /// must finish the dpkg install manually since it requires root privileges.
    /// </summary>
    public class UpdateCheckService : BackgroundService, IUpdateCheckService
    {
        private static readonly TimeSpan BaseInterval = TimeSpan.FromHours(24);

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IForensicsConfiguration _config;
        private readonly IGitHubReleaseClient _gitHubClient;
        private readonly IUpdateInstaller _updateInstaller;
        private readonly Func<string> _currentVersionProvider;
        private readonly ILogger<UpdateCheckService> _logger;

        private readonly object _lock = new();

        private bool _updateAvailable;
        private string? _latestVersion;
        private string _currentVersion;
        private string? _downloadUrl;
        private DateTime? _lastCheckedUtc;
        private string? _errorMessage;

        /// <summary>
        /// Initializes a new instance of UpdateCheckService.
        /// </summary>
        /// <param name="scopeFactory">Service scope factory for scoped dependencies per tick.</param>
        /// <param name="config">Forensics configuration (enables/disables checks, sets interval, channel, mode).</param>
        /// <param name="gitHubClient">GitHub release client for fetching version info.</param>
        /// <param name="updateInstaller">Platform-specific installer (Windows: launch MSI/EXE; Debian: download to pending location).</param>
        /// <param name="currentVersionProvider">Delegate to provide the current version; defaults to reading FileVersionInfo if null.</param>
        /// <param name="logger">Logger for diagnostic output.</param>
        public UpdateCheckService(
            IServiceScopeFactory scopeFactory,
            IForensicsConfiguration config,
            IGitHubReleaseClient gitHubClient,
            IUpdateInstaller updateInstaller,
            Func<string>? currentVersionProvider,
            ILogger<UpdateCheckService> logger)
        {
            _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _gitHubClient = gitHubClient ?? throw new ArgumentNullException(nameof(gitHubClient));
            _updateInstaller = updateInstaller ?? throw new ArgumentNullException(nameof(updateInstaller));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _currentVersionProvider = currentVersionProvider ?? GetDefaultCurrentVersion;
            _currentVersion = _currentVersionProvider();
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Calculate interval from config, defaulting to 24 hours if invalid.
            TimeSpan interval = _config.UpdateCheckIntervalHours > 0
                ? TimeSpan.FromHours(_config.UpdateCheckIntervalHours)
                : BaseInterval;

            using var timer = new PeriodicTimer(interval);

            do
            {
                if (!_config.EnableUpdateCheck)
                {
                    _logger.LogDebug("Update check is disabled via configuration; skipping this tick");
                    continue;
                }

                await RunOneTickAsync(stoppingToken);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }

        /// <summary>
        /// Internal method for direct testability without driving the whole BackgroundService lifecycle/timer.
        /// Performs one complete check: fetch latest release, compare versions, and optionally install if configured.
        /// </summary>
        internal async Task RunOneTickAsync(CancellationToken ct)
        {
            if (!_config.EnableUpdateCheck)
            {
                _logger.LogDebug("Update check is disabled via configuration; skipping this tick");
                return;
            }

            try
            {
                // Fetch the latest release from GitHub based on the configured release channel.
                GitHubReleaseInfo? release = _config.ReleaseChannel switch
                {
                    UpdateReleaseChannel.Dev => await _gitHubClient.GetLatestDevReleaseAsync(ct),
                    _ => await _gitHubClient.GetLatestReleaseAsync(ct)
                };

                if (release == null)
                {
                    lock (_lock)
                    {
                        _errorMessage = "GitHub API returned no release (null). Check network connectivity or GitHub status.";
                        _lastCheckedUtc = DateTime.UtcNow;
                        _updateAvailable = false;
                    }

                    _logger.LogWarning("Update check tick: GitHub API returned null");
                    return;
                }

                // Parse the release tag (strip leading 'v' if present) to extract version.
                string tagVersion = release.TagName.TrimStart('v');
                if (!System.Version.TryParse(ExtractBaseVersion(tagVersion), out System.Version? latestParsed))
                {
                    lock (_lock)
                    {
                        _errorMessage = $"Could not parse release tag '{release.TagName}' as a valid version.";
                        _lastCheckedUtc = DateTime.UtcNow;
                        _updateAvailable = false;
                    }

                    _logger.LogWarning("Update check tick: Failed to parse release tag '{TagName}'", release.TagName);
                    return;
                }

                // Parse the current version similarly.
                if (!System.Version.TryParse(ExtractBaseVersion(_currentVersion), out System.Version? currentParsed))
                {
                    lock (_lock)
                    {
                        _errorMessage = $"Could not parse current version '{_currentVersion}' as a valid version.";
                        _lastCheckedUtc = DateTime.UtcNow;
                        _updateAvailable = false;
                    }

                    _logger.LogWarning("Update check tick: Failed to parse current version '{CurrentVersion}'", _currentVersion);
                    return;
                }

                // Compare versions: newer if latest > current.
                bool isNewer = latestParsed > currentParsed;

                // Try to find a matching platform asset.
                GitHubReleaseAsset? matchingAsset = FindPlatformAsset(release.Assets);
                string? downloadUrl = matchingAsset?.BrowserDownloadUrl;

                // If AutoDownloadAndInstall and newer version found and asset available, invoke the installer.
                if (isNewer && _config.UpdateMode == UpdateCheckMode.AutoDownloadAndInstall && matchingAsset != null)
                {
                    try
                    {
                        await _updateInstaller.DownloadAndInvokeAsync(matchingAsset, ct);
                        _logger.LogInformation("Update check tick: Auto-downloaded and invoked installer for v{LatestVersion}", tagVersion);
                    }
                    catch (Exception ex)
                    {
                        // Installer failure is non-critical; log it but don't crash the tick.
                        _logger.LogWarning(ex, "Update check tick: Failed to download/invoke installer (non-critical)");
                    }
                }

                // Update state snapshot.
                lock (_lock)
                {
                    _updateAvailable = isNewer;
                    _latestVersion = isNewer ? tagVersion : null;
                    _downloadUrl = isNewer ? downloadUrl : null;
                    _errorMessage = null;
                    _lastCheckedUtc = DateTime.UtcNow;
                }

                if (isNewer)
                {
                    _logger.LogInformation("Update check tick: Newer version available: v{LatestVersion}", tagVersion);
                }
                else
                {
                    _logger.LogDebug("Update check tick: Already on latest version ({CurrentVersion})", _currentVersion);
                }
            }
            catch (Exception ex)
            {
                // Catch any unexpected exception, log it, and update state with error.
                lock (_lock)
                {
                    _errorMessage = $"Unexpected error during update check: {ex.Message}";
                    _lastCheckedUtc = DateTime.UtcNow;
                    _updateAvailable = false;
                }

                _logger.LogWarning(ex, "Update check tick: Unexpected error (non-critical)");
            }
        }

        /// <summary>
        /// Triggers an immediate update check without waiting for the periodic timer.
        /// Simply delegates to RunOneTickAsync.
        /// </summary>
        public Task TriggerCheckNowAsync(CancellationToken ct)
        {
            return RunOneTickAsync(ct);
        }

        /// <summary>
        /// Returns a thread-safe snapshot of the current update-check state.
        /// </summary>
        public UpdateCheckState GetState()
        {
            lock (_lock)
            {
                return new UpdateCheckState(
                    _updateAvailable,
                    _latestVersion,
                    _currentVersion,
                    _downloadUrl,
                    _lastCheckedUtc,
                    _errorMessage);
            }
        }

        /// <summary>
        /// Default version provider: reads from the running assembly's FileVersionInfo.
        /// </summary>
        private static string GetDefaultCurrentVersion()
        {
            try
            {
                string executingPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                string? productVersion = FileVersionInfo.GetVersionInfo(executingPath).ProductVersion;
                return !string.IsNullOrEmpty(productVersion)
                    ? productVersion
                    : "0.0.0";
            }
            catch
            {
                return "0.0.0";
            }
        }

        /// <summary>
        /// Extracts the base semantic version (major.minor.patch) from a version string that may contain
        /// prerelease (-) or build (+) metadata. E.g., "1.2.3-rc1+build123" -> "1.2.3".
        /// </summary>
        private static string ExtractBaseVersion(string version)
        {
            int dashIndex = version.IndexOf('-');
            int plusIndex = version.IndexOf('+');

            int cutoffIndex = version.Length;
            if (dashIndex >= 0)
            {
                cutoffIndex = Math.Min(cutoffIndex, dashIndex);
            }

            if (plusIndex >= 0)
            {
                cutoffIndex = Math.Min(cutoffIndex, plusIndex);
            }

            return version[..cutoffIndex];
        }

        /// <summary>
        /// Finds a platform-matching asset from the release's asset list:
        /// - Windows: looks for an asset ending with .exe
        /// - Other platforms: looks for an asset ending with .deb
        /// Returns null if no matching asset is found.
        /// </summary>
        private static GitHubReleaseAsset? FindPlatformAsset(IReadOnlyList<GitHubReleaseAsset> assets)
        {
            if (assets.Count == 0)
            {
                return null;
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return assets.FirstOrDefault(a => a.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase));
            }

            // Assume Linux/Debian by default.
            return assets.FirstOrDefault(a => a.Name.EndsWith(".deb", StringComparison.OrdinalIgnoreCase));
        }
    }

    /// <summary>
    /// Concrete implementation of IUpdateInstaller.
    /// On Windows: downloads the installer to a temp location and launches it with /passive flag for a non-interactive install.
    /// On Debian/Linux: downloads to a pending-update location; the admin must manually complete the dpkg install since it requires root.
    /// </summary>
    public class UpdateInstaller : IUpdateInstaller
    {
        private readonly ILogger<UpdateInstaller> _logger;

        public UpdateInstaller(ILogger<UpdateInstaller> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task DownloadAndInvokeAsync(GitHubReleaseAsset asset, CancellationToken ct)
        {
            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    await DownloadAndInvokeOnWindowsAsync(asset, ct);
                }
                else
                {
                    await DownloadForLinuxAsync(asset, ct);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to download/invoke installer for asset '{AssetName}'", asset.Name);
                throw;
            }
        }

        private static async Task DownloadAndInvokeOnWindowsAsync(GitHubReleaseAsset asset, CancellationToken ct)
        {
            string tempPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), asset.Name);

            using (var client = new HttpClient())
            {
                await using var response = await client.GetStreamAsync(asset.BrowserDownloadUrl, ct);
                await using var fileStream = System.IO.File.Create(tempPath);
                await response.CopyToAsync(fileStream, ct);
            }

            // Fire-and-forget: launch the installer with /passive flag (non-interactive).
            // CliWrap handles the process lifecycle; we don't await it directly so the update
            // can proceed without blocking the application.
            _ = Cli.Wrap(tempPath)
                .WithArguments("/passive")
                .ExecuteAsync(ct);
        }

        private static async Task DownloadForLinuxAsync(GitHubReleaseAsset asset, CancellationToken ct)
        {
            // On Linux, download to a well-known pending-update location (e.g., /var/lib/videoforensics/pending-update/).
            // The admin will need to manually complete the dpkg install.
            string pendingDir = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "VideoForensics",
                "pending-update");

            System.IO.Directory.CreateDirectory(pendingDir);

            string pendingPath = System.IO.Path.Combine(pendingDir, asset.Name);

            using (var client = new HttpClient())
            {
                await using var response = await client.GetStreamAsync(asset.BrowserDownloadUrl, ct);
                await using var fileStream = System.IO.File.Create(pendingPath);
                await response.CopyToAsync(fileStream, ct);
            }

            // On Linux, the admin must manually run: sudo dpkg -i /path/to/the.deb
            // Log this as info so the system log shows where to find the file.
        }
    }
}
