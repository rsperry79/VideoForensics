using Microsoft.Extensions.Logging;

using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Core.Contracts;
using VideoForensics.Providers.Ring.Catalog;
using VideoForensics.Providers.Ring.Interfaces;

namespace VideoForensics.Providers.Ring.Services
{
    /// <summary>
    /// Writes one device's settings graph (presets, zones, advanced object settings, features and alerts) through the same
    /// mapper and data-client writer the download path uses in <c>RingMediaDownloadService.CaptureDeviceHealthSnapshotAsync</c>.
    /// When the details carry the scalar detail fields, the device's scalar columns are written too (with setting-change history).
    /// The device row must already exist; a device the database does not hold yet is skipped, not created here.
    /// Capabilities are not written: the settings endpoint does not carry the audio flag they are built from, so writing them
    /// from it would clear values the download path stored.
    /// </summary>
    public class RingDeviceSettingsPersister
    {
        private const string RingProviderName = "Ring";

        private static readonly IRingModelCatalog RingCatalog = new RingModelCatalog();

        private readonly IVideoForensicsDataClient _dataClient;
        private readonly IDeviceRepository _deviceRepository;
        private readonly ILogger _logger;

        public RingDeviceSettingsPersister(IVideoForensicsDataClient dataClient, IDeviceRepository deviceRepository, ILogger logger)
        {
            _dataClient = dataClient ?? throw new ArgumentNullException(nameof(dataClient));
            _deviceRepository = deviceRepository ?? throw new ArgumentNullException(nameof(deviceRepository));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Persists the device's settings graph. Returns true when a row was written, false when the device is not stored yet.
        /// Logs no provider ids or URLs.
        /// </summary>
        public async Task<bool> PersistAsync(string providerDeviceId, RingDeviceDetails details, CancellationToken ct)
        {
            Device? device = (await _deviceRepository.ListAsync(ct)).FirstOrDefault(d => d.ProviderDeviceId == providerDeviceId);
            if (device == null)
            {
                _logger.LogDebug("Device settings not stored: device is not in the database yet; the next config sync retries");
                return false;
            }

            Guid deviceId = device.Id;
            await RecordUnknownKindAsync(device, details, ct);
            await _dataClient.RecordDeviceDetailsAsync(
                deviceId,
                null,
                null,
                RingDeviceDetailsMapper.ToFeatures(details, deviceId),
                RingDeviceDetailsMapper.ToAlerts(details, deviceId),
                ct,
                RingDeviceDetailsMapper.ToSettingsChildren(details.DeviceFields, deviceId));
            // The scalar columns come from the same details the child rows were built from; a null field set writes nothing.
            if (details.DeviceFields != null)
            {
                await _dataClient.UpdateDeviceDetailFieldsAsync(deviceId, details.DeviceFields, ct);
            }
            return true;
        }

        /// <summary>
        /// Records a kind the Ring catalog does not list, so the database knows the device model exists. The family comes from
        /// the device's Ring type. Known kinds and devices without a kind are left alone.
        /// </summary>
        private async Task RecordUnknownKindAsync(Device device, RingDeviceDetails details, CancellationToken ct)
        {
            string? kind = details.DeviceFields?.RingKind;
            if (string.IsNullOrWhiteSpace(kind) || RingCatalog.Find(kind) != null)
            {
                return;
            }

            await _dataClient.EnsureDeviceModelAsync(RingProviderName, kind, FamilyForType(device.Type), ct);
        }

        /// <summary>Maps a Ring device type (as discovery sets it on the device row) to its product family.</summary>
        internal static string FamilyForType(string type)
        {
            return type switch
            {
                "doorbot" or "authorized_doorbot" => RingDeviceFamily.Doorbell.ToString(),
                "stickup_cam" => RingDeviceFamily.Camera.ToString(),
                "chime" => RingDeviceFamily.Chime.ToString(),
                _ => "Unknown",
            };
        }
    }
}
