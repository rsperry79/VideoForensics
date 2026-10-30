using Microsoft.Extensions.Logging;

using System.Globalization;
using System.Text.Json;

using VideoForensics.Client.Hosting.Contracts;
using VideoForensics.Core.Providers.Common.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;

using Device = VideoForensics.Core.Providers.Common.Contracts.Device;
using DeviceEntity = VideoForensics.Data.Common.Entities.Device;
using Location = VideoForensics.Core.Providers.Common.Contracts.Location;
using LocationEntity = VideoForensics.Data.Common.Entities.Location;

namespace VideoForensics.Client.Hosting.Services
{
    /// <summary>What one inventory sync read from the provider and stored.</summary>
    /// <param name="LocationCount">Locations the provider returned for the account.</param>
    /// <param name="DeviceCount">Devices stored across those locations.</param>
    /// <param name="DeviceConfigsRead">Devices whose configuration the provider returned.</param>
    public readonly record struct InventorySyncResult(int LocationCount, int DeviceCount, int DeviceConfigsRead);

    /// <summary>
    /// Refreshes an account's locations, devices and device configuration from the provider. It runs only from the scheduler
    /// and from a manual sync. Event pulls never call it: they read the stored inventory from the reference cache.
    /// </summary>
    public interface IInventorySyncService
    {
        /// <summary>Asks the provider for the account's inventory, stores it, and replaces the account's cached snapshot.</summary>
        /// <exception cref="Exception">Provider discovery errors propagate so the caller can record them on the account.</exception>
        Task<InventorySyncResult> SyncAsync(Guid accountId, CancellationToken cancellationToken);
    }

    /// <summary>
    /// The only code path that calls the provider for locations, devices and device configuration. After storing, it
    /// rebuilds the account's <see cref="AccountReferenceData"/> so event pulls link to the rows just written.
    /// </summary>
    public sealed class InventorySyncService : IInventorySyncService
    {
        private readonly IDeviceDiscoveryService _deviceDiscovery;
        private readonly IEventAndConfigService _eventAndConfigService;
        private readonly ILocationRepository _locationRepository;
        private readonly IDeviceRepository _deviceRepository;
        private readonly IDeviceConfigRepository? _deviceConfigRepository;
        private readonly IReferenceDataCache _referenceCache;
        private readonly ILogger<InventorySyncService> _logger;

        /// <summary>Creates the sync over the provider services, the repositories, and the shared reference cache.</summary>
        public InventorySyncService(
            IDeviceDiscoveryService deviceDiscovery,
            IEventAndConfigService eventAndConfigService,
            ILocationRepository locationRepository,
            IDeviceRepository deviceRepository,
            IReferenceDataCache referenceCache,
            ILogger<InventorySyncService> logger,
            IDeviceConfigRepository? deviceConfigRepository = null)
        {
            _deviceDiscovery = deviceDiscovery;
            _eventAndConfigService = eventAndConfigService;
            _locationRepository = locationRepository;
            _deviceRepository = deviceRepository;
            _referenceCache = referenceCache;
            _logger = logger;
            _deviceConfigRepository = deviceConfigRepository;
        }

        /// <inheritdoc />
        public async Task<InventorySyncResult> SyncAsync(Guid accountId, CancellationToken cancellationToken)
        {
            IReadOnlyList<Location> remoteLocations = await _deviceDiscovery.GetLocationsAsync(accountId, cancellationToken);
            var locations = new List<CachedLocation>();
            var devices = new Dictionary<string, CachedDevice>(StringComparer.Ordinal);
            int configsRead = 0;

            foreach (Location remoteLocation in remoteLocations)
            {
                LocationEntity location = await UpsertLocationAsync(accountId, remoteLocation, cancellationToken);
                locations.Add(new CachedLocation(location.Id, location.ProviderLocationId, location.Name));

                IReadOnlyList<Device> remoteDevices = await _deviceDiscovery.GetDevicesAsync(accountId, remoteLocation.Id, cancellationToken);
                foreach (Device remoteDevice in remoteDevices)
                {
                    DeviceEntity device = await UpsertDeviceAsync(location.Id, remoteDevice, cancellationToken);
                    devices[device.ProviderDeviceId] = new CachedDevice(device.Id, location.Id, location.ProviderLocationId, device.ProviderDeviceId, device.Name);

                    if (await SyncDeviceConfigAsync(accountId, device, cancellationToken))
                    {
                        configsRead++;
                    }
                }
            }

            _referenceCache.Replace(accountId, new AccountReferenceData(locations, devices));
            _logger.LogInformation("Synced device inventory: {LocationCount} location(s), {DeviceCount} device(s)", locations.Count, devices.Count);
            return new InventorySyncResult(locations.Count, devices.Count, configsRead);
        }

        /// <summary>
        /// Reads one device's configuration and records a history snapshot when it changed. Returns true when the provider returned
        /// a configuration. A failure is logged and skipped: configuration history must not fail the inventory sync.
        /// </summary>
        private async Task<bool> SyncDeviceConfigAsync(Guid accountId, DeviceEntity device, CancellationToken cancellationToken)
        {
            try
            {
                DeviceConfig? config = await _eventAndConfigService.GetDeviceConfigAsync(accountId, device.ProviderDeviceId, cancellationToken);
                if (config is null)
                {
                    return false;
                }

                await PersistDeviceConfigIfChangedAsync(device.Id, config, cancellationToken);
                return true;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Could not read or record device configuration; inventory was synced");
                return false;
            }
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

            string sensitivity = config.MotionSensitivity.ToString(CultureInfo.InvariantCulture);
            string? customSettingsJson = CanonicalJson(config.CustomSettings);

            DeviceConfigSnapshot? latest = await _deviceConfigRepository.GetLatestAsync(deviceId, cancellationToken);
            if (latest is not null &&
                latest.MotionDetectionEnabled == config.MotionDetectionEnabled &&
                latest.MotionSensitivity == sensitivity &&
                latest.RecordingMode == config.RecordingMode &&
                latest.CustomSettingsJson == customSettingsJson)
            {
                return;
            }

            _ = await _deviceConfigRepository.AppendSnapshotAsync(new DeviceConfigSnapshot
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
    }
}
