using System.Globalization;
using System.Text.Json;

using VideoForensics.Core.Providers.Common.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Providers.Ring.Entities;

using RingDeviceDetailsRow = VideoForensics.Data.Common.Entities.RingDeviceDetails;

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
        bool? RingtonesEnabled = null,
        RingFeatureDetails? FeatureDetails = null,
        RingReportedHealth? ReportedHealth = null,
        IReadOnlyList<string>? UnstoredNotes = null,
        RingDeviceDetailsRow? RingRow = null,
        int? LedSecondsRemaining = null,
        bool? LedOn = null,
        RingNetworkGroups? NetworkGroups = null,
        string? AlertOtaStatus = null,
        RingSettingsGroups? SettingsGroups = null);

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
        public static DeviceDetailFields ToDeviceFields(Doorbot d, List<string>? notes = null)
        {
            return ToDeviceFields(d, TopOf(d, notes), notes);
        }

        private static RingTopLevelFields TopOf(Doorbot d, List<string>? notes)
        {
            return RingDeviceTopLevelParser.Parse(d.SchemaId, d.IsSidewalkGateway, d.DeactivatedAt, d.DeviceResourceId, d.SharedAt, d.RingNetId, d.ActiveScheduleUuid, d.FlickElimRecommendedMode, d.ExtPowerState, d.SnoozeSettings, d.EncryptionGroup, notes);
        }

        private static DeviceDetailFields ToDeviceFields(Doorbot d, RingTopLevelFields top, List<string>? notes)
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
                AdvancedObjectSettings: ToObjectSettingDetails(d.Settings?.AdvancedObjectSettings),
                RingRingId: d.RingId,
                DeactivatedAtUtc: top.DeactivatedAtUtc, SharedAtUtc: top.SharedAtUtc);
        }

        /// <summary>
        /// Builds the scalar device fields stored on the Devices row for a stickup cam, including its settings
        /// scalars, light brightness and the preset and advanced-zone lists that become child rows.
        /// </summary>
        public static DeviceDetailFields ToDeviceFields(StickupCam s, List<string>? notes = null)
        {
            return ToDeviceFields(s, TopOf(s, notes), notes);
        }

        private static RingTopLevelFields TopOf(StickupCam s, List<string>? notes)
        {
            return RingDeviceTopLevelParser.Parse(s.SchemaId, s.IsSidewalkGateway, s.DeactivatedAt, s.DeviceResourceId, s.SharedAt, s.RingNetId, s.ActiveScheduleUuid, s.FlickElimRecommendedMode, s.ExtPowerState, s.SnoozeSettings, s.EncryptionGroup, notes);
        }

        private static DeviceDetailFields ToDeviceFields(StickupCam s, RingTopLevelFields top, List<string>? notes)
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
                MotionSnoozePresetProfile: st?.MotionSnoozePresetProfile ?? NestedText(st?.AdditionalFields, "motion_settings", "motion_snooze_preset_profile"),
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
                AdvancedObjectSettings: ToObjectSettingDetails(st?.AdvancedObjectSettings),
                DeactivatedAtUtc: top.DeactivatedAtUtc, SharedAtUtc: top.SharedAtUtc);
        }

        /// <summary>Builds the scalar device fields stored on the Devices row for a chime.</summary>
        public static DeviceDetailFields ToDeviceFields(Chime c, List<string>? notes = null)
        {
            return ToDeviceFields(c, TopOf(c, notes), notes);
        }

        private static RingTopLevelFields TopOf(Chime c, List<string>? notes)
        {
            return RingDeviceTopLevelParser.Parse(c.SchemaId, c.IsSidewalkGateway, c.DeactivatedAt, c.DeviceResourceId, c.SharedAt, c.RingNetId, c.ActiveScheduleUuid, c.FlickElimRecommendedMode, c.ExtPowerState, c.SnoozeSettings, c.EncryptionGroup, notes);
        }

        private static DeviceDetailFields ToDeviceFields(Chime c, RingTopLevelFields top, List<string>? notes)
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
                OwnerEmail: c.Owner?.Email,
                RingRingId: c.RingId,
                DeactivatedAtUtc: top.DeactivatedAtUtc, SharedAtUtc: top.SharedAtUtc);
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

            var snooze = (fields.MotionSnoozePresets ?? [])
                .Select((p, i) => new DeviceMotionSnoozePreset { Id = Guid.NewGuid(), DeviceId = deviceId, Position = i, Preset = p })
                .ToList();
            var live = (fields.LiveViewPresets ?? [])
                .Select((p, i) => new DeviceLiveViewPreset { Id = Guid.NewGuid(), DeviceId = deviceId, Position = i, Preset = p })
                .ToList();
            var zones = (fields.AdvancedMotionZones ?? [])
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
            var zoneRows = fields.Zones?
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
            var objectRows = fields.AdvancedObjectSettings?
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

        /// <summary>
        /// Builds the settings child rows plus the device's feature-state rows. Feature states are replaced as a set on each sync;
        /// they stay null (stored rows untouched) when Ring sent no features object. With no scalar detail fields but feature states
        /// present, the children carry only the feature states.
        /// </summary>
        public static DeviceSettingsChildren? ToSettingsChildrenWithFeatures(RingDeviceDetails details, Guid deviceId)
        {
            DeviceSettingsChildren? children = ToSettingsChildren(details.DeviceFields, deviceId);
            if (details.FeatureDetails != null)
            {
                var states = details.FeatureDetails.States.Select(s => CopyState(s, deviceId)).ToList();
                children = (children ?? new DeviceSettingsChildren([], [], [])) with { FeatureStates = states };
            }

            children = (children ?? new DeviceSettingsChildren([], [], [])) with
            {
                RingDetails = details.RingRow == null ? null : CopyRow(details.RingRow, deviceId),
                LightSecondsRemaining = details.LedSecondsRemaining,
                LightIsOn = details.LedOn
            };

            // Health stays null (stored rows untouched) when Ring sent no health object; an empty object still yields a row of nulls.
            if (details.ReportedHealth != null)
            {
                RingReportedHealth h = details.ReportedHealth;
                children = (children ?? new DeviceSettingsChildren([], [], [])) with
                {
                    ReportedHealth = CopyRow(h.Health, deviceId),
                    RingHealthExtras = CopyRow(h.Extras, deviceId),
                    ReportedBatteries = h.Batteries.Select(b => CopyRow(b, deviceId)).ToList()
                };
            }

            // The paid flags and the network groups each have their own row. A group Ring did not send is null, which leaves its stored row untouched.
            if (details.NetworkGroups is { } groups)
            {
                children = (children ?? new DeviceSettingsChildren([], [], [])) with
                {
                    PaidFeatures = groups.PaidFeatures == null ? null : CopyRow(groups.PaidFeatures, deviceId),
                    NetworkSettings = groups.NetworkSettings == null ? null : CopyRow(groups.NetworkSettings, deviceId),
                    NetworkDiagnosis = groups.NetworkDiagnosis == null ? null : CopyRow(groups.NetworkDiagnosis, deviceId)
                };
            }

            // The Ring settings and general settings rows are null (stored rows untouched) when the device sent no settings object.
            if (details.SettingsGroups is { } settingRows)
            {
                children = (children ?? new DeviceSettingsChildren([], [], [])) with
                {
                    RingSettings = settingRows.Ring == null ? null : CopyRow(settingRows.Ring, deviceId),
                    GeneralSettings = settingRows.General == null ? null : CopyRow(settingRows.General, deviceId),
                    RingSettingGroups = settingRows.Groups == null ? null : CopyGroupRows(settingRows.Groups, deviceId)
                };
            }

            return children;
        }

        /// <summary>Copies the endpoint setting group rows onto fresh rows with their own Ids and the device id; each child list is copied element by element.</summary>
        private static RingDeviceSettingGroupRows CopyGroupRows(RingDeviceSettingGroupRows g, Guid deviceId)
        {
            return new RingDeviceSettingGroupRows(
                g.AdvancedMotion == null ? null : CopyRow(g.AdvancedMotion, deviceId),
                g.Backend == null ? null : CopyRow(g.Backend, deviceId),
                g.ClientDevice == null ? null : CopyRow(g.ClientDevice, deviceId),
                g.Cv == null ? null : CopyRow(g.Cv, deviceId),
                g.General == null ? null : CopyRow(g.General, deviceId),
                g.Misc == null ? null : CopyRow(g.Misc, deviceId),
                g.Motion == null ? null : CopyRow(g.Motion, deviceId),
                g.Snapshot == null ? null : CopyRow(g.Snapshot, deviceId),
                g.Stream == null ? null : CopyRow(g.Stream, deviceId),
                g.Video == null ? null : CopyRow(g.Video, deviceId),
                g.Vod == null ? null : CopyRow(g.Vod, deviceId),
                g.MotionSnoozeProfiles?.Select(r => CopyRow(r, deviceId)).ToList(),
                g.CvThresholdVersions?.Select(r => CopyRow(r, deviceId)).ToList(),
                g.CvThresholdValues?.Select(r => CopyRow(r, deviceId)).ToList());
        }

        /// <summary>Copies a parsed template onto a fresh row with its own Id and the device id, so repeated builds never share an instance.</summary>
        private static T CopyRow<T>(T template, Guid deviceId) where T : new()
        {
            var row = new T();
            foreach (System.Reflection.PropertyInfo p in typeof(T).GetProperties())
            {
                if (p.CanRead && p.CanWrite)
                {
                    p.SetValue(row, p.GetValue(template));
                }
            }

            typeof(T).GetProperty("Id")!.SetValue(row, Guid.NewGuid());
            typeof(T).GetProperty("DeviceId")!.SetValue(row, deviceId);
            return row;
        }

        private static RingDeviceFeatureState CopyState(RingDeviceFeatureState s, Guid deviceId)
        {
            Guid id = Guid.NewGuid();
            return new RingDeviceFeatureState
            {
                Id = id,
                DeviceId = deviceId,
                FeatureKey = s.FeatureKey,
                Eligible = s.Eligible,
                Allowed = s.Allowed,
                Enabled = s.Enabled,
                IsEnabled = s.IsEnabled,
                IsEligible = s.IsEligible,
                AllowsNoLocation = s.AllowsNoLocation,
                MaxDigitalZoomLevel = s.MaxDigitalZoomLevel,
                RtMaxDigitalZoomLevel = s.RtMaxDigitalZoomLevel,
                Score = s.Score,
                Learning = s.Learning,
                RecordingMode = s.RecordingMode,
                RecordingEnabled = s.RecordingEnabled,
                RecordingState = s.RecordingState,
                ContinuousSupported = s.ContinuousSupported,
                ContinuousAvailable = s.ContinuousAvailable,
                ContinuousUnavailabilityReason = s.ContinuousUnavailabilityReason,
                Reasons = s.Reasons
                    .Select(r => new RingDeviceFeatureReason { Id = Guid.NewGuid(), FeatureStateId = id, Kind = r.Kind, Position = r.Position, Reason = r.Reason })
                    .ToList()
            };
        }

        /// <summary>Builds the feature row, or null when the device has no features object.</summary>
        public static DeviceFeatures? ToFeatures(RingDeviceDetails details, Guid deviceId)
        {
            DeviceFeatures? scalars = details.FeatureDetails?.Scalars;
            return !details.HasFeatures
                ? null
                : new DeviceFeatures
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
                    RingtonesEnabled = details.RingtonesEnabled,
                    CfesEligible = scalars?.CfesEligible,
                    PackageWarning = scalars?.PackageWarning,
                    AutoZoomTrack = scalars?.AutoZoomTrack,
                    AutomatedSiren = scalars?.AutomatedSiren,
                    AlexaPlusGreetings = scalars?.AlexaPlusGreetings,
                    RemoteAccessControl = scalars?.RemoteAccessControl,
                    AutoShutoffDevice = scalars?.AutoShutoffDevice,
                    ShortOnLongOff = scalars?.ShortOnLongOff,
                    Tracklight = scalars?.Tracklight,
                    MotionZoneRecommendation = scalars?.MotionZoneRecommendation,
                    RichNotificationsEligible = scalars?.RichNotificationsEligible,
                    ShowOfflineMotionEvents = scalars?.ShowOfflineMotionEvents,
                    SheilaCameraEligible = scalars?.SheilaCameraEligible,
                    SheilaCameraProcessingEligible = scalars?.SheilaCameraProcessingEligible,
                    DynamicNetworkSwitchingEligible = scalars?.DynamicNetworkSwitchingEligible,
                    VehicleDetectionEligible = scalars?.VehicleDetectionEligible,
                    AnimalDetectionEligible = scalars?.AnimalDetectionEligible,
                    Show24x7Lite = scalars?.Show24x7Lite,
                    Recording24x7Eligible = scalars?.Recording24x7Eligible,
                    PropertyView = scalars?.PropertyView
                };
        }

        /// <summary>
        /// Ring's alerts.connection is a status string (e.g. "online"), not an on/off switch. The row records
        /// that the connection alert exists and leaves IsEnabled unknown, since DeviceAlerts has no column
        /// for the string itself.
        /// </summary>
        public static IReadOnlyList<DeviceAlerts> ToAlerts(RingDeviceDetails details, Guid deviceId)
        {
            var rows = new List<DeviceAlerts>();
            if (!string.IsNullOrWhiteSpace(details.AlertConnection))
            {
                rows.Add(new DeviceAlerts { Id = Guid.NewGuid(), DeviceId = deviceId, AlertType = "connection", IsEnabled = null, AlertValue = details.AlertConnection });
            }

            // alerts.ota_status is stored the same way as the connection status: a row whose value is the Ring text.
            if (!string.IsNullOrWhiteSpace(details.AlertOtaStatus))
            {
                rows.Add(new DeviceAlerts { Id = Guid.NewGuid(), DeviceId = deviceId, AlertType = "ota_status", IsEnabled = null, AlertValue = details.AlertOtaStatus });
            }

            return rows;
        }

        /// <summary>
        /// Stickup cam settings keys that StickupCamSettings types are absent from AdditionalFields, so their typed values are copied onto the
        /// settings row here. The row keeps the parser value for the snooze profile when the typed key is absent.
        /// </summary>
        private static void ApplyStickupTyped(RingDeviceSettings? ring, StickupCamSettings? st)
        {
            if (ring == null || st == null)
            {
                return;
            }

            ring.EnableVod = st.EnableVod;
            ring.ExposureControl = ToInt(st.ExposureControl);
            ring.VodSuspended = ToFlag(st.VodSuspended);
            ring.PirSensitivity1 = ToInt(st.PirSensitivity1);
            ring.EnableAudioRecording = st.EnableAudioRecording;
            ring.AdvancedMotionDetectionEnabled = st.AdvancedMotionDetectionEnabled;
            ring.AdvancedMotionDetectionHumanOnlyMode = st.AdvancedMotionDetectionHumanOnlyMode;
            ring.LiveViewPresetProfile = st.LiveViewPresetProfile;
            ring.MotionSnoozePresetProfile = st.MotionSnoozePresetProfile ?? ring.MotionSnoozePresetProfile;
        }

        /// <summary>A string value from a nested object of additional fields; null when absent or not a string.</summary>
        private static string? NestedText(Dictionary<string, JsonElement>? fields, string objectKey, string valueKey)
        {
            return fields != null && fields.TryGetValue(objectKey, out JsonElement obj) && obj.ValueKind == JsonValueKind.Object
                && obj.TryGetProperty(valueKey, out JsonElement v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
        }

        /// <summary>Parses a features object and adds its unstored-value notes to the device's notes.</summary>
        private static RingFeatureDetails ParseFeatures(IReadOnlyDictionary<string, JsonElement>? additional, List<string> notes)
        {
            RingFeatureDetails details = RingDeviceFeatureParser.Parse(additional);
            notes.AddRange(details.Unstored ?? []);
            return details;
        }

        private static RingDeviceDetails FromDoorbot(Doorbot d)
        {
            // Doorbot features have no audio flag, so HasAudio stays null.
            DoorbotFeatures? f = d.Features;
            var notes = new List<string>();
            RingTopLevelFields top = TopOf(d, notes);
            DeviceDetailFields fields = ToDeviceFields(d, top, notes);
            RingReportedHealth? health = RingDeviceHealthParser.Parse(d.Health);
            notes.AddRange(health?.Unstored ?? []);
            RingNetworkGroups groups = RingDeviceNetworkParser.Parse(d.Settings?.AdditionalFields);
            notes.AddRange(groups.Unstored);
            RingSettingsGroups settings = RingDeviceSettingsParser.Parse(d.Settings != null, d.Settings?.AdditionalFields, d.CreatedAt);
            notes.AddRange(settings.Unstored);
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
                DeviceFields: fields,
                ShowVodSettings: f?.ShowVodSettings,
                FeatureDetails: f == null ? null : ParseFeatures(f.AdditionalFeatures, notes),
                ReportedHealth: health,
                UnstoredNotes: notes,
                RingRow: top.Row,
                NetworkGroups: groups,
                AlertOtaStatus: d.Alerts?.OtaStatus,
                SettingsGroups: settings);
        }

        private static RingDeviceDetails FromStickupCam(StickupCam s)
        {
            // Audio lives in the settings object; stickup cam features have no night-vision flag.
            StickupCamFeatures? f = s.Features;
            var notes = new List<string>();
            RingTopLevelFields top = TopOf(s, notes);
            DeviceDetailFields fields = ToDeviceFields(s, top, notes);
            (int? ledSeconds, bool? ledOn) = LedState(s.LedStatus, notes);
            RingReportedHealth? health = RingDeviceHealthParser.Parse(s.Health);
            notes.AddRange(health?.Unstored ?? []);
            RingNetworkGroups groups = RingDeviceNetworkParser.Parse(s.Settings?.AdditionalFields);
            notes.AddRange(groups.Unstored);
            RingSettingsGroups settings = RingDeviceSettingsParser.Parse(s.Settings != null, s.Settings?.AdditionalFields, s.CreatedAt);
            ApplyStickupTyped(settings.Ring, s.Settings);
            notes.AddRange(settings.Unstored);
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
                MotionMessageEnabled: f?.MotionMessageEnabled,
                AlertConnection: s.Alerts?.Connection,
                DeviceFields: fields,
                ShowVodSettings: f?.ShowVodSettings,
                FeatureDetails: f == null ? null : ParseFeatures(f.AdditionalFeatures, notes),
                ReportedHealth: health,
                UnstoredNotes: notes,
                RingRow: top.Row,
                LedSecondsRemaining: ledSeconds,
                LedOn: ledOn,
                NetworkGroups: groups,
                AlertOtaStatus: s.Alerts?.OtaStatus,
                SettingsGroups: settings);
        }

        private static RingDeviceDetails FromChime(Chime c)
        {
            // Chime features (ringtones_enabled) are stored on a feature row, so HasFeatures follows the features object.
            var notes = new List<string>();
            RingTopLevelFields top = TopOf(c, notes);
            DeviceDetailFields fields = ToDeviceFields(c, top, notes);
            RingReportedHealth? health = RingDeviceHealthParser.Parse(c.Health);
            notes.AddRange(health?.Unstored ?? []);
            RingSettingsGroups settings = RingDeviceSettingsParser.Parse(c.Settings != null, c.Settings?.AdditionalFields, c.CreatedAt);
            notes.AddRange(settings.Unstored);
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
                DeviceFields: fields,
                RingtonesEnabled: c.Features?.RingtonesEnabled,
                FeatureDetails: c.Features == null ? null : ParseFeatures(c.Features.AdditionalFeatures, notes),
                ReportedHealth: health,
                UnstoredNotes: notes,
                RingRow: top.Row,
                AlertOtaStatus: c.Alerts?.OtaStatus,
                SettingsGroups: settings);
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

        /// <summary>
        /// Reads led_status for a light event: the object form gives seconds remaining; the plain strings "on" and "off" give the
        /// state with no duration. Any other shape (a number, another string, an object without whole-number seconds) is noted by
        /// key and JSON type only and not stored.
        /// </summary>
        private static (int? Seconds, bool? On) LedState(JsonElement? led, List<string>? notes)
        {
            if (led is not { } e || e.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            {
                return (null, null);
            }

            if (e.ValueKind == JsonValueKind.Object && e.TryGetProperty("seconds_remaining", out JsonElement seconds)
                && RingJsonValue.TryInt(seconds, out int? value))
            {
                return (value, null);
            }

            if (e.ValueKind == JsonValueKind.String)
            {
                string? text = e.GetString();
                if (string.Equals(text, "on", StringComparison.OrdinalIgnoreCase))
                {
                    return (null, true);
                }

                if (string.Equals(text, "off", StringComparison.OrdinalIgnoreCase))
                {
                    return (null, false);
                }
            }

            notes?.Add($"led_status.seconds_remaining not stored: missing or unexpected shape ({e.ValueKind})");
            return (null, null);
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
            return fields == null || !fields.TryGetValue(objectKey, out JsonElement obj) || obj.ValueKind != JsonValueKind.Object
                ? null
                : obj.TryGetProperty(valueKey, out JsonElement value) && value.TryGetInt64(out long number) ? number : null;
        }

        /// <summary>Reads a boolean from a nested settings object kept in AdditionalFields. Null when absent or not a boolean.</summary>
        private static bool? NestedBool(Dictionary<string, JsonElement>? fields, string objectKey, string valueKey)
        {
            return fields == null || !fields.TryGetValue(objectKey, out JsonElement obj) || obj.ValueKind != JsonValueKind.Object
                ? null
                : !obj.TryGetProperty(valueKey, out JsonElement value)
                ? null
                : value.ValueKind switch
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
