using Microsoft.Extensions.Logging;
using VideoForensics.Data.Core.Contracts;

namespace VideoForensics
{
    public interface IVideoDownloadService
    {
        Task<bool> AuthenticateAsync(string username, string password);
        /// <summary>
        /// Downloads videos in [startDate, endDate]. When force is false (default), each device's
        /// effective start date is narrowed to "since its last successful pull" via IWatermarkService,
        /// so a normal run only fetches what's new. Pass force=true to always scan the full requested
        /// window regardless of watermark (e.g. the operator explicitly wants to re-scan).
        /// </summary>
        Task<bool> DownloadVideosAsync(string outputPath, DateTime startDate, DateTime endDate, bool force = false);
        Task<bool> DownloadSnapshotsAsync(string outputPath, DateTime startDate, DateTime endDate);
        string GetDownloadStatus();
        /// <summary>Live per-file progress for the download currently in flight (files completed/total, bytes, current file).</summary>
        VideoForensics.Providers.Common.Contracts.DownloadStatus GetProgress();
        /// <summary>Drains and returns any per-file activity messages queued since the last call (e.g. "✓ file.mp4 (5.2 MB)", "✗ event 123: 404").</summary>
        IReadOnlyList<string> DrainActivityLog();
        /// <summary>How many matched items from the last download call weren't actually downloaded (e.g. a rate limit cut the run short), aggregated across all devices processed.</summary>
        int GetRemainingCount();
        /// <summary>Which device (1-based index, total count, display name) is currently being processed, so a UI can explain why the per-device file count just reset instead of it looking like a glitch.</summary>
        (int Index, int Total, string Name) GetCurrentDevice();
        string? GetLastError();
    }

    public class VideoDownloadServiceAdapter : IVideoDownloadService
    {
        private readonly ILogger<VideoDownloadServiceAdapter> _logger;
        private readonly VideoForensics.Providers.Common.Contracts.IVideoProvider _videoProvider;
        private readonly VideoForensics.Providers.Common.Contracts.IProviderAuthService _authService;
        private readonly VideoForensics.Providers.Common.Contracts.IMediaDownloadService _downloadService;
        private readonly VideoForensics.Providers.Common.Contracts.IDeviceDiscoveryService _deviceService;
        private readonly IVideoForensicsDataClient _dataClient;
        private string? _lastError;

        // Cache the User/Account/Location identity resolution so it only happens once per process
        // (mirrors the same synthetic-key pattern used in RingMediaDownloadService, but kept
        // provider-agnostic here via _videoProvider.ProviderName rather than hardcoding "Ring").
        private readonly SemaphoreSlim _identityResolutionLock = new(1, 1);
        private Guid? _cachedLocationId;
        private readonly Dictionary<string, Guid> _deviceIdCache = new();
        private int _lastRemainingCount;
        private int _currentDeviceIndex;
        private int _currentDeviceTotal;
        private string _currentDeviceName = string.Empty;
        private int _totalFilesCompleted;
        private int _totalFilesMatched;
        private long _totalBytesDownloaded;
        private DateTime _downloadStartTime;
        private long _lastBytesValue;
        private DateTime _lastSpeedCheck;

        // Tracks devices that were fully downloaded (FilesDownloaded >= FilesMatched) for the
        // current outputPath/date range, so hitting "Continue downloading" after a rate-limit pause
        // resumes at the device that still has work left instead of re-walking every already-
        // finished device (each incurring InterDeviceDelayMs and a full event-list re-scan).
        private readonly Dictionary<string, (int FilesDownloaded, int FilesMatched, long BytesDownloaded)> _completedDeviceResults = new();
        private string? _completedRunKey;

        /// <summary>Delay between device downloads to avoid rate limiting (milliseconds)</summary>
        private const int InterDeviceDelayMs = 5000;

        public VideoDownloadServiceAdapter(
            ILogger<VideoDownloadServiceAdapter> logger,
            VideoForensics.Providers.Common.Contracts.IVideoProvider videoProvider,
            VideoForensics.Providers.Common.Contracts.IProviderAuthService authService,
            VideoForensics.Providers.Common.Contracts.IMediaDownloadService downloadService,
            VideoForensics.Providers.Common.Contracts.IDeviceDiscoveryService deviceService,
            IVideoForensicsDataClient dataClient)
        {
            _logger = logger;
            _videoProvider = videoProvider;
            _authService = authService;
            _downloadService = downloadService;
            _deviceService = deviceService;
            _dataClient = dataClient;
        }

