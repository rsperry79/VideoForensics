using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using VideoForensics.Client.Common;
using VideoForensics.Client.Core.Utilities;
using VideoForensics.Data.Core.Contracts;

namespace VideoForensics.Client.Core
{
    public class VideoDownloadServiceAdapter : IVideoDownloadService
    {
        private readonly ILogger<VideoDownloadServiceAdapter> _logger;
        private readonly VideoForensics.Providers.Common.Contracts.IVideoProvider _videoProvider;
        private readonly VideoForensics.Providers.Common.Contracts.IProviderAuthService _authService;
        private readonly VideoForensics.Providers.Common.Contracts.IMediaDownloadService _downloadService;
        private readonly VideoForensics.Providers.Common.Contracts.IDeviceDiscoveryService _deviceService;
        private readonly IVideoForensicsDataClient _dataClient;
        private readonly IForensicsConfiguration _forensicsConfig;
        private string? _lastError;

        // Cache the User/Account/Location identity resolution so it only happens once per process
        // (mirrors the same synthetic-key pattern used in RingMediaDownloadService, but kept
        // provider-agnostic here via _videoProvider.ProviderName rather than hardcoding "Ring").
        private readonly SemaphoreSlim _identityResolutionLock = new(1, 1);
        // Keyed by the real Ring provider location id (not a single global cache) — an account can
        // have more than one location, and each must map to its own Location row, not all devices
        // collapsing into one synthetic "default" location.
        private readonly Dictionary<string, Guid> _locationIdCache = new();
        // Real location names discovered via DiscoverUniqueDevicesAsync, keyed by provider location id.
        private readonly Dictionary<string, string> _knownLocationNames = new();
        private string? _cachedLocationName;
        private readonly Dictionary<string, Guid> _deviceIdCache = new();
        // Device ID to location name mapping, set by the caller before downloading
        private Dictionary<string, string>? _deviceToLocationMapping;
        private int _lastRemainingCount;
        private string? _lastRemainingReason;
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

        // Live-observable pre-scan results (per-device matched-item counts), keyed by provider device
        // id, plus the resolved effective start dates. Cached by run key so a DownloadVideosAsync call
        // that follows an explicit PreScanAsync call for the same outputPath/range/force reuses these
        // instead of re-querying every device's history again.
        private readonly System.Collections.Concurrent.ConcurrentDictionary<string, int> _preScanCounts = new();
        private Dictionary<string, DateTime> _preScanEffectiveStartDates = new();
        private int _preScanGrandTotal;
        private string? _preScanRunKey;

        // Track the account ID during downloads for account watermark updates
        private Guid? _currentBatchAccountId;

        /// <summary>Delay between device downloads to avoid rate limiting (milliseconds)</summary>
        private const int InterDeviceDelayMs = 5000;

        public VideoDownloadServiceAdapter(
            ILogger<VideoDownloadServiceAdapter> logger,
            VideoForensics.Providers.Common.Contracts.IVideoProvider videoProvider,
            VideoForensics.Providers.Common.Contracts.IProviderAuthService authService,
            VideoForensics.Providers.Common.Contracts.IMediaDownloadService downloadService,
            VideoForensics.Providers.Common.Contracts.IDeviceDiscoveryService deviceService,
            IVideoForensicsDataClient dataClient,
            IForensicsConfiguration forensicsConfig)
        {
            _logger = logger;
            _videoProvider = videoProvider;
            _authService = authService;
            _downloadService = downloadService;
            _deviceService = deviceService;
            _dataClient = dataClient;
            _forensicsConfig = forensicsConfig;
        }

        /// <summary>Sets the device-to-location name mapping. Call this before DownloadVideosAsync to use real location names.</summary>
        public void SetDeviceLocationMapping(Dictionary<string, string> deviceIdToLocationName)
        {
            _deviceToLocationMapping = deviceIdToLocationName;
            _logger.LogInformation("Device location mapping set with {Count} entries", deviceIdToLocationName?.Count ?? 0);
        }

        /// <summary>Serializes an API response object to JSON and computes its SHA256 hash for audit/change tracking.</summary>
        private (string Json, string Hash) SerializeMetadata(object? apiResponse)
        {
            if (apiResponse == null)
                return (string.Empty, string.Empty);

            using var hash = SHA256.Create();
            var json = JsonSerializer.Serialize(apiResponse);
            var hashValue = hash.ComputeHash(System.Text.Encoding.UTF8.GetBytes(json));
            var hashHex = Convert.ToHexString(hashValue);
            return (json, hashHex);
        }

