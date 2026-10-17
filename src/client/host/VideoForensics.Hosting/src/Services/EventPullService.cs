using Microsoft.Extensions.Logging;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Core.Helpers.Platform;
using VideoForensics.Core.Providers.Common.Contracts;
using VideoForensics.Core.Providers.Common.Services;
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
        /// <param name="fromTimestampUtc">Explicit start of the pull. Null means the account's watermark, or one year ago for the initial backfill.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>What the pull synced. Failures are recorded on the account, not thrown; the counts cover what was reached.</returns>
        Task<EventPullResult> PullAccountEventsAsync(Guid accountId, DateTime? fromTimestampUtc, DateTime? toTimestampUtc, CancellationToken cancellationToken);
    }

    /// <summary>
    /// Pulls events and configuration from provider accounts. Discovers the inventory, then runs the shared batched media pull
    /// (<see cref="IEventMediaPuller"/>) for the window. The account's last-pull watermark is the only "last sync" time, and it
    /// moves only past batches whose events were all pulled.
    /// </summary>
    public class EventPullService : IEventPullService
    {
        private readonly IEventAndConfigService _eventAndConfigService;
        private readonly IDeviceRepository _deviceRepository;
        private readonly ILocationRepository _locationRepository;
        private readonly IProviderAccountRepository _providerAccountRepository;
        private readonly ILogger<EventPullService> _logger;
        private readonly IDeviceDiscoveryService _deviceDiscovery;
        private readonly ISyncRunTally? _syncRunTally;
        private readonly ISyncRunOptions? _syncRunOptions;
        private readonly IMediaDownloadService? _mediaDownloadService;
        private readonly IEventMediaPuller _mediaPuller;
        private readonly string _mediaRoot;

        /// <summary>
        /// Creates a new instance of the EventPullService. Each pull first discovers the account's locations and devices, then pulls
        /// media for the window in batches. The sync kind in <paramref name="syncRunOptions"/> decides whether the events and config
        /// steps run; a missing registration runs both.
        /// </summary>
        /// <param name="mediaDownloadService">The provider's media download service. Null means no media can be pulled, so no event is counted.</param>
        /// <param name="mediaRoot">Root folder for media; each account writes under its own subfolder. Null uses the platform's media folder.</param>
        /// <param name="minRequestInterval">Minimum gap between provider media requests. Null uses one second.</param>
        public EventPullService(
            IEventAndConfigService eventAndConfigService,
            IDeviceRepository deviceRepository,
            ILocationRepository locationRepository,
            IProviderAccountRepository providerAccountRepository,
            IDeviceDiscoveryService deviceDiscovery,
            ILogger<EventPullService>? logger = null,
            ISyncRunTally? syncRunTally = null,
            ISyncRunOptions? syncRunOptions = null,
            IMediaDownloadService? mediaDownloadService = null,
            string? mediaRoot = null,
            TimeSpan? minRequestInterval = null,
            IEventMediaPuller? mediaPuller = null)
        {
            _syncRunOptions = syncRunOptions;
            _eventAndConfigService = eventAndConfigService;
            _deviceRepository = deviceRepository;
            _locationRepository = locationRepository;
            _providerAccountRepository = providerAccountRepository;
            _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<EventPullService>.Instance;
            _deviceDiscovery = deviceDiscovery;
            _syncRunTally = syncRunTally;
            _mediaDownloadService = mediaDownloadService;
            _mediaPuller = mediaPuller ?? new EventMediaPuller(minRequestInterval: minRequestInterval);
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
                _syncRunTally?.SetInventory(inventory.LocationCount, inventory.Devices.Count);

                if (inventory.LocationCount == 0)
                {
                    _logger.LogInformation("No locations found for account");
                    account.LastErrorMessage = null;
                    await _providerAccountRepository.UpdateAsync(account, cancellationToken);
                    return new EventPullResult(devicesSynced, locationsSynced);
                }

                SyncRunOptions runOptions = _syncRunOptions?.Current ?? new SyncRunOptions();
                bool runsEvents = runOptions.Kind is SyncRunKind.Default or SyncRunKind.Events;
                bool runsConfig = runOptions.Kind is SyncRunKind.Default or SyncRunKind.Config;
                var deviceErrors = new List<string>();

                if (runsEvents)
                {
                    DateTime nowUtc = DateTime.UtcNow;
                    DateTime windowStart = fromTimestampUtc ?? account.LastSuccessfulAuthUtc ?? nowUtc.AddYears(-1);
                    DateTime windowEnd = runOptions.EndUtc ?? nowUtc;
                    await PullMediaAsync(account, inventory, windowStart, windowEnd, advanceWatermark: runOptions.EndUtc is null, deviceErrors, cancellationToken);
                }

                if (runsConfig)
                {
                    foreach (InventoryDevice item in inventory.Devices)
                    {
                        try
                        {
                            _ = await _eventAndConfigService.GetDeviceConfigAsync(accountId, item.Device.ProviderDeviceId, cancellationToken);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to pull configuration for device");
                            _syncRunTally?.AddFeedFailure();
                            deviceErrors.Add($"Device {item.Device.Name ?? item.Device.ProviderDeviceId ?? "Unknown Device"}: {ex.Message}");
                        }
                    }
                }

                if (deviceErrors.Count > 0)
                {
                    account.LastErrorMessage = TruncateErrorMessage(string.Join("; ", deviceErrors));
                    account.LastErrorUtc = DateTime.UtcNow;
                }
                else
                {
                    account.LastErrorMessage = null;
                    account.LastErrorUtc = null;
                }

                await _providerAccountRepository.UpdateAsync(account, cancellationToken);

                _logger.LogInformation("Finished pulling events for account");
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

                return new EventPullResult(devicesSynced, locationsSynced, eventsSynced, configAreasSynced, feedsFailed);
            }
        }

        /// <summary>
        /// Runs the shared batched pull for the account's devices and records its honest counts. A device's last-pull time moves
        /// only when that device had new media pulled in this run. Without a media service nothing can be pulled, so nothing is counted.
        /// </summary>
        private async Task PullMediaAsync(
            ProviderAccount account,
            Inventory inventory,
            DateTime windowStart,
            DateTime windowEnd,
            bool advanceWatermark,
            List<string> deviceErrors,
            CancellationToken cancellationToken)
        {
            if (_mediaDownloadService is null)
            {
                _logger.LogWarning("No media download service is registered; events were not pulled");
                return;
            }

            // Each account writes into its own folder under the media root. The account id is only used in the path, never logged.
            string accountFolder = Path.Combine(_mediaRoot, account.Id.ToString("N"));
            _ = Directory.CreateDirectory(accountFolder);
            _mediaDownloadService.SetActiveProviderAccountId(account.Id);

            IReadOnlyList<MediaPullDevice> devices = inventory.Devices
                .Select(item => new MediaPullDevice(item.Device.ProviderDeviceId, accountFolder, item.ProviderLocationId))
                .ToList();

            var request = new MediaPullRequest(windowStart, windowEnd, advanceWatermark, devices);
            var watermarkStore = new AccountWatermarkStore(account, _providerAccountRepository);
            MediaPullResult result = await _mediaPuller.PullAsync(_mediaDownloadService, watermarkStore, request, cancellationToken);

            _syncRunTally?.AddEventsPulled(result.Pulled);
            _syncRunTally?.AddEventsAlreadyPresent(result.AlreadyPresent);
            _syncRunTally?.AddEventsNotPulled(result.NotPulled);
            for (int i = 0; i < result.FeedsFailed; i++)
            {
                _syncRunTally?.AddFeedFailure();
            }

            if (result.FeedsFailed > 0)
            {
                deviceErrors.Add($"{result.FeedsFailed} device media pull(s) failed");
            }

            _logger.LogInformation(
                "Media pull: {Pulled} downloaded, {AlreadyPresent} already present, {NotPulled} not pulled over {Batches} batch(es)",
                result.Pulled, result.AlreadyPresent, result.NotPulled, result.BatchesCompleted);

            DateTime nowUtc = DateTime.UtcNow;
            foreach (InventoryDevice item in inventory.Devices)
            {
                item.Device.LastPullAttemptAtUtc = nowUtc;
                MediaPullDeviceResult? deviceResult = result.Devices.FirstOrDefault(d => d.DeviceId == item.Device.ProviderDeviceId);
                if (deviceResult is { Pulled: > 0 })
                {
                    item.Device.LastSuccessfulPullAtUtc = nowUtc;
                }

                await _deviceRepository.UpdateAsync(item.Device, cancellationToken);
            }
        }

        /// <summary>A device with the provider location it was discovered under.</summary>
        private sealed record InventoryDevice(DeviceEntity Device, string ProviderLocationId);

        /// <summary>The locations and devices a pull works from. <see cref="LocationCount"/> zero means nothing to pull.</summary>
        private sealed record Inventory(int LocationCount, List<InventoryDevice> Devices);

        /// <summary>
        /// Persists the account's watermark (its LastSuccessfulAuthUtc, the last-pull time a normal sync starts from) through the account repository.
        /// </summary>
        private sealed class AccountWatermarkStore : IMediaPullProgressStore
        {
            private readonly ProviderAccount _account;
            private readonly IProviderAccountRepository _repository;

            public AccountWatermarkStore(ProviderAccount account, IProviderAccountRepository repository)
            {
                _account = account;
                _repository = repository;
            }

            public Task<DateTime?> GetWatermarkUtcAsync(CancellationToken cancellationToken) =>
                Task.FromResult(_account.LastSuccessfulAuthUtc);

            public async Task SetWatermarkUtcAsync(DateTime watermarkUtc, CancellationToken cancellationToken)
            {
                _account.LastSuccessfulAuthUtc = watermarkUtc;
                await _repository.UpdateAsync(_account, cancellationToken);
            }
        }

        /// <summary>
        /// Asks the provider for the account's locations and devices and saves any that are new or changed. Errors propagate so the
        /// caller records them on the account rather than pulling from a partial inventory.
        /// </summary>
        private async Task<Inventory> DiscoverInventoryAsync(Guid accountId, CancellationToken cancellationToken)
        {
            IReadOnlyList<Location> remoteLocations = await _deviceDiscovery.GetLocationsAsync(accountId, cancellationToken);
            var devices = new List<InventoryDevice>();
            int newDeviceCount = 0;

            foreach (Location remoteLocation in remoteLocations)
            {
                LocationEntity location = await UpsertLocationAsync(accountId, remoteLocation, cancellationToken);
                IReadOnlyList<Device> remoteDevices = await _deviceDiscovery.GetDevicesAsync(accountId, remoteLocation.Id, cancellationToken);
                foreach (Device remoteDevice in remoteDevices)
                {
                    (DeviceEntity device, bool created) = await UpsertDeviceAsync(location.Id, remoteDevice, cancellationToken);
                    if (created)
                    {
                        newDeviceCount++;
                    }

                    devices.Add(new InventoryDevice(device, remoteLocation.Id));
                }
            }

            _logger.LogInformation("Discovered {LocationCount} location(s) and {DeviceCount} device(s) for account", remoteLocations.Count, devices.Count);

            if (newDeviceCount > 0)
            {
                // The provider's config step ran before these rows existed, so their settings were skipped (the provider keeps
                // the config gate open for them). Asking for the locations again runs that step now that the rows are stored.
                // Done once per run; the repeat's result is only logged, and the inventory above is kept. The repeat runs as a
                // forced config pass with no event pulls, since the events were already pulled before this point in the run.
                IReadOnlyList<Location> repeated = await RepeatDiscoveryAsForcedConfigAsync(accountId, cancellationToken);
                _logger.LogInformation("Created {NewDeviceCount} device(s); repeated location discovery returned {LocationCount} location(s)", newDeviceCount, repeated.Count);
            }

            return new Inventory(remoteLocations.Count, devices);
        }

        /// <summary>
        /// Runs location discovery again with the sync kind set to Config, so the provider's config gate is forced open. The
        /// caller's options are restored afterwards, even when the call fails.
        /// </summary>
        private async Task<IReadOnlyList<Location>> RepeatDiscoveryAsForcedConfigAsync(Guid accountId, CancellationToken cancellationToken)
        {
            if (_syncRunOptions is null)
            {
                return await _deviceDiscovery.GetLocationsAsync(accountId, cancellationToken);
            }

            SyncRunOptions saved = _syncRunOptions.Current;
            _syncRunOptions.Current = saved with { Kind = SyncRunKind.Config };
            try
            {
                return await _deviceDiscovery.GetLocationsAsync(accountId, cancellationToken);
            }
            finally
            {
                _syncRunOptions.Current = saved;
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

        /// <summary>Saves the device, creating its row when none exists. Returns the row and whether this call created it.</summary>
        private async Task<(DeviceEntity Device, bool Created)> UpsertDeviceAsync(Guid locationId, Device remote, CancellationToken cancellationToken)
        {
            DeviceEntity? existing = await _deviceRepository.GetByProviderDeviceIdAsync(locationId, remote.Id, cancellationToken);
            bool created = existing == null;
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

            return (existing, created);
        }

        private static string TruncateErrorMessage(string message, int maxLength = 2000)
        {
            if (message == null) return null;
            return message.Length > maxLength ? message.Substring(0, maxLength) : message;
        }
    }
}
