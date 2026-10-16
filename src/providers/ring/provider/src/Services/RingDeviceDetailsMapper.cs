using System.Globalization;

using VideoForensics.Core.Providers.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Providers.Ring.Entities;

namespace VideoForensics.Providers.Ring.Services
{
    /// <summary>
    /// The Ring device fields that map onto DeviceCapabilities, DeviceFeatures and DeviceAlerts,
    /// read from one ring_devices entry. Null means Ring did not supply that value.
    /// </summary>
    public sealed record RingDeviceDetails(
        string? TimeZone,
        string? FirmwareVersion,
        bool? HasAudio,
        bool? MotionsEnabled,
        bool? NightVisionEnabled,
        bool HasFeatures,
        bool? ShowRecordings,
        bool? AdvancedMotionEnabled,
        bool? PeopleOnlyEnabled,
        bool? ShadowCorrectionEnabled,
        bool? MotionMessageEnabled,
        string? AlertConnection,
        DeviceDetailFields? DeviceFields = null);

    /// <summary>Finds a device in a ring_devices response and maps it onto the DB entities it fills.</summary>
    public static class RingDeviceDetailsMapper
    {
        /// <summary>Finds the device by provider id across doorbots, authorized doorbots, stickup cams and chimes.</summary>
        public static RingDeviceDetails? Find(Devices? devices, string providerDeviceId)
        {
            if (devices == null)
            {
                return null;
            }

            Doorbot? doorbot = devices.Doorbots?.FirstOrDefault(d => d.Id.ToString() == providerDeviceId)
                ?? devices.AuthorizedDoorbots?.FirstOrDefault(d => d.Id.ToString() == providerDeviceId);
            if (doorbot != null)
            {
                return FromDoorbot(doorbot);
            }

            StickupCam? stickupCam = devices.StickupCams?.FirstOrDefault(d => d.Id.HasValue && d.Id.Value.ToString() == providerDeviceId);
            if (stickupCam != null)
            {
                return FromStickupCam(stickupCam);
            }

            Chime? chime = devices.Chimes?.FirstOrDefault(c => c.Id.ToString() == providerDeviceId);
            return chime != null ? FromChime(chime) : null;
        }

        /// <summary>
        /// Builds the capability row. Ring exposes no resolution, storage or cloud-storage field in the
        /// device data, so those stay null rather than being inferred.
        /// </summary>
        public static DeviceCapabilities ToCapabilities(RingDeviceDetails details, Guid deviceId, DateTime nowUtc)
        {
            return new DeviceCapabilities
            {
                Id = Guid.NewGuid(),
                DeviceId = deviceId,
                HasAudio = details.HasAudio,
                HasMotionDetection = details.MotionsEnabled,
                HasNightVision = details.NightVisionEnabled,
                HasCloudStorage = null,
                FirmwareVersion = details.FirmwareVersion,
                LastSyncedUtc = nowUtc,
                SyncStatus = SyncStatus.Synced
            };
        }

        /// <summary>
        /// Builds the scalar device fields stored on the Devices row. Chimes report no subscription, battery or
        /// external-connection values, so those stay null for them.
        /// </summary>
        public static DeviceDetailFields ToDeviceFields(Doorbot d)
        {
            return new DeviceDetailFields(
                RingDeviceId: d.DeviceId,
                RingKind: d.Kind,
                FirmwareVersion: d.FirmwareVersion,
                Latitude: d.Latitude,
                Longitude: d.Longitude,
                Address: d.Address,
                Subscribed: d.Subscribed,
                SubscribedMotions: d.SubscribedMotions,
                BatteryLife: d.BatteryLife?.ToString(CultureInfo.InvariantCulture),
                ExternalConnection: d.ExternalConnection,
                Owned: d.Owned,
                Stolen: d.Stolen);
        }

        /// <summary>Builds the scalar device fields stored on the Devices row for a stickup cam.</summary>
        public static DeviceDetailFields ToDeviceFields(StickupCam s)
        {
            return new DeviceDetailFields(
                RingDeviceId: s.DeviceId,
                RingKind: s.Kind,
                FirmwareVersion: s.FirmwareVersion,
                Latitude: s.Latitude,
                Longitude: s.Longitude,
                Address: s.Address,
                Subscribed: s.Subscribed,
                SubscribedMotions: s.SubscribedMotions,
                BatteryLife: s.BatteryLife?.ToString(CultureInfo.InvariantCulture),
                ExternalConnection: s.ExternalConnection,
                Owned: s.Owned,
                Stolen: s.Stolen);
        }

