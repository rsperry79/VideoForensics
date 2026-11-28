using VideoForensics.Client.Common.Contracts;
using VideoForensics.Core.Api.Contracts;
using VideoForensics.Core.Api.Contracts.Repositories;
using VideoForensics.Core.Helpers.Platform;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Client.Core.Services
{
    /// <summary>
    /// Server-side <see cref="ILiveStillCaptureService"/>: resolves the camera's provider account, location and
    /// the Media root, then stores the frame through <see cref="ILiveStillStore"/>. Used by the upload endpoint and by
    /// the live-view page when it runs in the same process.
    /// </summary>
    public sealed class LiveStillCaptureService : ILiveStillCaptureService
    {
        private readonly IDeviceRepository _devices;
        private readonly ILocationRepository _locations;
        private readonly IProviderAccountRepository _accounts;
        private readonly IAccountDeviceRepository _accountDevices;
        private readonly IForensicsConfigurationService _configuration;
        private readonly IStorageLocationProvider _storageLocation;
        private readonly ILiveStillStore _stills;

        public LiveStillCaptureService(
            IDeviceRepository devices,
            ILocationRepository locations,
            IProviderAccountRepository accounts,
            IAccountDeviceRepository accountDevices,
            IForensicsConfigurationService configuration,
            IStorageLocationProvider storageLocation,
            ILiveStillStore stills)
        {
            _devices = devices;
            _locations = locations;
            _accounts = accounts;
            _accountDevices = accountDevices;
            _configuration = configuration;
            _storageLocation = storageLocation;
            _stills = stills;
        }

        /// <inheritdoc />
        public async Task<LiveStillResult> CaptureAsync(Guid deviceId, byte[] jpeg, string operatorId, CancellationToken ct)
        {
            DeviceDto? device = await _devices.GetAsync(deviceId, ct);
            if (device is null)
            {
                return LiveStillResult.Fail(LiveStillError.NotFound, "Camera not found.");
            }

            LocationRecordDto? location = await _locations.GetAsync(device.LocationId, ct);
            string? providerName = await FindProviderNameAsync(device.Id, ct);
            if (providerName is null)
            {
                return LiveStillResult.Fail(LiveStillError.NotFound, "No linked account can see this camera.");
            }

            IForensicsConfiguration config = await _configuration.LoadConfigurationAsync(string.Empty, ct);
            string mediaRoot = _storageLocation.GetEffectiveRoot(StorageCategory.Media, config.DownloadLocation);

            return await _stills.StoreAsync(new LiveStillRequest(
                device.Id,
                mediaRoot,
                providerName,
                location?.Name ?? string.Empty,
                device.Name,
                DateTime.UtcNow,
                jpeg,
                operatorId), ct);
        }

        /// <summary>The provider name of the first account that can see this camera, or null when none can.</summary>
        private async Task<string?> FindProviderNameAsync(Guid deviceId, CancellationToken ct)
        {
            foreach (ProviderAccountDto account in await _accounts.ListAsync(ct))
            {
                IReadOnlyList<Device> visible = await _accountDevices.ListByProviderAccountAsync(account.Id, ct);
                if (visible.Any(d => d.Id == deviceId))
                {
                    return account.ProviderName;
                }
            }

            return null;
        }
    }
}