        /// <summary>
        /// Resolves (find-or-create) the Guid Device.Id for a provider's string device id, ensuring the
        /// implicit single-account/single-location chain exists first. Cached per process/device so this
        /// is only a DB round-trip on first use for a given device.
        /// </summary>
        private async Task<Guid> EnsureDeviceIdentityAsync(string providerDeviceId, string deviceName, CancellationToken ct)
        {
            if (_deviceIdCache.TryGetValue(providerDeviceId, out var cached))
                return cached;

            await _identityResolutionLock.WaitAsync(ct);
            try
            {
                if (_deviceIdCache.TryGetValue(providerDeviceId, out cached))
                    return cached;

                if (_cachedLocationId is null)
                {
                    var (_, account) = await _dataClient.EnsureUserAndAccountAsync(
                        _videoProvider.ProviderName, "default", "default", null, ct);
                    var location = await _dataClient.EnsureLocationAsync(
                        account.Id, "default", "default", null, ct);
                    _cachedLocationId = location.Id;
                }

                var device = await _dataClient.EnsureDeviceAsync(
                    _cachedLocationId.Value, providerDeviceId, deviceName, "camera", true, ct);
                _deviceIdCache[providerDeviceId] = device.Id;
                return device.Id;
            }
            finally
            {
                _identityResolutionLock.Release();
            }
        }

        public async Task<bool> AuthenticateAsync(string username, string password)
        {
            var result = await _authService.AuthenticateAsync(username, password);
            return result.Success;
        }

