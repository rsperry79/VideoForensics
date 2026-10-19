using VideoForensics.Core.Providers.Common.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Core.Contracts;

using ContractDevice = VideoForensics.Core.Providers.Common.Contracts.Device;
using ContractLocation = VideoForensics.Core.Providers.Common.Contracts.Location;
using StoredDevice = VideoForensics.Data.Common.Entities.Device;
using StoredLocation = VideoForensics.Data.Common.Entities.Location;

namespace VideoForensics.Data.Core.Services
{
    /// <summary>Reads locations and devices from the database only; never touches a provider.</summary>
    public sealed class StoredDeviceDiscoveryReader : IStoredDeviceReader
    {
        private readonly IDeviceRepository _devices;
        private readonly ILocationRepository _locations;

        public StoredDeviceDiscoveryReader(IDeviceRepository devices, ILocationRepository locations)
        {
            _devices = devices;
            _locations = locations;
        }

        public async Task<IReadOnlyList<ContractLocation>> GetLocationsAsync(CancellationToken ct)
        {
            IReadOnlyList<StoredLocation> rows = await _locations.ListAsync(ct);
            return rows.Select(x => new ContractLocation(x.ProviderLocationId, x.Name, x.Address)).ToList();
        }

        public async Task<IReadOnlyList<ContractDevice>> GetDevicesAsync(string providerLocationId, CancellationToken ct)
        {
            StoredLocation? location = await _locations.GetByProviderLocationIdAsync(providerLocationId, ct);
            if (location == null)
            {
                return [];
            }

            IReadOnlyList<StoredDevice> rows = await _devices.GetByLocationIdAsync(location.Id, ct);
            return rows.Select(x => ToContract(x, location.ProviderLocationId)).ToList();
        }

        public async Task<ContractDevice?> GetDeviceAsync(string providerDeviceId, CancellationToken ct)
        {
            // Provider device ids are not unique across locations in the repository API, so scan the stored rows.
            IReadOnlyList<StoredDevice> rows = await _devices.ListAsync(ct);
            StoredDevice? row = rows.FirstOrDefault(x => x.ProviderDeviceId == providerDeviceId);
            if (row == null)
            {
                return null;
            }

            StoredLocation? location = await _locations.GetAsync(row.LocationId, ct);
            return ToContract(row, location?.ProviderLocationId ?? string.Empty);
        }

        private static ContractDevice ToContract(StoredDevice row, string providerLocationId) =>
            new(row.ProviderDeviceId, row.Name, row.Type, providerLocationId, row.IsOnline, null, ToDetails(row));

        /// <summary>Inverse of VideoForensicsDataClient.ApplyDetailFields for the scalar columns it writes.</summary>
        internal static DeviceDetailFields ToDetails(StoredDevice d) => new(
                Address: d.Address,
                AdvancedMotionDetectionEnabled: d.AdvancedMotionDetectionEnabled,
                AdvancedMotionDetectionHumanOnlyMode: d.AdvancedMotionDetectionHumanOnlyMode,
                BatteryLife: d.BatteryLife,
                BatteryLife2: d.BatteryLife2,
                BatteryVoltage: d.BatteryVoltage,
                BatteryVoltage2: d.BatteryVoltage2,
                ChimeVolume: d.ChimeVolume,
                DingAudioId: d.DingAudioId,
                DingAudioUserId: d.DingAudioUserId,
                DoNotDisturbSecondsLeft: d.DoNotDisturbSecondsLeft,
                DoorbellVolume: d.DoorbellVolume,
                EnableAudioRecording: d.EnableAudioRecording,
                EnableVod: d.EnableVod,
                EnableWhiteLeds: d.EnableWhiteLeds,
                ExposureControl: d.ExposureControl,
                ExternalConnection: d.ExternalConnection,
                FirmwareVersion: d.FirmwareVersion,
                Latitude: d.Latitude,
                LedStatus: d.LedStatus,
                LightBrightness: d.LightBrightness,
                LightSettingsMetadataJson: d.LightSettingsMetadataJson,
                LiveViewPresetProfile: d.LiveViewPresetProfile,
                Longitude: d.Longitude,
                MotionAudioId: d.MotionAudioId,
                MotionAudioUserId: d.MotionAudioUserId,
                MotionDetectionEnabled: d.MotionDetectionEnabled,
                MotionSnoozeJson: d.MotionSnoozeJson,
                MotionSnoozePresetProfile: d.MotionSnoozePresetProfile,
                NightMode: d.NightMode,
                NightModeOn: d.NightModeOn,
                Owned: d.Owned,
                OwnerEmail: d.OwnerEmail,
                OwnerFirstName: d.OwnerFirstName,
                OwnerId: d.OwnerId,
                OwnerLastName: d.OwnerLastName,
                PirSensitivity1: d.PirSensitivity1,
                PirSettingsSensitivity1: d.PirSettingsSensitivity1,
                PirSettingsSensitivity2: d.PirSettingsSensitivity2,
                PirSettingsSensitivity3: d.PirSettingsSensitivity3,
                PirSettingsZoneEnable: d.PirSettingsZoneEnable,
                PirSettingsZoneMask: d.PirSettingsZoneMask,
                RingDeviceId: d.RingDeviceId,
                RingKind: d.RingKind,
                RingRingId: d.RingRingId,
                RingType: d.RingType,
                SettingsChimeDuration: d.SettingsChimeDuration,
                SettingsChimeEnabled: d.SettingsChimeEnabled,
                SettingsChimeType: d.SettingsChimeType,
                SettingsMetadataJson: d.SettingsMetadataJson,
                SirenSecondsRemaining: d.SirenSecondsRemaining,
                Stolen: d.Stolen,
                Subscribed: d.Subscribed,
                SubscribedMotions: d.SubscribedMotions,
                VodStatus: d.VodStatus,
                VodSuspended: d.VodSuspended);
    }
}
