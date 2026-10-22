using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;

using VideoForensics.Core.Providers.Common.Contracts;
using VideoForensics.Data.Common.Contracts;
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
        DeviceDetailFields? DeviceFields = null,
        bool? ShowVodSettings = null,
        bool? RingtonesEnabled = null);

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
            TypedSettingDetails typed = RingDeviceSettingGroupParser.Parse(d.Settings?.AdditionalFields);
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
                Stolen: d.Stolen,
                OwnerId: OwnerIdText(d.Owner),
                OwnerFirstName: d.Owner?.FirstName,
                OwnerLastName: d.Owner?.LastName,
                OwnerEmail: d.Owner?.Email,
                RingType: d.Type,
                MotionSnoozeJson: RawJson(d.MotionSnooze),
                DoorbellVolume: ToInt(NestedLong(d.Settings?.AdditionalFields, "volume_settings", "doorbell_volume") ?? d.Settings?.DoorbellVolume),
                NightModeOn: d.Settings?.NightModeOn,
                MotionDetectionEnabled: NestedBool(d.Settings?.AdditionalFields, "motion_settings", "motion_detection_enabled") ?? d.Settings?.MotionDetectionEnabled,
                SettingsChimeType: ToInt(d.Settings?.ChimeSettings?.Type),
                SettingsChimeEnabled: d.Settings?.ChimeSettings?.Enable,
                SettingsChimeDuration: ToInt(d.Settings?.ChimeSettings?.Duration),
                SettingsMetadataJson: RingDeviceSettingGroupParser.RemainderJson(d.Settings?.AdditionalFields),
                OfflineMotion: typed.OfflineMotion,
                DetectionTypes: typed.DetectionTypes,
                DetectionThresholds: typed.Thresholds,
                Zones: ToZoneSettingsDetails(d.ZoneSettings),
                AdvancedObjectSettings: ToObjectSettingDetails(d.Settings?.AdvancedObjectSettings));
        }

        /// <summary>
        /// Builds the scalar device fields stored on the Devices row for a stickup cam, including its settings
        /// scalars, light brightness and the preset and advanced-zone lists that become child rows.
        /// </summary>
        public static DeviceDetailFields ToDeviceFields(StickupCam s)
        {
            StickupCamSettings? st = s.Settings;
            TypedSettingDetails typed = RingDeviceSettingGroupParser.Parse(st?.AdditionalFields);
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
                Stolen: s.Stolen,
                EnableVod: st?.EnableVod,
                ExposureControl: ToInt(st?.ExposureControl),
                MotionSnoozePresetProfile: st?.MotionSnoozePresetProfile,
                LiveViewPresetProfile: st?.LiveViewPresetProfile,
                PirSensitivity1: ToInt(st?.PirSensitivity1),
                VodSuspended: ToFlag(st?.VodSuspended),
                DoorbellVolume: ToInt(NestedLong(st?.AdditionalFields, "volume_settings", "doorbell_volume") ?? st?.DoorbellVolume),
                VodStatus: st?.VodStatus,
                AdvancedMotionDetectionEnabled: st?.AdvancedMotionDetectionEnabled,
                AdvancedMotionDetectionHumanOnlyMode: st?.AdvancedMotionDetectionHumanOnlyMode,
                EnableAudioRecording: st?.EnableAudioRecording,
                EnableWhiteLeds: ToFlag(st?.EnableWhiteLeds),
                LightBrightness: ToInt(st?.LightSettings?.Brightness),
                OwnerId: OwnerIdText(s.Owner),
                OwnerFirstName: s.Owner?.FirstName,
                OwnerLastName: s.Owner?.LastName,
                OwnerEmail: s.Owner?.Email,
                MotionSnoozePresets: st?.MotionSnoozePresets?.ToList(),
                LiveViewPresets: st?.LiveViewPresets?.ToList(),
                AdvancedMotionZones: st?.AdvancedMotionZones == null ? null : ToZoneDetails(st.AdvancedMotionZones),
                RingRingId: s.RingId,
                BatteryLife2: s.BatteryLife2?.ToString(CultureInfo.InvariantCulture),
                BatteryVoltage: s.BatteryVoltage,
                BatteryVoltage2: s.BatteryVoltage2,
                LedStatus: JsonText(s.LedStatus),
                SirenSecondsRemaining: ToInt(s.SirenStatus?.SecondsRemaining),
                NightMode: ToInt(s.NightMode),
                MotionSnoozeJson: RawJson(s.MotionSnooze),
                SettingsMetadataJson: RingDeviceSettingGroupParser.RemainderJson(st?.AdditionalFields),
                OfflineMotion: typed.OfflineMotion,
                DetectionTypes: typed.DetectionTypes,
                DetectionThresholds: typed.Thresholds,
                LightSettingsMetadataJson: ExtraJson(st?.LightSettings?.AdditionalFields),
                PirSettingsSensitivity1: ToInt(st?.PirSettings?.Sensitivity1),
                PirSettingsSensitivity2: ToInt(st?.PirSettings?.Sensitivity2),
                PirSettingsSensitivity3: ToInt(st?.PirSettings?.Sensitivity3),
                PirSettingsZoneEnable: ToInt(st?.PirSettings?.ZoneEnable),
                PirSettingsZoneMask: ToInt(st?.PirSettings?.ZoneMask),
                Zones: ToZoneSettingsDetails(s.ZoneSettings),
                AdvancedObjectSettings: ToObjectSettingDetails(st?.AdvancedObjectSettings));
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
                Stolen: c.Stolen,
                ChimeVolume: c.Settings?.Volume,
                DingAudioUserId: c.Settings?.DingAudioUserId,
                DingAudioId: c.Settings?.DingAudioId,
                MotionAudioUserId: c.Settings?.MotionAudioUserId,
                MotionAudioId: c.Settings?.MotionAudioId,
                DoNotDisturbSecondsLeft: ToInt(c.DoNotDisturb?.SecondsLeft),
                OwnerId: OwnerIdText(c.Owner),
                OwnerFirstName: c.Owner?.FirstName,
                OwnerLastName: c.Owner?.LastName,
                OwnerEmail: c.Owner?.Email);
        }

        /// <summary>
        /// Builds the child-row lists for a device's presets and advanced zones. Lists absent from the provider
        /// fields become empty, so a write clears rows Ring no longer reports. Returns null when there are no fields.
        /// </summary>
        public static DeviceSettingsChildren? ToSettingsChildren(DeviceDetailFields? fields, Guid deviceId)
        {
            if (fields == null)
            {
                return null;
            }

            List<DeviceMotionSnoozePreset> snooze = (fields.MotionSnoozePresets ?? [])
                .Select((p, i) => new DeviceMotionSnoozePreset { Id = Guid.NewGuid(), DeviceId = deviceId, Position = i, Preset = p })
                .ToList();
            List<DeviceLiveViewPreset> live = (fields.LiveViewPresets ?? [])
                .Select((p, i) => new DeviceLiveViewPreset { Id = Guid.NewGuid(), DeviceId = deviceId, Position = i, Preset = p })
                .ToList();
            List<DeviceAdvancedMotionZone> zones = (fields.AdvancedMotionZones ?? [])
                .Select(z => new DeviceAdvancedMotionZone
                {
                    Id = Guid.NewGuid(),
                    DeviceId = deviceId,
                    ZoneKey = z.ZoneKey,
                    Name = z.Name,
                    State = z.State,
                    Vertices = z.Vertices
                        .Select(v => new DeviceAdvancedMotionZoneVertex { Id = Guid.NewGuid(), Position = v.Position, X = v.X, Y = v.Y })
                        .ToList()
                })
                .ToList();

            // Zones and object settings stay null (untouched) when Ring did not send that object at all.
            List<DeviceZone>? zoneRows = fields.Zones?
                .Select(z => new DeviceZone
                {
                    Id = Guid.NewGuid(),
                    DeviceId = deviceId,
                    Source = z.Source,
                    ZoneId = z.ZoneId,
                    Name = z.Name,
                    State = z.State,
                    Style = z.Style,
                    DetectionTypes = z.DetectionTypes
                        .Select((t, i) => new DeviceZoneDetectionType { Id = Guid.NewGuid(), Position = i, DetectionType = t })
                        .ToList(),
                    Vertices = z.Vertices
                        .Select(v => new DeviceZoneVertex { Id = Guid.NewGuid(), Position = v.Position, X = v.X, Y = v.Y })
                        .ToList()
                })
                .ToList();
            List<DeviceAdvancedObjectSetting>? objectRows = fields.AdvancedObjectSettings?
                .Select(s => new DeviceAdvancedObjectSetting { Id = Guid.NewGuid(), DeviceId = deviceId, SettingKey = s.SettingKey, Day = s.Day, Night = s.Night })
                .ToList();

            // Typed settings come only from doorbots and stickup cams. Chimes do not supply them, so their rows stay untouched (null).
            List<DeviceOfflineMotionSettings>? offlineRows = null;
            List<DeviceDetectionType>? detectionRows = null;
            List<DeviceDetectionThreshold>? thresholdRows = null;
            if (fields.DetectionTypes != null)
            {
                offlineRows = fields.OfflineMotion == null
                    ? []
                    : [new DeviceOfflineMotionSettings
                    {
                        Id = Guid.NewGuid(),
                        DeviceId = deviceId,
                        Subscribed = fields.OfflineMotion.Subscribed,
                        Enabled = fields.OfflineMotion.Enabled,
                        MaxUploadKb = fields.OfflineMotion.MaxUploadKb,
                        ResolutionP = fields.OfflineMotion.ResolutionP,
                        FrequencyAfterSecs = fields.OfflineMotion.FrequencyAfterSecs,
                        PeriodAfterSecs = fields.OfflineMotion.PeriodAfterSecs,
                    }];
                detectionRows = fields.DetectionTypes
                    .Select(t => new DeviceDetectionType
                    {
                        Id = Guid.NewGuid(),
                        DeviceId = deviceId,
                        DetectionType = t.DetectionType,
                        Enabled = t.Enabled,
                        Mode = t.Mode,
                        Record = t.Record,
                        Notification = t.Notification,
                    })
                    .ToList();
                thresholdRows = (fields.DetectionThresholds ?? [])
                    .Select(t => new DeviceDetectionThreshold { Id = Guid.NewGuid(), DeviceId = deviceId, DetectionType = t.DetectionType, Threshold = t.Threshold })
                    .ToList();
            }

            return new DeviceSettingsChildren(snooze, live, zones, zoneRows, objectRows, offlineRows, detectionRows, thresholdRows);
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
                NightVisionEnabled = details.NightVisionEnabled,
                ShowVodSettings = details.ShowVodSettings,
                RingtonesEnabled = details.RingtonesEnabled
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

            return [new DeviceAlerts { Id = Guid.NewGuid(), DeviceId = deviceId, AlertType = "connection", IsEnabled = null, AlertValue = details.AlertConnection }];
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
                DeviceFields: ToDeviceFields(s),
                ShowVodSettings: f?.ShowVodSettings);
        }

        private static RingDeviceDetails FromChime(Chime c)
        {
            // Chime features (ringtones_enabled) are stored on a feature row, so HasFeatures follows the features object.
            return new RingDeviceDetails(
                TimeZone: c.TimeZone,
                FirmwareVersion: c.FirmwareVersion,
                HasAudio: null,
                MotionsEnabled: null,
                NightVisionEnabled: null,
                HasFeatures: c.Features != null,
                ShowRecordings: null,
                AdvancedMotionEnabled: null,
                PeopleOnlyEnabled: null,
                ShadowCorrectionEnabled: null,
                MotionMessageEnabled: null,
                AlertConnection: c.Alerts?.Connection,
                DeviceFields: ToDeviceFields(c),
                RingtonesEnabled: c.Features?.RingtonesEnabled);
        }

        private static IReadOnlyList<AdvancedMotionZoneDetail> ToZoneDetails(AdvancedMotionZones zones)
        {
            var result = new List<AdvancedMotionZoneDetail>();
            AddZone(result, "zone1", zones.Zone1);
            AddZone(result, "zone2", zones.Zone2);
            AddZone(result, "zone3", zones.Zone3);
            return result;
        }

        /// <summary>Adds a zone with its present vertices only; an absent vertex is skipped, not stored as 0,0.</summary>
        private static void AddZone(List<AdvancedMotionZoneDetail> result, string zoneKey, Zone? zone)
        {
            if (zone == null)
            {
                return;
            }

            Vertex?[] vertices = [zone.Vertex1, zone.Vertex2, zone.Vertex3, zone.Vertex4, zone.Vertex5, zone.Vertex6, zone.Vertex7, zone.Vertex8];
            List<AdvancedMotionZoneVertexDetail> present = [];
            for (int i = 0; i < vertices.Length; i++)
            {
                if (vertices[i] != null)
                {
                    present.Add(new AdvancedMotionZoneVertexDetail(i + 1, vertices[i]!.X, vertices[i]!.Y));
                }
            }

            result.Add(new AdvancedMotionZoneDetail(zoneKey, zone.Name, ToInt(zone.State), present));
        }

        private static string? OwnerIdText(Owner? owner)
        {
            return owner?.Id?.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>Ring sends on/off settings as 0 or 1. Any other value is unknown, so it maps to null.</summary>
        private static bool? ToFlag(long? value)
        {
            return value switch
            {
                0 => false,
                1 => true,
                _ => null
            };
        }

        /// <summary>Maps zone_settings to zone rows: motion zones first, then package zones. Null when Ring sent no zone_settings.</summary>
        private static IReadOnlyList<DeviceZoneDetail>? ToZoneSettingsDetails(ZoneSettings? zoneSettings)
        {
            if (zoneSettings == null)
            {
                return null;
            }

            var result = new List<DeviceZoneDetail>();
            AddZoneSettingsArray(result, "motion", zoneSettings.Motion);
            AddZoneSettingsArray(result, "package", zoneSettings.Package);
            return result;
        }

        private static void AddZoneSettingsArray(List<DeviceZoneDetail> result, string source, List<RingZone>? zones)
        {
            if (zones == null)
            {
                return;
            }

            foreach (RingZone zone in zones)
            {
                IReadOnlyList<AdvancedMotionZoneVertexDetail> vertices = (zone.Vertices ?? [])
                    .Select((v, i) => new AdvancedMotionZoneVertexDetail(i + 1, v?.X, v?.Y))
                    .ToList();
                result.Add(new DeviceZoneDetail(
                    Source: source,
                    ZoneId: zone.Id,
                    Name: zone.Name,
                    State: ToInt(zone.State),
                    Style: zone.Properties?.Style,
                    DetectionTypes: zone.Properties?.DetectionTypes?.ToList() ?? [],
                    Vertices: vertices));
            }
        }

        /// <summary>Maps the advanced object settings Ring sent, one row per key present. Null when Ring sent no object.</summary>
        private static IReadOnlyList<AdvancedObjectSettingDetail>? ToObjectSettingDetails(AdvancedObjectSettings? settings)
        {
            if (settings == null)
            {
                return null;
            }

            var result = new List<AdvancedObjectSettingDetail>();
            AddObjectSetting(result, "human_detection_confidence", settings.HumanDetectionConfidence);
            AddObjectSetting(result, "motion_zone_overlap", settings.MotionZoneOverlap);
            AddObjectSetting(result, "object_time_overlap", settings.ObjectTimeOverlap);
            AddObjectSetting(result, "object_size_minimum", settings.ObjectSizeMinimum);
            AddObjectSetting(result, "object_size_maximum", settings.ObjectSizeMaximum);
            return result;
        }

        private static void AddObjectSetting(List<AdvancedObjectSettingDetail> result, string key, HumanDetectionConfidence? value)
        {
            if (value != null)
            {
                result.Add(new AdvancedObjectSettingDetail(key, value.Day, value.Night));
            }
        }

        /// <summary>Raw JSON text of a value whose shape is not modelled, or null when Ring omitted it.</summary>
        private static string? RawJson(JsonElement? value)
        {
            return value is { ValueKind: not JsonValueKind.Null and not JsonValueKind.Undefined } element ? element.GetRawText() : null;
        }

        /// <summary>A string value as its text, or the raw JSON of a non-string value, or null when Ring omitted it.</summary>
        private static string? JsonText(JsonElement? value)
        {
            if (value is not { } element || element.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            {
                return null;
            }

            return element.ValueKind == JsonValueKind.String ? element.GetString() : element.GetRawText();
        }

        /// <summary>Serializes the settings keys not modelled by a named column. Null when there are none.</summary>
        private static string? ExtraJson(Dictionary<string, JsonElement>? extra)
        {
            return extra is { Count: > 0 } ? JsonSerializer.Serialize(extra) : null;
        }

        /// <summary>
        /// Reads a number from a nested settings object kept in AdditionalFields (e.g. volume_settings.doorbell_volume).
        /// Real captures put doorbell_volume and motion_detection_enabled there, not at the settings root.
        /// </summary>
        private static long? NestedLong(Dictionary<string, JsonElement>? fields, string objectKey, string valueKey)
        {
            if (fields == null || !fields.TryGetValue(objectKey, out JsonElement obj) || obj.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            return obj.TryGetProperty(valueKey, out JsonElement value) && value.TryGetInt64(out long number) ? number : null;
        }

        /// <summary>Reads a boolean from a nested settings object kept in AdditionalFields. Null when absent or not a boolean.</summary>
        private static bool? NestedBool(Dictionary<string, JsonElement>? fields, string objectKey, string valueKey)
        {
            if (fields == null || !fields.TryGetValue(objectKey, out JsonElement obj) || obj.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            if (!obj.TryGetProperty(valueKey, out JsonElement value))
            {
                return null;
            }

            return value.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                _ => null
            };
        }

        /// <summary>Narrows a Ring number to int, or null when it does not fit (never truncates silently).</summary>
        private static int? ToInt(long? value)
        {
            return value is >= int.MinValue and <= int.MaxValue ? (int)value.Value : null;
        }

        /// <summary>Rounds a Ring decimal to a whole number of seconds, or null when it does not fit an int.</summary>
        private static int? ToInt(decimal? value)
        {
            if (value == null)
            {
                return null;
            }

            decimal rounded = Math.Round(value.Value, MidpointRounding.AwayFromZero);
            return rounded is >= int.MinValue and <= int.MaxValue ? (int)rounded : null;
        }
    }
}