        public async Task<bool> DownloadVideosAsync(string outputPath, DateTime startDate, DateTime endDate, bool force = false)
        {
            _lastRemainingCount = 0;
            _currentDeviceIndex = 0;
            _currentDeviceTotal = 0;
            _currentDeviceName = string.Empty;
            _totalFilesCompleted = 0;
            _totalFilesMatched = 0;
            _totalBytesDownloaded = 0;
            _downloadStartTime = DateTime.UtcNow;
            _lastBytesValue = 0;
            _lastSpeedCheck = DateTime.UtcNow;

            // Only keep the completed-device cache when this call is a "Continue" for the same
            // outputPath/date range as the previous call — a different range means different work.
            var runKey = $"{outputPath}|{startDate:O}|{endDate:O}";
            if (_completedRunKey != runKey)
            {
                _completedDeviceResults.Clear();
                _completedRunKey = runKey;
            }

            try
            {
                _logger.LogInformation("Starting video download to {OutputPath} from {StartDate} to {EndDate}", outputPath, startDate, endDate);

                if (!await _authService.IsAuthenticatedAsync())
                {
                    _lastError = "Not authenticated. Please authenticate first.";
                    _logger.LogError(_lastError);
                    return false;
                }

                // Get available devices
                var locations = await _deviceService.GetLocationsAsync();
                if (locations == null || locations.Count == 0)
                {
                    _lastError = "No locations found for the account.";
                    _logger.LogError(_lastError);
                    return false;
                }

                _logger.LogInformation("Found {LocationCount} location(s)", locations.Count);

                // A device shared across multiple locations comes back once per location it's
                // associated with — dedupe by device id so we don't download the same camera's
                // videos twice (which also made per-device progress look like it was resetting
                // mid-run: same device, same count, appearing to "restart").
                var seenDeviceIds = new HashSet<string>();
                var uniqueDevices = new List<VideoForensics.Providers.Common.Contracts.Device>();
                foreach (var location in locations)
                {
                    _logger.LogInformation("Checking location: {LocationId}", location.Id);
                    var devices = await _deviceService.GetDevicesAsync(location.Id.ToString());
                    if (devices == null || devices.Count == 0)
                    {
                        _logger.LogWarning("No devices found in location {LocationId}", location.Id);
                        continue;
                    }

                    foreach (var device in devices)
                    {
                        if (seenDeviceIds.Add(device.Id))
                        {
                            uniqueDevices.Add(device);
                        }
                    }
                }

                _logger.LogInformation("Found {DeviceCount} unique device(s) across all locations", uniqueDevices.Count);

                // Download videos from all devices
                var totalDevices = uniqueDevices.Count;
                var totalFilesDownloaded = 0;
                var totalFilesMatched = 0;
                var deviceErrors = new List<string>();

                _currentDeviceTotal = uniqueDevices.Count;

                // Sequential device processing to avoid rate limiting
                // Process one device completely before moving to the next
                for (int i = 0; i < uniqueDevices.Count; i++)
                {
                    var device = uniqueDevices[i];
                    _currentDeviceIndex = i + 1;
                    _currentDeviceName = device.Name;

                    // Already fully downloaded on a prior pass for this exact range — resume past
                    // it without a network call or the inter-device delay.
                    if (_completedDeviceResults.TryGetValue(device.Id, out var cached))
                    {
                        totalFilesDownloaded += cached.FilesDownloaded;
                        totalFilesMatched += cached.FilesMatched;
                        _totalFilesCompleted = totalFilesDownloaded;
                        _totalFilesMatched = totalFilesMatched;
                        _totalBytesDownloaded += cached.BytesDownloaded;
                        _logger.LogInformation("⏭ Skipping {DeviceName} — already fully downloaded for this range", device.Name);
                        continue;
                    }

                    _logger.LogInformation("Downloading from device {DeviceNumber}/{TotalDevices}: {DeviceName} ({DeviceId})",
                        _currentDeviceIndex, uniqueDevices.Count, device.Name, device.Id);

                    // Resolve this device's effective start date: the full requested window when
                    // forced, otherwise narrowed to "since its last successful pull" (the watermark).
                    var effectiveStartDate = startDate;
                    try
                    {
                        var deviceGuid = await EnsureDeviceIdentityAsync(device.Id, device.Name, CancellationToken.None);
                        effectiveStartDate = await _dataClient.GetWatermarkAsync(deviceGuid, startDate, force, CancellationToken.None);
                        if (effectiveStartDate != startDate)
                        {
                            _logger.LogInformation("Using watermark-narrowed start date {StartDate} for {DeviceName} (requested {RequestedStartDate})",
                                effectiveStartDate, device.Name, startDate);
                        }
                    }
                    catch (Exception ex)
                    {
                        // A watermark-resolution failure should never block the actual download —
                        // fall back to the originally requested range for this device.
                        _logger.LogWarning(ex, "Failed to resolve watermark for {DeviceName}; using requested date range", device.Name);
                    }

                    var result = await _downloadService.DownloadVideosAsync(
                        device.Id,
                        outputPath,
                        effectiveStartDate,
                        endDate
                    );

                    if (result?.Success == true)
                    {
                        totalFilesDownloaded += result.FilesDownloaded;
                        totalFilesMatched += result.FilesMatched;
                        _totalFilesCompleted = totalFilesDownloaded;
                        _totalFilesMatched = totalFilesMatched;
                        _totalBytesDownloaded += result.BytesDownloaded;
                        _logger.LogInformation("✓ Downloaded {FileCount} video(s) from {DeviceName}",
                            result.FilesDownloaded, device.Name);

                        if (result.FilesDownloaded >= result.FilesMatched)
                        {
                            _completedDeviceResults[device.Id] = (result.FilesDownloaded, result.FilesMatched, result.BytesDownloaded);
                        }
                    }
                    else
                    {
                        var reason = result?.ErrorMessage ?? "unknown error";
                        deviceErrors.Add($"{device.Id}: {reason}");
                        _logger.LogWarning("✗ Failed to download from {DeviceName}: {Reason}",
                            device.Name, reason);
                    }

                    // Rate limit safety: only delay after a device we actually contacted — a
                    // cache-skipped device above already `continue`d past this.
                    if (i < uniqueDevices.Count - 1)
                    {
                        _logger.LogInformation("Waiting {DelayMs}ms before next device to avoid rate limiting",
                            InterDeviceDelayMs);
                        await Task.Delay(InterDeviceDelayMs);
                    }
                }

                _lastRemainingCount = Math.Max(0, totalFilesMatched - totalFilesDownloaded);

                if (totalDevices == 0)
                {
                    _lastError = "No devices found on the account.";
                    _logger.LogError(_lastError);
                    return false;
                }

                if (totalFilesDownloaded == 0)
                {
                    if (deviceErrors.Count > 0)
                    {
                        // Check if all errors are rate limit errors
                        var rateLimitErrors = deviceErrors
                            .Where(e => e.Contains("too many requests", StringComparison.OrdinalIgnoreCase) ||
                                       e.Contains("denied by Ring", StringComparison.OrdinalIgnoreCase))
                            .Count();

                        if (rateLimitErrors == deviceErrors.Count)
                        {
                            _lastError = $"Rate limit exceeded by Ring API. All {totalDevices} device(s) were rate limited. Please wait 5+ minutes before retrying.";
                        }
                        else if (rateLimitErrors > totalDevices * 0.5)
                        {
                            // More than half are rate limit errors
                            _lastError = $"Ring API rate limit hit ({rateLimitErrors}/{totalDevices} devices). Please wait a few minutes before retrying.";
                        }
                        else
                        {
                            // Mix of errors - show unique error types
                            var errorTypes = deviceErrors
                                .Select(e => e.Contains("too many requests", StringComparison.OrdinalIgnoreCase)
                                    ? "rate limited"
                                    : e.Split(':').LastOrDefault()?.Trim() ?? "unknown error")
                                .Distinct()
                                .ToList();

                            _lastError = errorTypes.Count == 1
                                ? $"No videos downloaded ({totalDevices} device(s) checked): {errorTypes[0]}"
                                : $"No videos downloaded ({totalDevices} device(s) checked): {string.Join(", ", errorTypes)}";
                        }
                    }
                    else
                    {
                        _lastError = $"No videos found in the selected date range across {totalDevices} device(s).";
                    }

                    _logger.LogError(_lastError);
                    return false;
                }

                _logger.LogInformation("Downloaded {FileCount} video(s) across {DeviceCount} device(s)", totalFilesDownloaded, totalDevices);
                return true;
            }
            catch (Exception ex)
            {
                _lastError = $"Download failed: {ex.Message}";
                _logger.LogError(ex, "Video download error");
                return false;
            }
        }

