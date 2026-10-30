using Microsoft.Extensions.Logging;

using System.Globalization;
using System.Text.Json;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Client.Hosting.Contracts;
using VideoForensics.Core.Helpers.Platform;
using VideoForensics.Core.Providers.Common.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Core.Contracts;
using VideoForensics.Data.Core.Services;

using Device = VideoForensics.Core.Providers.Common.Contracts.Device;
using DeviceEntity = VideoForensics.Data.Common.Entities.Device;
using Location = VideoForensics.Core.Providers.Common.Contracts.Location;
using LocationEntity = VideoForensics.Data.Common.Entities.Location;

namespace VideoForensics.Client.Hosting.Services
{
    /// <summary>Service for pulling events and configuration from provider accounts automatically.</summary>
    public interface IEventPullService
    {
        /// <summary>Pulls events and configuration for all devices in a provider account.</summary>
        /// <param name="accountId">The provider account ID.</param>
        /// <param name="fromTimestampUtc">Start timestamp for event retrieval. If null, defaults to DateTime.MinValue for first-time pulls.</param>
        /// <param name="toTimestampUtc">End of the window (UTC). If null, the pull runs up to now.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>What the pull synced. Failures are recorded on the account, not thrown; the counts cover what was reached.</returns>
        Task<EventPullResult> PullAccountEventsAsync(Guid accountId, DateTime? fromTimestampUtc, DateTime? toTimestampUtc, CancellationToken cancellationToken);
    }

    /// <summary>What one event pull synced, recorded on the sync run.</summary>
    /// <param name="DevicesSynced">Devices discovered and stored.</param>
    /// <param name="LocationsSynced">Locations discovered and stored.</param>
    /// <param name="EventsSynced">Events returned by the provider across all device feeds.</param>
    /// <param name="ConfigAreasSynced">Devices whose configuration was returned by the provider.</param>
    /// <param name="FeedsFailed">Per-device event feeds that failed.</param>
    public readonly record struct EventPullResult(
        int DevicesSynced = 0,
        int LocationsSynced = 0,
        int EventsSynced = 0,
        int ConfigAreasSynced = 0,
        int FeedsFailed = 0);

    /// <summary>Pulls events and configuration from provider accounts automatically.</summary>
    public class EventPullService : IEventPullService
    {
        private readonly IEventAndConfigService _eventAndConfigService;
        private readonly IDeviceRepository _deviceRepository;
        private readonly ILocationRepository _locationRepository;
        private readonly IProviderAccountRepository _providerAccountRepository;
        private readonly ILogger<EventPullService> _logger;
        private readonly IReferenceDataCache _referenceCache;
        private readonly IMediaDownloadService? _mediaDownloadService;
        private readonly string? _mediaRootOverride;
        private readonly string _mediaRoot;
        private readonly IStorageLocationProvider? _storage;
        private readonly IForensicsConfigurationService? _configurationService;
        private readonly IMediaPreviewService? _previewService;
        private readonly IMediaItemRepository? _mediaItems;
        private readonly ISyncProgressStore? _progress;
        private readonly IMediaPathResolver _mediaPaths;
        private readonly TimeSpan _progressPollInterval;

        // Process-wide, not per pull: parallel account pulls share these two encode slots, so ffmpeg never runs more than two at once.
        private static readonly SemaphoreSlim PreviewEncodeGate = new(2);

