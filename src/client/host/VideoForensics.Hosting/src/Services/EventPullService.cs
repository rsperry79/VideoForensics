using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Core.Helpers.Platform;
using VideoForensics.Core.Providers.Common.Contracts;
using Location = VideoForensics.Core.Providers.Common.Contracts.Location;
using Device = VideoForensics.Core.Providers.Common.Contracts.Device;
using LocationEntity = VideoForensics.Data.Common.Entities.Location;
using DeviceEntity = VideoForensics.Data.Common.Entities.Device;

namespace VideoForensics.Hosting.Services
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
        private readonly IDeviceDiscoveryService _deviceDiscovery;
        private readonly IMediaDownloadService? _mediaDownloadService;
        private readonly string _mediaRoot;
        private readonly IDeviceConfigRepository? _deviceConfigRepository;

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
            IDeviceDiscoveryService deviceDiscovery,
            ILogger<EventPullService>? logger = null,
            IMediaDownloadService? mediaDownloadService = null,
            string? mediaRoot = null,
            IDeviceConfigRepository? deviceConfigRepository = null)
        {
            _deviceConfigRepository = deviceConfigRepository;
            _eventAndConfigService = eventAndConfigService;
            _deviceRepository = deviceRepository;
            _locationRepository = locationRepository;
            _providerAccountRepository = providerAccountRepository;
            _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<EventPullService>.Instance;
            _deviceDiscovery = deviceDiscovery;
            _mediaDownloadService = mediaDownloadService;
            _mediaRoot = mediaRoot ?? new StorageLocationProvider().GetDefaultRoot(StorageCategory.Media);
        }

        public async Task<EventPullResult> PullAccountEventsAsync(Guid accountId, DateTime? fromTimestampUtc, DateTime? toTimestampUtc, CancellationToken cancellationToken)
        {
            // Counts live outside the try so a failure partway through still reports what was reached.
            int locationsSynced = 0, devicesSynced = 0, eventsSynced = 0, configAreasSynced = 0, feedsFailed = 0;
            try
            {
                var account = await _providerAccountRepository.GetAsync(accountId, cancellationToken);
                if (account == null)
                {
                    _logger.LogWarning("Provider account not found");
                    return default;
                }

                Inventory inventory = await DiscoverInventoryAsync(accountId, cancellationToken);
                locationsSynced = inventory.LocationCount;
                devicesSynced = inventory.Devices.Count;

                if (inventory.LocationCount == 0)
                {
                    _logger.LogDebug("No locations found for account");
                    account.LastSuccessfulAuthUtc = DateTime.UtcNow;
                    account.LastErrorMessage = null;
                    await _providerAccountRepository.UpdateAsync(account, cancellationToken);
                    return new EventPullResult(devicesSynced, locationsSynced);
                }

                _logger.LogInformation("Syncing events for {DeviceCount} device(s)", inventory.Devices.Count);

                var startTime = fromTimestampUtc ?? DateTime.MinValue;
                // A null end date means "now". Unspecified kinds are treated as UTC, the contract for both bounds.
                var endTime = toTimestampUtc is { } to
                    ? (to.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(to, DateTimeKind.Utc) : to.ToUniversalTime())
                    : DateTime.UtcNow;
                var deviceErrors = new List<string>();

                string accountFolder = string.Empty;
                if (_mediaDownloadService == null)
                {
                    _logger.LogWarning("No media download service is registered; media was not pulled");
                }
                else
                {
                    accountFolder = Path.Combine(_mediaRoot, accountId.ToString("N"));
                    Directory.CreateDirectory(accountFolder);
                    _mediaDownloadService.SetActiveProviderAccountId(accountId);
                }

                int deviceIndex = 0;
                foreach (InventoryDevice inventoryDevice in inventory.Devices)
                {
                    DeviceEntity? device = inventoryDevice.Device;
                    if (device == null) continue;
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
                            // A failed media pull throws so the per-device catch records it and counts the feed as failed.
                            DownloadResult media = await _mediaDownloadService.DownloadVideosAsync(
                                device.ProviderDeviceId, accountFolder, startTime, endTime, providerLocationId, cancellationToken);
                            if (!media.Success)
                            {
                                throw new InvalidOperationException(media.ErrorMessage ?? "Media download failed");
                            }
                        }

                        DeviceConfig? config = await _eventAndConfigService.GetDeviceConfigAsync(accountId, device.ProviderDeviceId, cancellationToken);
                        if (config != null)
                        {
                            configAreasSynced++;
                            // Config history is secondary to the events and media just synced. A failure here is logged and
                            // does not count the whole device pull as failed.
                            try
                            {
                                await PersistDeviceConfigIfChangedAsync(device.Id, config, cancellationToken);
                            }
                            catch (Exception ex) when (ex is not OperationCanceledException)
                            {
                                _logger.LogWarning(ex, "Could not record device configuration history; events were synced");
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
                        _logger.LogError(ex, "Failed to pull events for device");
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

                _logger.LogInformation("Successfully pulled events for account");
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

                var account = await _providerAccountRepository.GetAsync(accountId, cancellationToken);
                if (account != null)
                {
                    account.LastErrorMessage = TruncateErrorMessage(ex.Message);
                    account.LastErrorUtc = DateTime.UtcNow;
                    await _providerAccountRepository.UpdateAsync(account, cancellationToken);
                }

                return new EventPullResult(devicesSynced, locationsSynced, eventsSynced, configAreasSynced, feedsFailed);
            }
        }

        /// <summary>The locations and devices a pull works from. <see cref="LocationCount"/> zero means nothing to pull.</summary>
        private sealed record Inventory(int LocationCount, List<InventoryDevice> Devices);

        /// <summary>A stored device paired with its provider location id, which media downloads need for attribution.</summary>
        private sealed record InventoryDevice(DeviceEntity Device, string ProviderLocationId);

        /// <summary>
        /// Asks the provider for the account's locations and devices and saves any that are new or changed. Errors propagate so the
        /// caller records them on the account rather than pulling from a partial inventory.
        /// </summary>
        private async Task<Inventory> DiscoverInventoryAsync(Guid accountId, CancellationToken cancellationToken)
        {
            IReadOnlyList<Location> remoteLocations = await _deviceDiscovery.GetLocationsAsync(accountId, cancellationToken);
            var devices = new List<InventoryDevice>();

            foreach (Location remoteLocation in remoteLocations)
            {
                LocationEntity location = await UpsertLocationAsync(accountId, remoteLocation, cancellationToken);
                IReadOnlyList<Device> remoteDevices = await _deviceDiscovery.GetDevicesAsync(accountId, remoteLocation.Id, cancellationToken);
                foreach (Device remoteDevice in remoteDevices)
                {
                    devices.Add(new InventoryDevice(await UpsertDeviceAsync(location.Id, remoteDevice, cancellationToken), remoteLocation.Id));
                }
            }

            _logger.LogDebug("Discovered {LocationCount} location(s) and {DeviceCount} device(s) for account", remoteLocations.Count, devices.Count);
            return new Inventory(remoteLocations.Count, devices);
        }

        private async Task<LocationEntity> UpsertLocationAsync(Guid accountId, Location remote, CancellationToken cancellationToken)
        {
            LocationEntity? existing = await _locationRepository.GetByProviderLocationIdAsync(accountId, remote.Id, cancellationToken);
            if (existing == null)
            {
                existing = new LocationEntity
                {
                    Id = Guid.NewGuid(),
                    ProviderLocationId = remote.Id,
                    Name = remote.Name,
                    Address = remote.Address,
                    LastSyncedUtc = DateTime.UtcNow,
                    SyncStatus = SyncStatus.Synced
                };
                await _locationRepository.AddAsync(existing, cancellationToken);
            }

            // Locations are shared across accounts; linking is idempotent.
            await _locationRepository.LinkToProviderAccountAsync(existing.Id, accountId, cancellationToken);
            return existing;
        }

        private async Task<DeviceEntity> UpsertDeviceAsync(Guid locationId, Device remote, CancellationToken cancellationToken)
        {
            DeviceEntity? existing = await _deviceRepository.GetByProviderDeviceIdAsync(locationId, remote.Id, cancellationToken);
            if (existing == null)
            {
                existing = new DeviceEntity
                {
                    Id = Guid.NewGuid(),
                    LocationId = locationId,
                    ProviderDeviceId = remote.Id,
                    Name = remote.Name,
                    Type = remote.Type,
                    IsOnline = remote.IsOnline,
                    LastSyncedUtc = DateTime.UtcNow,
                    SyncStatus = SyncStatus.Synced
                };
                await _deviceRepository.AddAsync(existing, cancellationToken);
            }
            else
            {
                existing.Name = remote.Name;
                existing.Type = remote.Type;
                existing.IsOnline = remote.IsOnline;
                existing.LastSyncedUtc = DateTime.UtcNow;
                await _deviceRepository.UpdateAsync(existing, cancellationToken);
            }

            return existing;
        }

        /// <summary>
        /// Appends a configuration snapshot only when the fetched values differ from the latest stored one, so history
        /// records changes rather than one row per sync.
        /// </summary>
        private async Task PersistDeviceConfigIfChangedAsync(Guid deviceId, DeviceConfig config, CancellationToken cancellationToken)
        {
            if (_deviceConfigRepository is null)
            {
                return;
            }

            var sensitivity = config.MotionSensitivity.ToString(CultureInfo.InvariantCulture);
            var customSettingsJson = CanonicalJson(config.CustomSettings);

            DeviceConfigSnapshot? latest = await _deviceConfigRepository.GetLatestAsync(deviceId, cancellationToken);
            if (latest is not null &&
                latest.MotionDetectionEnabled == config.MotionDetectionEnabled &&
                latest.MotionSensitivity == sensitivity &&
                latest.RecordingMode == config.RecordingMode &&
                latest.CustomSettingsJson == customSettingsJson)
            {
                return;
            }

            await _deviceConfigRepository.AppendSnapshotAsync(new DeviceConfigSnapshot
            {
                Id = Guid.NewGuid(),
                DeviceId = deviceId,
                MotionDetectionEnabled = config.MotionDetectionEnabled,
                MotionSensitivity = sensitivity,
                RecordingMode = config.RecordingMode,
                CustomSettingsJson = customSettingsJson,
                CapturedAtUtc = DateTime.UtcNow,
                Source = DeviceConfigSource.Fetched
            }, cancellationToken);
        }

        /// <summary>
        /// Serializes settings with object keys sorted at every level. Dictionary enumeration order is not a stable
        /// contract, so an unsorted serialization would make the same settings look changed on every sync and append a
        /// duplicate snapshot each time.
        /// </summary>
        internal static string? CanonicalJson(object? value)
        {
            if (value is null)
            {
                return null;
            }

            using JsonDocument document = JsonSerializer.SerializeToDocument(value);
            using MemoryStream stream = new();
            using (Utf8JsonWriter writer = new(stream))
            {
                WriteSorted(document.RootElement, writer);
            }

            return System.Text.Encoding.UTF8.GetString(stream.ToArray());
        }

        private static void WriteSorted(JsonElement element, Utf8JsonWriter writer)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    writer.WriteStartObject();
                    foreach (JsonProperty property in element.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal))
                    {
                        writer.WritePropertyName(property.Name);
                        WriteSorted(property.Value, writer);
                    }

                    writer.WriteEndObject();
                    break;
                case JsonValueKind.Array:
                    writer.WriteStartArray();
                    foreach (JsonElement item in element.EnumerateArray())
                    {
                        WriteSorted(item, writer);
                    }

                    writer.WriteEndArray();
                    break;
                default:
                    element.WriteTo(writer);
                    break;
            }
        }

        private static string TruncateErrorMessage(string message, int maxLength = 2000)
        {
            if (message == null) return null;
            return message.Length > maxLength ? message.Substring(0, maxLength) : message;
        }
    }
}