        /// <summary>
        /// Resolves (find-or-create) the Guid Device.Id for a provider's string device id, ensuring its
        /// real Ring location (not a synthetic placeholder) exists first. Cached per process/device and
        /// per process/location so this is only a DB round-trip on first use for a given device/location.
        /// </summary>
        private async Task<Guid> EnsureDeviceIdentityAsync(string providerDeviceId, string deviceName, string? providerLocationId, CancellationToken ct)
        {
            if (_deviceIdCache.TryGetValue(providerDeviceId, out var cached))
                return cached;

            await _identityResolutionLock.WaitAsync(ct);
            try
            {
                if (_deviceIdCache.TryGetValue(providerDeviceId, out cached))
                    return cached;

                // Falls back to a synthetic "default" location only when the caller genuinely has no
                // real Ring location id for this device (e.g. GetLocationsAsync/DeriveLocationsFromDevices
                // both came back empty) — every device that does have one gets its own real Location row.
                var effectiveLocationId = string.IsNullOrEmpty(providerLocationId) ? "default" : providerLocationId;

                if (!_locationIdCache.TryGetValue(effectiveLocationId, out var locationGuid))
                {
                    Guid accountId;
                    if (_forensicsConfig.ActiveProviderAccountId is { } activeAccountId)
                    {
                        accountId = activeAccountId;
                    }
                    else
                    {
                        // Shouldn't normally happen - authentication is expected to have already
                        // set the active account. Falling back to a synthetic placeholder account
                        // rather than failing the whole download, but this indicates the active
                        // account wasn't set where it should have been.
                        _logger.LogWarning("No active provider account set; falling back to a synthetic placeholder account for device identity resolution");
                        var (_, account) = await _dataClient.EnsureUserAndAccountAsync(
                            _videoProvider.ProviderName, "default", "default", null, ct);
                        accountId = account.Id;
                    }

                    var locationName = _knownLocationNames.TryGetValue(effectiveLocationId, out var knownName)
                        ? knownName
                        : effectiveLocationId;

                    var locationMetadata = SerializeMetadata(new { id = effectiveLocationId, name = locationName });
                    var location = await _dataClient.EnsureLocationAsync(
                        accountId, effectiveLocationId, locationName, locationMetadata.Json, locationMetadata.Hash, ct: ct);
                    locationGuid = location.Id;
                    _locationIdCache[effectiveLocationId] = locationGuid;
                    _cachedLocationName = location.Name;
                }

                var deviceMetadata = SerializeMetadata(new { id = providerDeviceId, name = deviceName, type = "camera", isOnline = true });
                var device = await _dataClient.EnsureDeviceAsync(
                    locationGuid, providerDeviceId, deviceName, "camera", true, deviceMetadata.Json, deviceMetadata.Hash, ct: ct);
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

        /// <summary>
        /// Discovers devices across all locations, deduped by device id (a device shared across
        /// multiple locations otherwise comes back once per location).
        /// </summary>
        private async Task<List<VideoForensics.Providers.Common.Contracts.Device>> DiscoverUniqueDevicesAsync()
        {
            var locations = await _deviceService.GetLocationsAsync();
            if (locations == null || locations.Count == 0)
            {
                return new List<VideoForensics.Providers.Common.Contracts.Device>();
            }

            _logger.LogInformation("Found {LocationCount} location(s)", locations.Count);

            var seenDeviceIds = new HashSet<string>();
            var uniqueDevices = new List<VideoForensics.Providers.Common.Contracts.Device>();
            foreach (var location in locations)
            {
                _knownLocationNames[location.Id] = location.Name;
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
            return uniqueDevices;
        }

        /// <summary>
        /// Resolves every device's effective start date and matched-item count up front. Populates
        /// _preScanCounts live as each device's count is discovered (so a polling UI can show progress),
        /// and caches the result under runKey so a later call with the same key is a no-op.
        /// </summary>
        private async Task PreScanCoreAsync(
            List<VideoForensics.Providers.Common.Contracts.Device> uniqueDevices,
            DateTime startDate,
            DateTime endDate,
            bool force,
            string runKey,
            CancellationToken cancellationToken)
        {
            if (_preScanRunKey == runKey)
            {
                return; // already pre-scanned for this exact outputPath/range/force
            }

            _preScanCounts.Clear();
            var effectiveStartDates = new Dictionary<string, DateTime>();
            var grandTotalMatched = 0;

            // Resolve every device's watermark first - these are local DB lookups, not Ring API
            // calls - so the count-fetch pass below can process devices widest-range-first (earliest
            // effective start date first). GetHistoryEventsAsync's cache only avoids a re-fetch when
            // a later request's range fits inside what's already cached, so starting with the device
            // that needs the most history means its single fetch covers every other device's
            // narrower range too - one real history fetch for the whole batch instead of up to one
            // per distinct watermark.
            var devicesNeedingCount = new List<(VideoForensics.Providers.Common.Contracts.Device Device, DateTime EffectiveStart)>();
            foreach (var device in uniqueDevices)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (_completedDeviceResults.TryGetValue(device.Id, out var cachedForCount))
                {
                    grandTotalMatched += cachedForCount.FilesMatched;
                    _preScanCounts[device.Id] = cachedForCount.FilesMatched;
                    continue;
                }

                var deviceEffectiveStart = startDate;
                try
                {
                    var deviceGuid = await EnsureDeviceIdentityAsync(device.Id, device.Name, device.LocationId, cancellationToken);
                    deviceEffectiveStart = await _dataClient.GetWatermarkAsync(deviceGuid, startDate, force, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to resolve watermark for {DeviceName} during pre-scan; using requested date range", device.Name);
                }
                effectiveStartDates[device.Id] = deviceEffectiveStart;
                devicesNeedingCount.Add((device, deviceEffectiveStart));
            }

            var hasMadeRealHistoryCall = false;
            foreach (var (device, deviceEffectiveStart) in devicesNeedingCount.OrderBy(d => d.EffectiveStart))
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Processing widest-range-first (see the OrderBy above) means most devices after the
                // first are served from the already-cached superset with no API call at all - the
                // rate-limit-safety delay below only needs to apply before a call that will actually
                // hit the network, not before every device regardless.
                var willHitNetwork = !_downloadService.IsHistoryCached(deviceEffectiveStart, endDate);
                if (willHitNetwork && hasMadeRealHistoryCall)
                {
                    await Task.Delay(InterDeviceDelayMs, cancellationToken);
                }

                var deviceCount = 0;
                try
                {
                    deviceCount = await _downloadService.GetMatchedEventCountAsync(device.Id, deviceEffectiveStart, endDate, cancellationToken);
                    if (willHitNetwork)
                    {
                        hasMadeRealHistoryCall = true;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to pre-count matched events for {DeviceName}", device.Name);
                }

                grandTotalMatched += deviceCount;
                _preScanCounts[device.Id] = deviceCount;
            }

            _preScanEffectiveStartDates = effectiveStartDates;
            _preScanGrandTotal = grandTotalMatched;
            _preScanRunKey = runKey;
        }

        public async Task PreScanAsync(string outputPath, DateTime startDate, DateTime endDate, bool force = false, CancellationToken cancellationToken = default)
        {
            var runKey = $"{outputPath}|{startDate:O}|{endDate:O}";
            if (_completedRunKey != runKey)
            {
                _completedDeviceResults.Clear();
                _completedRunKey = runKey;
            }

            var uniqueDevices = await DiscoverUniqueDevicesAsync();
            await PreScanCoreAsync(uniqueDevices, startDate, endDate, force, runKey, cancellationToken);
        }

        public IReadOnlyDictionary<string, int> GetPreScanCounts()
        {
            return _preScanCounts;
        }

        public async Task<bool> DownloadVideosAsync(string outputPath, DateTime startDate, DateTime endDate, bool force = false)
        {
            _lastRemainingCount = 0;
            _lastRemainingReason = null;
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

            // Capture the account ID for watermark updates
            _currentBatchAccountId = _forensicsConfig.ActiveProviderAccountId;

            try
            {
                _logger.LogInformation("Starting video download to {OutputPath} from {StartDate} to {EndDate}", outputPath, startDate, endDate);

                _downloadService.SetMaxConcurrentDownloads(_forensicsConfig.MaxConcurrentDownloads);

                if (!await _authService.IsAuthenticatedAsync())
                {
                    _lastError = "Not authenticated. Please authenticate first.";
                    _logger.LogError(_lastError);
                    return false;
                }

                // Without this, the provider service has no way to know which account is currently
                // active and falls back to a synthetic placeholder account for every device/media
                // record it creates - silently mis-attributing real downloads regardless of which
                // Ring account is actually logged in.
                if (_currentBatchAccountId.HasValue)
                {
                    _downloadService.SetActiveProviderAccountId(_currentBatchAccountId.Value);
                }

                // Get available devices
                var uniqueDevices = await DiscoverUniqueDevicesAsync();

                // Download videos from all devices
                var totalDevices = uniqueDevices.Count;
                var totalFilesDownloaded = 0;
                var totalFilesMatched = 0;
                var deviceErrors = new List<string>();
                // Reasons for a per-device shortfall (FilesDownloaded < FilesMatched) even when that
                // device's call reported Success — e.g. "rate limited by Ring API". Collected
                // regardless of whether any files downloaded overall, so the "N remaining" prompt can
                // state the real cause instead of assuming rate limiting.
                var skipReasons = new List<string>();

                _currentDeviceTotal = uniqueDevices.Count;

                // Resolve every device's effective start date up front (not just the one about to be
                // processed), and pre-count its matched events. Without this, "Across All Devices" only
                // ever knew about the device currently in flight — the aggregate total grew device-by-
                // device instead of reflecting the true grand total from before device 1 even starts.
                // A no-op if PreScanAsync already did this for the same run (see runKey above).
                await PreScanCoreAsync(uniqueDevices, startDate, endDate, force, runKey, CancellationToken.None);
                var effectiveStartDates = _preScanEffectiveStartDates;
                _totalFilesMatched = _preScanGrandTotal;

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
                        _totalBytesDownloaded += cached.BytesDownloaded;
                        _logger.LogInformation("⏭ Skipping {DeviceName} — already fully downloaded for this range", device.Name);
                        continue;
                    }

                    _logger.LogInformation("Downloading from device {DeviceNumber}/{TotalDevices}: {DeviceName} ({DeviceId})",
                        _currentDeviceIndex, uniqueDevices.Count, device.Name, device.Id);

                    // Effective start date was already resolved during the pre-scan above.
                    var effectiveStartDate = effectiveStartDates.TryGetValue(device.Id, out var resolvedStart) ? resolvedStart : startDate;
                    if (effectiveStartDate != startDate)
                    {
                        _logger.LogInformation("Using watermark-narrowed start date {StartDate} for {DeviceName} (requested {RequestedStartDate})",
                            effectiveStartDate, device.Name, startDate);
                    }

                    // Build device-specific path with location and camera name structure
                    var locationName = "Unknown";
                    if (_deviceToLocationMapping?.TryGetValue(device.Id, out var mappedLocation) == true)
                    {
                        locationName = mappedLocation;
                    }
                    else if (!string.IsNullOrEmpty(_cachedLocationName))
                    {
                        locationName = _cachedLocationName;
                    }

                    var deviceOutputPath = PathUtilities.BuildSavePath(outputPath, locationName, device.Name);

                    var result = await _downloadService.DownloadVideosAsync(
                        device.Id,
                        deviceOutputPath,
                        effectiveStartDate,
                        endDate,
                        device.LocationId
                    );

                    if (result?.Success == true)
                    {
                        totalFilesDownloaded += result.FilesDownloaded;
                        totalFilesMatched += result.FilesMatched;
                        _totalFilesCompleted = totalFilesDownloaded;
                        _totalBytesDownloaded += result.BytesDownloaded;
                        _logger.LogInformation("✓ Downloaded {FileCount} video(s) from {DeviceName}",
                            result.FilesDownloaded, device.Name);

                        if (result.FilesDownloaded >= result.FilesMatched)
                        {
                            _completedDeviceResults[device.Id] = (result.FilesDownloaded, result.FilesMatched, result.BytesDownloaded);
                        }
                        else if (result.SkipReason != null)
                        {
                            skipReasons.Add(result.SkipReason);
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
                _lastRemainingReason = _lastRemainingCount > 0 ? BuildRemainingReason(skipReasons) : null;

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
                        // A hard-ban cooldown (see HttpUtility) carries the exact local time to retry
                        // at and persists across app restarts - surface that precise message verbatim
                        // instead of the generic "wait 5+ minutes" guess below, which is both vague
                        // and can be badly wrong (the real cooldown can run up to an hour).
                        var hardBanError = deviceErrors.FirstOrDefault(e => e.Contains("extended period", StringComparison.OrdinalIgnoreCase));
                        if (hardBanError != null)
                        {
                            var colonIndex = hardBanError.IndexOf(':');
                            _lastError = colonIndex >= 0 ? hardBanError[(colonIndex + 1)..].Trim() : hardBanError;
                            _logger.LogError(_lastError);
                            return false;
                        }

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

                // Update account-level watermark to enable incremental downloads on next run
                if (_currentBatchAccountId.HasValue && totalFilesDownloaded > 0)
                {
                    try
                    {
                        await _dataClient.UpdateAccountDownloadWatermarkAsync(_currentBatchAccountId.Value, endDate, CancellationToken.None);
                        _logger.LogInformation("Updated account download watermark to {Timestamp}", endDate);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to update account download watermark; next download may re-process some events");
                    }
                }

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
            _lastRemainingReason = null;
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

                // See DownloadVideosAsync's identical call for why this matters.
                if (_forensicsConfig.ActiveProviderAccountId is { } activeAccountId)
                {
                    _downloadService.SetActiveProviderAccountId(activeAccountId);
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

                    // Build device-specific path with location and camera name structure
                    var locationName = "Unknown";
                    if (_deviceToLocationMapping?.TryGetValue(device.Id, out var mappedLocation) == true)
                    {
                        locationName = mappedLocation;
                    }
                    else if (!string.IsNullOrEmpty(_cachedLocationName))
                    {
                        locationName = _cachedLocationName;
                    }

                    var deviceOutputPath = PathUtilities.BuildSavePath(outputPath, locationName, device.Name);

                    var result = await _downloadService.DownloadSnapshotsAsync(
                        device.Id,
                        deviceOutputPath,
                        startDate,
                        endDate,
                        device.LocationId
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

        /// <summary>Why GetRemainingCount() is nonzero (e.g. "rate limited by Ring API"), or null if unknown/not applicable.</summary>
        public string? GetRemainingReason()
        {
            return _lastRemainingReason;
        }

        /// <summary>
        /// Summarizes per-device skip reasons into one human-readable explanation for why some
        /// matched items weren't downloaded. Returns null (unknown cause) rather than guessing when
        /// no reasons were collected - e.g. a provider that hasn't been updated to populate
        /// DownloadResult.SkipReason.
        /// </summary>
        private static readonly System.Text.RegularExpressions.Regex ItemFailureCountPattern =
            new(@"^(\d+) items? failed to download$", System.Text.RegularExpressions.RegexOptions.Compiled);

        private static string? BuildRemainingReason(List<string> skipReasons)
        {
            if (skipReasons.Count == 0)
            {
                return null;
            }

            // Each device reports its own "N item(s) failed to download" count, which .Distinct()
            // treats as unrelated reasons when the counts differ (e.g. "9 items failed to download"
            // vs "1 item failed to download" render as two separate "causes" instead of one combined
            // total) - sum those into a single reason before deduplicating what's left.
            var itemFailureTotal = 0;
            var otherReasons = new List<string>();
            foreach (var reason in skipReasons)
            {
                var match = ItemFailureCountPattern.Match(reason);
                if (match.Success)
                {
                    itemFailureTotal += int.Parse(match.Groups[1].Value);
                }
                else
                {
                    otherReasons.Add(reason);
                }
            }

            var reasons = otherReasons.Distinct().ToList();
            if (itemFailureTotal > 0)
            {
                reasons.Add(itemFailureTotal == 1 ? "1 item failed to download" : $"{itemFailureTotal} items failed to download");
            }

            if (reasons.Count == 1)
            {
                return char.ToUpperInvariant(reasons[0][0]) + reasons[0].Substring(1);
            }

            return "Multiple causes: " + string.Join(", ", reasons);
        }

        public (int Index, int Total, string Name) GetCurrentDevice()
        {
            return (_currentDeviceIndex, _currentDeviceTotal, _currentDeviceName);
        }

        public string? GetLastError()
        {
            return _lastError;
        }

        public DateTime? GetRateLimitBanUntilUtc()
        {
            return _downloadService.GetRateLimitBanUntilUtc();
        }

        public void OverrideRateLimitBan()
        {
            _downloadService.OverrideRateLimitBan();
        }
    }
}