        /// <summary>
        /// Creates a new instance of the EventPullService. Each pull first discovers the account's locations and devices from
        /// the provider and persists them, then pulls events for every discovered device. Connecting an account stores no
        /// inventory, so discovery is what makes a first sync work.
        /// </summary>
        public EventPullService(
            IEventAndConfigService eventAndConfigService,
            IDeviceRepository deviceRepository,
            ILocationRepository locationRepository,
            IProviderAccountRepository providerAccountRepository,
            ILogger<EventPullService>? logger = null,
            IMediaDownloadService? mediaDownloadService = null,
            string? mediaRoot = null,
            IMediaPreviewService? previewService = null,
            IMediaItemRepository? mediaItems = null,
            IStorageLocationProvider? storage = null,
            IForensicsConfigurationService? configurationService = null,
            ISyncProgressStore? progress = null,
            TimeSpan? progressPollInterval = null,
            IMediaPathResolver? mediaPaths = null,
            IReferenceDataCache? referenceCache = null)
        {
            _progress = progress;
            _mediaPaths = mediaPaths ?? PassThroughMediaPathResolver.Instance;
            _progressPollInterval = progressPollInterval ?? TimeSpan.FromMilliseconds(500);
            _referenceCache = referenceCache ?? new ReferenceDataCache();
            _previewService = previewService;
            _mediaItems = mediaItems;
            _eventAndConfigService = eventAndConfigService;
            _deviceRepository = deviceRepository;
            _locationRepository = locationRepository;
            _providerAccountRepository = providerAccountRepository;
            _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<EventPullService>.Instance;
            _mediaDownloadService = mediaDownloadService;
            _mediaRootOverride = mediaRoot;
            _storage = storage;
            _configurationService = configurationService;
            _mediaRoot = mediaRoot ?? new StorageLocationProvider().GetDefaultRoot(StorageCategory.Media);
        }

        /// <summary>
        /// Resolves the Media root at pull time, so a change on the Storage Settings page applies to the next pull without a restart.
        /// An explicit override wins. Without storage and configuration services the constructor default is used.
        /// </summary>
        private async Task<string> ResolveMediaRootAsync(CancellationToken cancellationToken)
        {
            if (_mediaRootOverride is not null || _storage is null || _configurationService is null)
            {
                return _mediaRoot;
            }

            IForensicsConfiguration config = await _configurationService.LoadConfigurationAsync(string.Empty, cancellationToken);
            return _storage.GetEffectiveRoot(StorageCategory.Media, config.DownloadLocation);
        }