        public async Task<bool> DownloadSnapshotsAsync(string outputPath, DateTime startDate, DateTime endDate)
        {
            _lastRemainingCount = 0;
            _currentDeviceIndex = 0;
            _currentDeviceTotal = 0;
            _currentDeviceName = string.Empty;
            _totalFilesCompleted = 0;
            _totalFilesMatched = 0;
            _totalBytesDownloaded = 0;
            _downloadStartTime = DateTime.UtcNow;
            _lastBytesValue = 0;
            _lastSpeedCheck = DateTime.UtcNow;

            try
            {
                _logger.LogInformation("Starting snapshot download to {OutputPath} from {StartDate} to {EndDate}", outputPath, startDate, endDate);

                if (!await _authService.IsAuthenticatedAsync())
                {
                    _lastError = "Not authenticated. Please authenticate first.";
                    _logger.LogError(_lastError);
                    return false;
                }

                // Get available devices
                var locations = await _deviceService.GetLocationsAsync();
                if (locations == null || locations.Count == 0)
                {
                    _lastError = "No locations found for the account.";
                    _logger.LogError(_lastError);
                    return false;
                }

                _logger.LogInformation("Found {LocationCount} location(s)", locations.Count);

                // Dedupe devices shared across multiple locations (see DownloadVideosAsync).
                var seenDeviceIds = new HashSet<string>();
                var uniqueDevices = new List<VideoForensics.Providers.Common.Contracts.Device>();
                foreach (var location in locations)
                {
                    _logger.LogInformation("Checking location: {LocationId}", location.Id);
                    var devices = await _deviceService.GetDevicesAsync(location.Id.ToString());
                    if (devices == null || devices.Count == 0)
                    {
                        _logger.LogWarning("No devices found in location {LocationId}", location.Id);
                        continue;
                    }

                    foreach (var device in devices)
                    {
                        if (seenDeviceIds.Add(device.Id))
                        {
                            uniqueDevices.Add(device);
                        }
                    }
                }

                _logger.LogInformation("Found {DeviceCount} unique device(s) across all locations", uniqueDevices.Count);

                // Download snapshots from all devices
                var totalDevices = uniqueDevices.Count;
                var totalFilesDownloaded = 0;
                var deviceErrors = new List<string>();

                _currentDeviceTotal = uniqueDevices.Count;

                // Sequential device processing to avoid rate limiting
                for (int i = 0; i < uniqueDevices.Count; i++)
                {
                    var device = uniqueDevices[i];
                    _currentDeviceIndex = i + 1;
                    _currentDeviceName = device.Name;

                    _logger.LogInformation("Downloading from device {DeviceNumber}/{TotalDevices}: {DeviceName} ({DeviceId})",
                        _currentDeviceIndex, uniqueDevices.Count, device.Name, device.Id);

                    var result = await _downloadService.DownloadSnapshotsAsync(
                        device.Id,
                        outputPath,
                        startDate,
                        endDate
                    );

                    if (result?.Success == true)
                    {
                        totalFilesDownloaded += result.FilesDownloaded;
                        _totalFilesCompleted = totalFilesDownloaded;
                        _totalFilesMatched += 1;
                        _totalBytesDownloaded += result.BytesDownloaded;
                        _logger.LogInformation("✓ Downloaded {FileCount} snapshot(s) from {DeviceName}",
                            result.FilesDownloaded, device.Name);
                    }
                    else
                    {
                        var reason = result?.ErrorMessage ?? "unknown error";
                        deviceErrors.Add($"{device.Id}: {reason}");
                        _logger.LogWarning("✗ Failed to download from {DeviceName}: {Reason}",
                            device.Name, reason);
                    }

                    // Rate limit safety: sequential processing with inter-device delay
                    if (i < uniqueDevices.Count - 1)
                    {
                        _logger.LogInformation("Waiting {DelayMs}ms before next device to avoid rate limiting",
                            InterDeviceDelayMs);
                        await Task.Delay(InterDeviceDelayMs);
                    }
                }

                if (totalDevices == 0)
                {
                    _lastError = "No devices found on the account.";
                    _logger.LogError(_lastError);
                    return false;
                }

                if (totalFilesDownloaded == 0)
                {
                    _lastError = deviceErrors.Count > 0
                        ? $"No snapshots downloaded ({totalDevices} device(s) checked): {string.Join("; ", deviceErrors)}"
                        : $"No snapshot was available from any of the {totalDevices} device(s).";
                    _logger.LogError(_lastError);
                    return false;
                }

                _logger.LogInformation("Downloaded {FileCount} snapshot(s) across {DeviceCount} device(s)", totalFilesDownloaded, totalDevices);
                return true;
            }
            catch (Exception ex)
            {
                _lastError = $"Download failed: {ex.Message}";
                _logger.LogError(ex, "Snapshot download error");
                return false;
            }
        }