        /// <summary>Builds the scalar device fields stored on the Devices row for a chime.</summary>
        public static DeviceDetailFields ToDeviceFields(Chime c)
        {
            return new DeviceDetailFields(
                RingDeviceId: c.DeviceId,
                RingKind: c.Kind,
                FirmwareVersion: c.FirmwareVersion,
                Latitude: c.Latitude,
                Longitude: c.Longitude,
                Address: c.Address,
                Owned: c.Owned,
                Stolen: c.Stolen);
        }

        /// <summary>Builds the feature row, or null when the device has no features object.</summary>
        public static DeviceFeatures? ToFeatures(RingDeviceDetails details, Guid deviceId)
        {
            if (!details.HasFeatures)
            {
                return null;
            }

            return new DeviceFeatures
            {
                Id = Guid.NewGuid(),
                DeviceId = deviceId,
                MotionsEnabled = details.MotionsEnabled,
                ShowRecordings = details.ShowRecordings,
                AdvancedMotionEnabled = details.AdvancedMotionEnabled,
                PeopleOnlyEnabled = details.PeopleOnlyEnabled,
                ShadowCorrectionEnabled = details.ShadowCorrectionEnabled,
                MotionMessageEnabled = details.MotionMessageEnabled,
                NightVisionEnabled = details.NightVisionEnabled
            };
        }

        /// <summary>
        /// Ring's alerts.connection is a status string (e.g. "online"), not an on/off switch. The row records
        /// that the connection alert exists and leaves IsEnabled unknown, since DeviceAlerts has no column
        /// for the string itself.
        /// </summary>
        public static IReadOnlyList<DeviceAlerts> ToAlerts(RingDeviceDetails details, Guid deviceId)
        {
            if (string.IsNullOrWhiteSpace(details.AlertConnection))
            {
                return [];
            }

            return [new DeviceAlerts { Id = Guid.NewGuid(), DeviceId = deviceId, AlertType = "connection", IsEnabled = null }];
        }

        private static RingDeviceDetails FromDoorbot(Doorbot d)
        {
            // Doorbot features have no audio flag, so HasAudio stays null.
            DoorbotFeatures? f = d.Features;
            return new RingDeviceDetails(
                TimeZone: d.TimeZone,
                FirmwareVersion: d.FirmwareVersion,
                HasAudio: null,
                MotionsEnabled: f?.MotionsEnabled,
                NightVisionEnabled: f?.NightVisionEnabled,
                HasFeatures: f != null,
                ShowRecordings: f?.ShowRecordings,
                AdvancedMotionEnabled: f?.AdvancedMotionEnabled,
                PeopleOnlyEnabled: f?.PeopleOnlyEnabled,
                ShadowCorrectionEnabled: f?.ShadowCorrectionEnabled,
                MotionMessageEnabled: f?.MotionMessageEnabled,
                AlertConnection: d.Alerts?.Connection,
                DeviceFields: ToDeviceFields(d));
        }

        private static RingDeviceDetails FromStickupCam(StickupCam s)
        {
            // Audio lives in the settings object; stickup cam features have no night-vision flag.
            StickupCamFeatures? f = s.Features;
            return new RingDeviceDetails(
                TimeZone: s.TimeZone,
                FirmwareVersion: s.FirmwareVersion,
                HasAudio: s.Settings?.EnableAudioRecording,
                MotionsEnabled: f?.MotionsEnabled,
                NightVisionEnabled: null,
                HasFeatures: f != null,
                ShowRecordings: f?.ShowRecordings,
                AdvancedMotionEnabled: null,
                PeopleOnlyEnabled: null,
                ShadowCorrectionEnabled: null,
                MotionMessageEnabled: null,
                AlertConnection: s.Alerts?.Connection,
                DeviceFields: ToDeviceFields(s));
        }

        private static RingDeviceDetails FromChime(Chime c)
        {
            // Chime features (ringtones_enabled) have no column, so HasFeatures stays false and no feature row is written.
            return new RingDeviceDetails(
                TimeZone: c.TimeZone,
                FirmwareVersion: c.FirmwareVersion,
                HasAudio: null,
                MotionsEnabled: null,
                NightVisionEnabled: null,
                HasFeatures: false,
                ShowRecordings: null,
                AdvancedMotionEnabled: null,
                PeopleOnlyEnabled: null,
                ShadowCorrectionEnabled: null,
                MotionMessageEnabled: null,
                AlertConnection: c.Alerts?.Connection,
                DeviceFields: ToDeviceFields(c));
        }
    }
}
