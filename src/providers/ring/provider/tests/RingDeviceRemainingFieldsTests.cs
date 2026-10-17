using System.Text.Json;

using VideoForensics.Core.Providers.Common.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Providers.Ring.Entities;
using VideoForensics.Providers.Ring.Services;

using Xunit;

namespace VideoForensics.Providers.Ring.Tests
{
    /// <summary>Maps the device fields the earlier audit left unstored: doorbot settings, zones, PIR and object settings, and the remaining device scalars.</summary>
    public class RingDeviceRemainingFieldsTests
    {
        private static readonly Guid DeviceId = Guid.Parse("11111111-2222-3333-4444-555555555555");

        private static DeviceDetailFields? DoorbotFields(Doorbot doorbot)
        {
            return RingDeviceDetailsMapper.Find(new Devices { Doorbots = [doorbot] }, doorbot.Id.ToString())?.DeviceFields;
        }

        private static DeviceDetailFields? StickupFields(StickupCam cam)
        {
            return RingDeviceDetailsMapper.Find(new Devices { StickupCams = [cam] }, cam.Id!.Value.ToString())?.DeviceFields;
        }

        private static T Parse<T>(string json)
        {
            return JsonSerializer.Deserialize<T>(json)!;
        }

        [Fact]
        public void ToDeviceFields_DoorbotSettings_MapsScalarsAndChimeSettings()
        {
            DeviceDetailFields? f = DoorbotFields(new Doorbot
            {
                Id = 1,
                Settings = new DoorbotSettings
                {
                    DoorbellVolume = 4,
                    NightModeOn = true,
                    MotionDetectionEnabled = false,
                    ChimeSettings = new DoorbotChimeSettings { Type = 2, Enable = true, Duration = 3 }
                }
            });

            Assert.NotNull(f);
            Assert.Equal(4, f!.DoorbellVolume);
            Assert.True(f.NightModeOn);
            Assert.False(f.MotionDetectionEnabled);
            Assert.Equal(2, f.SettingsChimeType);
            Assert.True(f.SettingsChimeEnabled);
            Assert.Equal(3, f.SettingsChimeDuration);
        }

        [Fact]
        public void ToDeviceFields_DoorbotSettingsUnknownKeys_StoredAsSettingsMetadataJson()
        {
            DoorbotSettings settings = Parse<DoorbotSettings>("{\"doorbell_volume\":2,\"future_key\":{\"a\":1}}");

            DeviceDetailFields? f = DoorbotFields(new Doorbot { Id = 1, Settings = settings });

            Assert.NotNull(f);
            Assert.Equal(2, f!.DoorbellVolume);
            Assert.NotNull(f.SettingsMetadataJson);
            Assert.Contains("future_key", f.SettingsMetadataJson);
        }

        [Fact]
        public void ToDeviceFields_DoorbotSettingsWithoutUnknownKeys_LeavesSettingsMetadataJsonNull()
        {
            DeviceDetailFields? f = DoorbotFields(new Doorbot { Id = 1, Settings = Parse<DoorbotSettings>("{\"doorbell_volume\":2}") });

            Assert.NotNull(f);
            Assert.Null(f!.SettingsMetadataJson);
        }

        [Fact]
        public void ToDeviceFields_DoorbotType_MapsRingType()
        {
            DeviceDetailFields? f = DoorbotFields(new Doorbot { Id = 1, Type = "doorbot" });

            Assert.NotNull(f);
            Assert.Equal("doorbot", f!.RingType);
        }

        [Fact]
        public void ToDeviceFields_DoorbotMotionSnoozeObject_StoredAsRawJson()
        {
            using JsonDocument doc = JsonDocument.Parse("{\"until\":5}");

            DeviceDetailFields? f = DoorbotFields(new Doorbot { Id = 1, MotionSnooze = doc.RootElement.Clone() });

            Assert.NotNull(f);
            Assert.Equal("{\"until\":5}", f!.MotionSnoozeJson);
        }

        [Fact]
        public void ToDeviceFields_DoorbotMotionSnoozeAbsent_LeavesNull()
        {
            DeviceDetailFields? f = DoorbotFields(new Doorbot { Id = 1 });

            Assert.NotNull(f);
            Assert.Null(f!.MotionSnoozeJson);
        }

        [Fact]
        public void ToDeviceFields_StickupCamMotionSnoozeObject_StoredAsRawJson()
        {
            StickupCam cam = Parse<StickupCam>("{\"id\":9,\"motion_snooze\":{\"until\":5}}");

            DeviceDetailFields? f = StickupFields(cam);

            Assert.NotNull(f);
            Assert.Equal("{\"until\":5}", f!.MotionSnoozeJson);
        }

        [Fact]
        public void ToDeviceFields_StickupCamRingFields_MapsRingIdBatteryAndNightMode()
        {
            StickupCam cam = Parse<StickupCam>("{\"id\":9,\"ring_id\":\"abc\",\"battery_life_2\":55,\"battery_voltage\":12.5,\"battery_voltage_2\":3.3,\"night_mode\":1}");

            DeviceDetailFields? f = StickupFields(cam);

            Assert.NotNull(f);
            Assert.Equal("abc", f!.RingRingId);
            Assert.Equal("55", f.BatteryLife2);
            Assert.Equal(12.5m, f.BatteryVoltage);
            Assert.Equal(3.3m, f.BatteryVoltage2);
            Assert.Equal(1, f.NightMode);
        }