        public string GetDownloadStatus()
        {
            return "Ready";
        }

        public VideoForensics.Providers.Common.Contracts.DownloadStatus GetProgress()
        {
            var status = _downloadService.GetStatus();

            // _totalBytesDownloaded only advances once a device fully finishes, so mid-device it
            // sits frozen — use the live grand total (completed devices + the current device's
            // in-flight bytes) or the speed reads 0 for the entire time a device is downloading.
            var liveTotalBytes = _totalBytesDownloaded + status.BytesDownloaded;

            var now = DateTime.UtcNow;
            var elapsed = now - _lastSpeedCheck;
            var currentSpeed = 0.0;

            if (elapsed.TotalSeconds >= 1)
            {
                // Guard against a transient dip right as one device's live total is replaced by the
                // next device's (status.BytesDownloaded resets to 0 a moment before
                // _totalBytesDownloaded catches up), which would otherwise show a negative rate.
                var bytesDelta = Math.Max(0, liveTotalBytes - _lastBytesValue);
                currentSpeed = (bytesDelta * 8) / (1_000_000 * elapsed.TotalSeconds);
                _lastBytesValue = liveTotalBytes;
                _lastSpeedCheck = now;
            }

            // Return enriched status with aggregated data and speed
            return new VideoForensics.Providers.Common.Contracts.DownloadStatus(
                IsDownloading: status.IsDownloading,
                FilesCompleted: status.FilesCompleted,
                FilesTotal: status.FilesTotal,
                BytesDownloaded: status.BytesDownloaded,
                CurrentFile: status.CurrentFile,
                TotalFilesCompleted: _totalFilesCompleted,
                TotalFilesMatched: _totalFilesMatched,
                TotalBytesDownloaded: _totalBytesDownloaded,
                ActiveConnections: status.ActiveConnections,
                CurrentSpeedMbps: currentSpeed
            );
        }

        public IReadOnlyList<string> DrainActivityLog()
        {
            return _downloadService.DrainActivityLog();
        }

        public int GetRemainingCount()
        {
            return _lastRemainingCount;
        }

        public (int Index, int Total, string Name) GetCurrentDevice()
        {
            return (_currentDeviceIndex, _currentDeviceTotal, _currentDeviceName);
        }

        public string? GetLastError()
        {
            return _lastError;
        }
    }
}
