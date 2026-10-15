using Microsoft.Extensions.Logging;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
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
        /// <param name="cancellationToken">Cancellation token.</param>
        Task PullAccountEventsAsync(Guid accountId, DateTime? fromTimestampUtc, CancellationToken cancellationToken);
    }

    /// <summary>Pulls events and configuration from provider accounts automatically.</summary>
    public class EventPullService : IEventPullService
    {
        private readonly IEventAndConfigService _eventAndConfigService;
        private readonly IDeviceRepository _deviceRepository;
        private readonly ILocationRepository _locationRepository;
        private readonly IProviderAccountRepository _providerAccountRepository;
        private readonly ILogger<EventPullService> _logger;
        private readonly IDeviceDiscoveryService _deviceDiscovery;

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
            ILogger<EventPullService>? logger = null)
        {
            _eventAndConfigService = eventAndConfigService;
            _deviceRepository = deviceRepository;
            _locationRepository = locationRepository;
            _providerAccountRepository = providerAccountRepository;
            _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<EventPullService>.Instance;
            _deviceDiscovery = deviceDiscovery;
        }

        public async Task PullAccountEventsAsync(Guid accountId, DateTime? fromTimestampUtc, CancellationToken cancellationToken)
        {
            try
            {
                var account = await _providerAccountRepository.GetAsync(accountId, cancellationToken);
                if (account == null)
                {
                    _logger.LogWarning("Provider account not found");
                    return;
                }

                Inventory inventory = await DiscoverInventoryAsync(accountId, cancellationToken);

                if (inventory.LocationCount == 0)
                {
                    _logger.LogInformation("No locations found for account");
                    account.LastSuccessfulAuthUtc = DateTime.UtcNow;
                    account.LastErrorMessage = null;
                    await _providerAccountRepository.UpdateAsync(account, cancellationToken);
                    return;
                }

                var startTime = fromTimestampUtc ?? DateTime.MinValue;
                var endTime = DateTime.UtcNow;
                var deviceErrors = new List<string>();

                foreach (var device in inventory.Devices)
                {
                    if (device == null) continue;

                    try
                    {
                        _logger.LogDebug("Pulling events for device");

                        await _eventAndConfigService.GetEventsAsync(
                            accountId,
                            device.ProviderDeviceId,
                            startTime,
                            endTime,
                            eventType: null,
                            cancellationToken
                        );

                        await _eventAndConfigService.GetDeviceConfigAsync(accountId, device.ProviderDeviceId, cancellationToken);

                        device.LastSuccessfulPullAtUtc = DateTime.UtcNow;
                        device.LastPullAttemptAtUtc = DateTime.UtcNow;
                        await _deviceRepository.UpdateAsync(device, cancellationToken);

                        _logger.LogDebug("Successfully pulled events for device");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to pull events for device");
                        device.LastPullAttemptAtUtc = DateTime.UtcNow;
                        await _deviceRepository.UpdateAsync(device, cancellationToken);
                        var deviceName = device.Name ?? device.ProviderDeviceId ?? "Unknown Device";
                        deviceErrors.Add($"Device {deviceName}: {ex.Message}");
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
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to pull events for account");

                var account = await _providerAccountRepository.GetAsync(accountId, cancellationToken);
                if (account != null)
                {
                    account.LastErrorMessage = TruncateErrorMessage(ex.Message);
                    account.LastErrorUtc = DateTime.UtcNow;
                    await _providerAccountRepository.UpdateAsync(account, cancellationToken);
                }
            }
        }

        /// <summary>The locations and devices a pull works from. <see cref="LocationCount"/> zero means nothing to pull.</summary>
        private sealed record Inventory(int LocationCount, List<DeviceEntity> Devices);

        /// <summary>
        /// Asks the provider for the account's locations and devices and saves any that are new or changed. Errors propagate so the
        /// caller records them on the account rather than pulling from a partial inventory.
        /// </summary>
        private async Task<Inventory> DiscoverInventoryAsync(Guid accountId, CancellationToken cancellationToken)
        {
            IReadOnlyList<Location> remoteLocations = await _deviceDiscovery.GetLocationsAsync(accountId, cancellationToken);
            var devices = new List<DeviceEntity>();

            foreach (Location remoteLocation in remoteLocations)
            {
                LocationEntity location = await UpsertLocationAsync(accountId, remoteLocation, cancellationToken);
                IReadOnlyList<Device> remoteDevices = await _deviceDiscovery.GetDevicesAsync(accountId, remoteLocation.Id, cancellationToken);
                foreach (Device remoteDevice in remoteDevices)
                {
                    devices.Add(await UpsertDeviceAsync(location.Id, remoteDevice, cancellationToken));
                }
            }

            _logger.LogInformation("Discovered {LocationCount} location(s) and {DeviceCount} device(s) for account", remoteLocations.Count, devices.Count);
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

        private static string TruncateErrorMessage(string message, int maxLength = 2000)
        {
            if (message == null) return null;
            return message.Length > maxLength ? message.Substring(0, maxLength) : message;
        }
    }
}