        [Fact]
        public void ToDeviceFields_StickupCamLedStatusString_StoredAsText()
        {
            DeviceDetailFields? f = StickupFields(Parse<StickupCam>("{\"id\":9,\"led_status\":\"on\"}"));

            Assert.NotNull(f);
            Assert.Equal("on", f!.LedStatus);
        }

        [Fact]
        public void ToDeviceFields_StickupCamLedStatusObject_StoredAsRawJson()
        {
            DeviceDetailFields? f = StickupFields(Parse<StickupCam>("{\"id\":9,\"led_status\":{\"seconds_remaining\":0}}"));

            Assert.NotNull(f);
            Assert.Equal("{\"seconds_remaining\":0}", f!.LedStatus);
        }

        [Fact]
        public void ToDeviceFields_StickupCamSirenStatus_MapsSecondsRemaining()
        {
            DeviceDetailFields? f = StickupFields(Parse<StickupCam>("{\"id\":9,\"siren_status\":{\"seconds_remaining\":30}}"));

            Assert.NotNull(f);
            Assert.Equal(30, f!.SirenSecondsRemaining);
        }

        [Fact]
        public void ToDeviceFields_StickupCamPirSettings_MapsNestedSensitivitiesAndZoneWithoutTouchingTopLevelPir()
        {
            StickupCam cam = Parse<StickupCam>(
                "{\"id\":9,\"settings\":{\"pir_sensitivity_1\":7,\"pir_settings\":{\"sensitivity_1\":1,\"sensitivity_2\":2,\"sensitivity_3\":3,\"zone_enable\":1,\"zone_mask\":15}}}");

            DeviceDetailFields? f = StickupFields(cam);

            Assert.NotNull(f);
            Assert.Equal(7, f!.PirSensitivity1);
            Assert.Equal(1, f.PirSettingsSensitivity1);
            Assert.Equal(2, f.PirSettingsSensitivity2);
            Assert.Equal(3, f.PirSettingsSensitivity3);
            Assert.Equal(1, f.PirSettingsZoneEnable);
            Assert.Equal(15, f.PirSettingsZoneMask);
        }

        [Fact]
        public void ToDeviceFields_StickupCamAdvancedObjectSettings_MapsPresentKeysWithDayAndNight()
        {
            StickupCam cam = Parse<StickupCam>(
                "{\"id\":9,\"settings\":{\"advanced_object_settings\":{\"human_detection_confidence\":{\"day\":0.8,\"night\":0.6},\"object_size_maximum\":{\"day\":0.9}}}}");

            DeviceDetailFields? f = StickupFields(cam);

            Assert.NotNull(f?.AdvancedObjectSettings);
            Assert.Equal(2, f!.AdvancedObjectSettings!.Count);
            AdvancedObjectSettingDetail human = f.AdvancedObjectSettings.Single(s => s.SettingKey == "human_detection_confidence");
            Assert.Equal(0.8, human.Day);
            Assert.Equal(0.6, human.Night);
            AdvancedObjectSettingDetail maximum = f.AdvancedObjectSettings.Single(s => s.SettingKey == "object_size_maximum");
            Assert.Equal(0.9, maximum.Day);
            Assert.Null(maximum.Night);
        }

        [Fact]
        public void ToDeviceFields_StickupCamZoneSettings_MapsMotionAndPackageZonesWithDetectionTypesAndVertices()
        {
            StickupCam cam = Parse<StickupCam>(
                "{\"id\":9,\"zone_settings\":{" +
                "\"motion\":[{\"id\":\"7\",\"name\":\"Porch\",\"state\":1,\"properties\":{\"detection_types\":[\"human\",\"vehicle\"],\"style\":\"solid\"}," +
                "\"vertices\":[{\"x\":0.1,\"y\":0.2},{\"x\":0.3,\"y\":0.4}]}]," +
                "\"package\":[{\"id\":8,\"name\":\"Step\"}]}}");

            DeviceDetailFields? f = StickupFields(cam);

            Assert.NotNull(f?.Zones);
            Assert.Equal(2, f!.Zones!.Count);
            DeviceZoneDetail motion = f.Zones[0];
            Assert.Equal("motion", motion.Source);
            Assert.Equal("7", motion.ZoneId);
            Assert.Equal("Porch", motion.Name);
            Assert.Equal(1, motion.State);
            Assert.Equal("solid", motion.Style);
            Assert.Equal(["human", "vehicle"], motion.DetectionTypes);
            Assert.Equal(2, motion.Vertices.Count);
            Assert.Equal(1, motion.Vertices[0].Position);
            Assert.Equal(0.1, motion.Vertices[0].X);
            Assert.Equal(0.4, motion.Vertices[1].Y);
            DeviceZoneDetail package = f.Zones[1];
            Assert.Equal("package", package.Source);
            Assert.Equal("8", package.ZoneId);
            Assert.Empty(package.DetectionTypes);
            Assert.Empty(package.Vertices);
        }