        public async Task<EventPullResult> PullAccountEventsAsync(Guid accountId, DateTime? fromTimestampUtc, DateTime? toTimestampUtc, CancellationToken cancellationToken)
        {
            // Counts live outside the try so a failure partway through still reports what was reached.
            int locationsSynced = 0, devicesSynced = 0, eventsSynced = 0, configAreasSynced = 0, feedsFailed = 0;
            try
            {
                ProviderAccount? account = await _providerAccountRepository.GetAsync(accountId, cancellationToken);
                if (account == null)
                {
                    _logger.LogWarning("Provider account not found");
                    return default;
                }

                // Why: a pull never asks the provider for inventory. Stored locations and devices come from the reference cache,
                // which the scheduler or a manual sync fills; with none stored, a sync has to run before events can be linked.
                Inventory inventory = await LoadInventoryAsync(accountId, cancellationToken);
                locationsSynced = inventory.LocationCount;
                devicesSynced = inventory.Devices.Count;

                if (inventory.LocationCount == 0)
                {
                    _logger.LogWarning("No stored locations for account; run a device sync before pulling events");
                    return default;
                }

                _logger.LogInformation("Syncing events for {DeviceCount} device(s)", inventory.Devices.Count);

                DateTime startTime = fromTimestampUtc ?? DateTime.MinValue;
                // A null end date means "now". Unspecified kinds are treated as UTC, the contract for both bounds.
                DateTime endTime = toTimestampUtc is { } to
                    ? (to.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(to, DateTimeKind.Utc) : to.ToUniversalTime())
                    : DateTime.UtcNow;
                var deviceErrors = new List<string>();

                if (_mediaDownloadService == null)
                {
                    _logger.LogWarning("No media download service is registered; media was not pulled");
                }
                else
                {
                    _mediaDownloadService.SetActiveProviderAccountId(accountId);
                }

                // Read once per pull so every device in this pull lands under the same root.
                string mediaRoot = await ResolveMediaRootAsync(cancellationToken);
                int deviceIndex = 0;
                foreach (InventoryDevice inventoryDevice in inventory.Devices)
                {
                    DeviceEntity? device = inventoryDevice.Device;
                    if (device == null)
                    {
                        continue;
                    }

                    string providerLocationId = inventoryDevice.ProviderLocationId;
                    string deviceName = device.Name ?? device.ProviderDeviceId ?? "Unknown Device";
                    deviceIndex++;

                    try
                    {
                        _logger.LogInformation("Syncing events for device {DeviceName} ({Index}/{DeviceCount})", deviceName, deviceIndex, inventory.Devices.Count);

                        IReadOnlyList<DeviceEvent> events = await _eventAndConfigService.GetEventsAsync(
                            accountId,
                            device.ProviderDeviceId,
                            startTime,
                            endTime,
                            eventType: null,
                            cancellationToken
                        );

                        // The provider is asked for the window, but not every provider trims strictly; count only what is inside it.
                        int deviceEventCount = events?.Count(e => e.Timestamp >= startTime && e.Timestamp <= endTime) ?? 0;
                        eventsSynced += deviceEventCount;

                        if (_mediaDownloadService != null)
                        {
                            // Media lands in <root>\<provider>\<location>\<device>, the layout the Storage Settings page
                            // assumes. The location name comes from the stored inventory, not the provider.
                            string locationName = inventoryDevice.LocationName ?? providerLocationId;
                            string deviceFolder = MediaFolderLayout.GetDeviceFolder(mediaRoot, account.ProviderName, locationName, deviceName);
                            _ = Directory.CreateDirectory(deviceFolder);

                            // A failed media pull throws so the per-device catch records it and counts the feed as failed.
                            DownloadResult media = await DownloadWithProgressAsync(
                                _mediaDownloadService, device.ProviderDeviceId, deviceFolder, startTime, endTime, providerLocationId,
                                deviceName, deviceIndex, inventory.Devices.Count, cancellationToken);
                            if (!media.Success)
                            {
                                throw new InvalidOperationException(media.ErrorMessage ?? "Media download failed");
                            }

                            if (_previewService != null && _mediaItems != null)
                            {
                                // Previews are a convenience. Their failure is logged here and never counts the device as failed.
                                try
                                {
                                    await GeneratePreviewsAsync(device.Id, deviceName, startTime, endTime, cancellationToken);
                                }
                                catch (Exception ex) when (ex is not OperationCanceledException)
                                {
                                    _logger.LogError(ex, "Preview generation failed; media was synced");
                                }
                            }
                        }

                        device.LastSuccessfulPullAtUtc = DateTime.UtcNow;
                        device.LastPullAttemptAtUtc = DateTime.UtcNow;
                        await _deviceRepository.UpdateAsync(device, cancellationToken);

                        _logger.LogInformation("Successfully pulled events for device");
                        _logger.LogInformation("Synced device {DeviceName}: {EventCount} event(s)", deviceName, deviceEventCount);
                    }
                    catch (Exception ex)
                    {
                        // A throttle is handled and expected: name the device and the reason, never the stack.
                        if (ex is VideoForensics.Providers.Ring.Exceptions.ThrottledException throttled)
                        {
                            _logger.LogWarning("Skipped device {DeviceName}: {Reason}", deviceName, throttled.Message);
                        }
                        else
                        {
                            _logger.LogError(ex, "Failed to pull events for device {DeviceName}", deviceName);
                        }

                        device.LastPullAttemptAtUtc = DateTime.UtcNow;
                        await _deviceRepository.UpdateAsync(device, cancellationToken);
                        deviceErrors.Add($"Device {deviceName}: {ex.Message}");
                        feedsFailed++;
                    }
                }

                if (deviceErrors.Count > 0)
                {
                    account.LastErrorMessage = TruncateErrorMessage(string.Join("; ", deviceErrors));
                    account.LastErrorUtc = DateTime.UtcNow;
                }
                else
                {
                    account.LastSuccessfulAuthUtc = DateTime.UtcNow;
                    account.LastErrorMessage = null;
                    account.LastErrorUtc = null;
                }

                await _providerAccountRepository.UpdateAsync(account, cancellationToken);

                // Only report success when every device pulled; a partial pull must not read as a clean one in the log.
                if (feedsFailed > 0)
                {
                    _logger.LogWarning("Pulled events for account with {FailedCount} of {DeviceCount} device(s) failed", feedsFailed, inventory.Devices.Count);
                }
                else
                {
                    _logger.LogInformation("Successfully pulled events for account");
                }

                return new EventPullResult(devicesSynced, locationsSynced, eventsSynced, configAreasSynced, feedsFailed);
            }
            catch (Exception ex)
            {
                // A throttle is handled and expected: log its message, not the stack.
                if (ex is VideoForensics.Providers.Ring.Exceptions.ThrottledException)
                {
                    _logger.LogWarning("Event pull skipped for account: {Reason}", ex.Message);
                }
                else
                {
                    _logger.LogError(ex, "Failed to pull events for account");
                }

                ProviderAccount? account = await _providerAccountRepository.GetAsync(accountId, cancellationToken);
                if (account != null)
                {
                    account.LastErrorMessage = TruncateErrorMessage(ex.Message);
                    account.LastErrorUtc = DateTime.UtcNow;
                    await _providerAccountRepository.UpdateAsync(account, cancellationToken);
                }

                return new EventPullResult(devicesSynced, locationsSynced, eventsSynced, configAreasSynced, feedsFailed);
            }
            finally
            {
                // The pull is over, success or failure: the progress panel returns to idle.
                _progress?.Clear();
            }
        }