        [Fact]
        public void ToDeviceFields_DoorbotZoneSettings_MapsZonesWithSameShape()
        {
            Doorbot doorbot = Parse<Doorbot>("{\"id\":1,\"zone_settings\":{\"motion\":[{\"id\":\"3\",\"name\":\"Driveway\"}]}}");

            DeviceDetailFields? f = DoorbotFields(doorbot);

            Assert.NotNull(f?.Zones);
            DeviceZoneDetail zone = Assert.Single(f!.Zones!);
            Assert.Equal("motion", zone.Source);
            Assert.Equal("Driveway", zone.Name);
        }

        [Fact]
        public void ToDeviceFields_DoorbotAdvancedObjectSettings_MapsObjectRowsAndLeavesOtherKeysInMetadata()
        {
            DeviceDetailFields? f = DoorbotFields(new Doorbot
            {
                Id = 1,
                Settings = Parse<DoorbotSettings>("{\"advanced_object_settings\":{\"object_time_overlap\":{\"day\":0.4,\"night\":0.3}},\"other\":1}")
            });

            Assert.NotNull(f?.AdvancedObjectSettings);
            AdvancedObjectSettingDetail setting = Assert.Single(f!.AdvancedObjectSettings!);
            Assert.Equal("object_time_overlap", setting.SettingKey);
            Assert.Equal(0.3, setting.Night);
            Assert.NotNull(f.SettingsMetadataJson);
            Assert.Contains("other", f.SettingsMetadataJson);
            Assert.DoesNotContain("advanced_object_settings", f.SettingsMetadataJson);
        }

        [Fact]
        public void ToDeviceFields_NoZoneSettings_LeavesZonesNull()
        {
            DeviceDetailFields? f = DoorbotFields(new Doorbot { Id = 1 });

            Assert.NotNull(f);
            Assert.Null(f!.Zones);
        }

        [Fact]
        public void ToDeviceFields_LightSettingsUnknownKeys_StoredAsLightSettingsMetadataJson()
        {
            StickupCam cam = Parse<StickupCam>("{\"id\":9,\"settings\":{\"light_settings\":{\"brightness\":50,\"schedule\":{\"on\":1}}}}");

            DeviceDetailFields? f = StickupFields(cam);

            Assert.NotNull(f);
            Assert.Equal(50, f!.LightBrightness);
            Assert.NotNull(f.LightSettingsMetadataJson);
            Assert.Contains("schedule", f.LightSettingsMetadataJson);
        }

        [Fact]
        public void ToDeviceFields_LightSettingsBrightnessOnly_LeavesLightSettingsMetadataJsonNull()
        {
            DeviceDetailFields? f = StickupFields(Parse<StickupCam>("{\"id\":9,\"settings\":{\"light_settings\":{\"brightness\":50}}}"));

            Assert.NotNull(f);
            Assert.Null(f!.LightSettingsMetadataJson);
        }

        [Fact]
        public void ToAlerts_ConnectionStatus_StoresValueOnAlertRow()
        {
            RingDeviceDetails? details = RingDeviceDetailsMapper.Find(
                new Devices { Doorbots = [new Doorbot { Id = 1, Alerts = new DoorbotAlerts { Connection = "online" } }] }, "1");

            IReadOnlyList<DeviceAlerts> alerts = RingDeviceDetailsMapper.ToAlerts(details!, DeviceId);

            DeviceAlerts alert = Assert.Single(alerts);
            Assert.Equal("connection", alert.AlertType);
            Assert.Equal("online", alert.AlertValue);
        }

        [Fact]
        public void ToSettingsChildren_StickupCamZonesAndAdvancedObjects_BuildsZoneRowsAndObjectRows()
        {
            DeviceDetailFields? f = StickupFields(Parse<StickupCam>(
                "{\"id\":9,\"zone_settings\":{\"motion\":[{\"id\":\"7\",\"vertices\":[{\"x\":0.1,\"y\":0.2}]}]}," +
                "\"settings\":{\"advanced_object_settings\":{\"motion_zone_overlap\":{\"day\":0.5}}}}"));

            DeviceSettingsChildren? children = RingDeviceDetailsMapper.ToSettingsChildren(f!, DeviceId);

            Assert.NotNull(children);
            DeviceZone zone = Assert.Single(children!.Zones);
            Assert.Equal(DeviceId, zone.DeviceId);
            Assert.Equal("motion", zone.Source);
            Assert.Equal("7", zone.ZoneId);
            DeviceZoneVertex vertex = Assert.Single(zone.Vertices);
            Assert.Equal(1, vertex.Position);
            Assert.Equal(0.1, vertex.X);
            DeviceAdvancedObjectSetting setting = Assert.Single(children.AdvancedObjectSettings);
            Assert.Equal("motion_zone_overlap", setting.SettingKey);
            Assert.Equal(0.5, setting.Day);
            Assert.Null(setting.Night);
        }
    }
}