        /// <summary>
        /// Runs one device's media download while a lightweight poller publishes the download service's status to the
        /// progress store. The download service is scoped to this pull, so only this loop can read its live status.
        /// Without a store nothing is polled.
        /// </summary>
        private async Task<DownloadResult> DownloadWithProgressAsync(
            IMediaDownloadService media,
            string providerDeviceId,
            string deviceFolder,
            DateTime startTime,
            DateTime endTime,
            string providerLocationId,
            string deviceName,
            int deviceIndex,
            int deviceTotal,
            CancellationToken cancellationToken)
        {
            if (_progress is null)
            {
                return await media.DownloadVideosAsync(providerDeviceId, deviceFolder, startTime, endTime, providerLocationId, cancellationToken);
            }

            using var pollCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            Task poller = PollProgressAsync(media, deviceName, deviceIndex, deviceTotal, pollCancellation.Token);
            try
            {
                return await media.DownloadVideosAsync(providerDeviceId, deviceFolder, startTime, endTime, providerLocationId, cancellationToken);
            }
            finally
            {
                await pollCancellation.CancelAsync();
                await poller;
                // One last reading so the store reflects the end state of this device before the next one starts.
                PublishProgress(media, deviceName, deviceIndex, deviceTotal);
            }
        }

        /// <summary>Publishes a reading every poll interval until cancelled. A failed reading is logged and skipped, never thrown.</summary>
        private async Task PollProgressAsync(IMediaDownloadService media, string deviceName, int deviceIndex, int deviceTotal, CancellationToken cancellationToken)
        {
            while (true)
            {
                PublishProgress(media, deviceName, deviceIndex, deviceTotal);
                try
                {
                    await Task.Delay(_progressPollInterval, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }

        /// <summary>
        /// Reads the download service's status and stores it with the device position. A status failure must never fail the
        /// pull, so it is logged with the exception type and message only.
        /// </summary>
        private void PublishProgress(IMediaDownloadService media, string deviceName, int deviceIndex, int deviceTotal)
        {
            try
            {
                DownloadStatus status = media.GetStatus();
                _progress!.Set(new SyncProgress(
                    status.IsDownloading,
                    status.FilesTotal,
                    status.FilesCompleted,
                    status.BytesDownloaded,
                    status.CurrentFile,
                    status.ActiveConnections,
                    deviceIndex,
                    deviceTotal,
                    deviceName));
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Sync progress poll failed: {ErrorType}: {ErrorMessage}", ex.GetType().Name, ex.Message);
            }
        }

        /// <summary>The locations and devices a pull works from. <see cref="LocationCount"/> zero means nothing to pull.</summary>
        private sealed record Inventory(int LocationCount, List<InventoryDevice> Devices);

        /// <summary>A stored device paired with its provider location id and name, which media downloads need for attribution and folders.</summary>
        private sealed record InventoryDevice(DeviceEntity Device, string ProviderLocationId, string? LocationName);

        /// <summary>
        /// Reads the account's stored inventory from the reference cache, loading it from the database on a miss. Only the
        /// device rows are read per pull, because a pull updates their timestamps.
        /// </summary>
        private async Task<Inventory> LoadInventoryAsync(Guid accountId, CancellationToken cancellationToken)
        {
            AccountReferenceData data = _referenceCache.Get(accountId) ?? await LoadReferenceDataFromDbAsync(accountId, cancellationToken);
            var locationNames = data.Locations.ToDictionary(l => l.Id, l => l.Name);

            var devices = new List<InventoryDevice>();
            foreach (CachedDevice cached in data.DevicesByProviderId.Values)
            {
                DeviceEntity? device = await _deviceRepository.GetAsync(cached.Id, cancellationToken);
                if (device is not null)
                {
                    _ = locationNames.TryGetValue(cached.LocationId, out string? locationName);
                    devices.Add(new InventoryDevice(device, cached.ProviderLocationId, locationName));
                }
            }

            return new Inventory(data.Locations.Count, devices);
        }

        /// <summary>Builds the account's reference snapshot from the database and caches it, for a pull that finds the cache empty.</summary>
        private async Task<AccountReferenceData> LoadReferenceDataFromDbAsync(Guid accountId, CancellationToken cancellationToken)
        {
            IReadOnlyList<LocationEntity> locations = await _locationRepository.ListByProviderAccountIdAsync(accountId, cancellationToken);
            var cachedLocations = new List<CachedLocation>();
            var devices = new Dictionary<string, CachedDevice>(StringComparer.Ordinal);

            foreach (LocationEntity location in locations)
            {
                cachedLocations.Add(new CachedLocation(location.Id, location.ProviderLocationId, location.Name));
                IReadOnlyList<DeviceEntity> locationDevices = await _deviceRepository.GetByLocationIdAsync(location.Id, cancellationToken);
                foreach (DeviceEntity device in locationDevices)
                {
                    devices[device.ProviderDeviceId] = new CachedDevice(device.Id, location.Id, location.ProviderLocationId, device.ProviderDeviceId, device.Name);
                }
            }

            var data = new AccountReferenceData(cachedLocations, devices);
            _referenceCache.Replace(accountId, data);
            return data;
        }

        /// <summary>
        /// Generates preview sidecars for the device's clips in the pulled window. At most two encodes run at once, so a large
        /// pull does not saturate the host with ffmpeg. Per-clip failures are logged and skipped; only cancellation escapes.
        /// </summary>
        private async Task GeneratePreviewsAsync(Guid deviceId, string deviceName, DateTime startTime, DateTime endTime, CancellationToken cancellationToken)
        {
            try
            {
                IReadOnlyList<MediaItem> clips = await _mediaItems!.GetByDeviceAndDateRangeAsync(deviceId, startTime, endTime, cancellationToken);
                // The repository returns purged rows too. A clip with a preview already on disk needs no encode.
                var pending = new List<MediaItem>();
                foreach (MediaItem c in clips.Where(c => !c.IsPurged))
                {
                    if (c.PreviewPath == null || !File.Exists(await _mediaPaths.ToAbsoluteAsync(c.PreviewPath, cancellationToken)))
                    {
                        pending.Add(c);
                    }
                }

                int generated = 0, failed = 0;

                async Task ProcessAsync(MediaItem clip)
                {
                    await PreviewEncodeGate.WaitAsync(cancellationToken);
                    try
                    {
                        bool ok = await _previewService!.EnsurePreviewAsync(clip, cancellationToken);
                        if (ok)
                        {
                            _ = Interlocked.Increment(ref generated);
                        }
                        else
                        {
                            _ = Interlocked.Increment(ref failed);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        _ = Interlocked.Increment(ref failed);
                        _logger.LogError(ex, "Preview failed for clip {FileName}: {Message}", clip.FileName, ex.Message);
                    }
                    finally
                    {
                        _ = PreviewEncodeGate.Release();
                    }
                }

                await Task.WhenAll(pending.Select(ProcessAsync));
                _logger.LogInformation("Previews for device {DeviceName}: {Generated} generated, {Failed} failed", deviceName, generated, failed);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Preview generation failed for device {DeviceName}", deviceName);
            }
        }

        private static string TruncateErrorMessage(string message, int maxLength = 2000)
        {
            return message == null ? null : message.Length > maxLength ? message[..maxLength] : message;
        }
    }
}
