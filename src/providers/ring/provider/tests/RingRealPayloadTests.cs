using System.Text.Json;
using System.Text.Json.Nodes;

using VideoForensics.Core.Providers.Common.Contracts;
using VideoForensics.Providers.Ring.Entities;
using VideoForensics.Providers.Ring.Services;

using Xunit;

namespace VideoForensics.Providers.Ring.Tests
{
    /// <summary>
    /// Runs the device-details mappers against a sanitized capture of a real Ring device-settings response
    /// (Front Door, doorbot 86401464). Expected values are read from the fixture, not hand-copied.
    /// </summary>
    public class RingRealPayloadTests
    {
        private const string DeviceId = "86401464";

        private static JsonElement LoadFixture()
        {
            string path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "doorbot-settings-sanitized.json");
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            return doc.RootElement.Clone();
        }

        /// <summary>Wraps the captured settings as a doorbot's settings object, with zone_settings at the doorbot level when requested.</summary>
        private static Devices DoorbotDevices(JsonElement root, bool includeZoneSettings = true)
        {
            var doorbot = new JsonObject
            {
                ["id"] = long.Parse(DeviceId),
                ["settings"] = JsonNode.Parse(root.GetRawText())
            };
            if (includeZoneSettings)
            {
                doorbot["zone_settings"] = JsonNode.Parse(root.GetProperty("zone_settings").GetRawText());
            }

            var json = new JsonObject { ["doorbots"] = new JsonArray(doorbot) };
            return JsonSerializer.Deserialize<Devices>(json.ToJsonString())!;
        }

        /// <summary>Wraps the captured settings as a stickup cam's settings object, which carries pir_settings.</summary>
        private static Devices StickupCamDevices(JsonElement root)
        {
            var cam = new JsonObject
            {
                ["id"] = long.Parse(DeviceId),
                ["settings"] = JsonNode.Parse(root.GetRawText())
            };
            var json = new JsonObject { ["stickup_cams"] = new JsonArray(cam) };
            return JsonSerializer.Deserialize<Devices>(json.ToJsonString())!;
        }

        private static DeviceDetailFields DoorbotFields(JsonElement root, bool includeZoneSettings = true)
        {
            RingDeviceDetails? details = RingDeviceDetailsMapper.Find(DoorbotDevices(root, includeZoneSettings), DeviceId);
            Assert.NotNull(details);
            return details!.DeviceFields!;
        }

        [Fact]
        public void ZoneSettings_CapturedMotionZones_MapsCountNamesAndVertexCounts()
        {
            JsonElement root = LoadFixture();
            JsonElement.ArrayEnumerator capturedZones = root.GetProperty("zone_settings").GetProperty("motion").EnumerateArray();

            IReadOnlyList<DeviceZoneDetail>? zones = DoorbotFields(root).Zones;

            Assert.NotNull(zones);
            var motion = zones!.Where(z => z.Source == "motion").ToList();
            Assert.Equal(capturedZones.Count(), motion.Count);
            for (int i = 0; i < motion.Count; i++)
            {
                JsonElement captured = root.GetProperty("zone_settings").GetProperty("motion")[i];
                Assert.Equal(captured.GetProperty("name").GetString(), motion[i].Name);
                Assert.Equal(captured.GetProperty("vertices").GetArrayLength(), motion[i].Vertices.Count);
            }
        }

        [Fact]
        public void PirSettings_CapturedPayload_MapsSensitivityZoneEnableAndZoneMask()
        {
            JsonElement pir = LoadFixture().GetProperty("pir_settings");

            DeviceDetailFields? f = RingDeviceDetailsMapper.Find(StickupCamDevices(LoadFixture()), DeviceId)?.DeviceFields;

            Assert.NotNull(f);
            Assert.Equal(pir.GetProperty("sensitivity_1").GetInt32(), f!.PirSettingsSensitivity1);
            Assert.Equal(pir.GetProperty("sensitivity_2").GetInt32(), f.PirSettingsSensitivity2);
            Assert.Equal(pir.GetProperty("sensitivity_3").GetInt32(), f.PirSettingsSensitivity3);
            Assert.Equal(pir.GetProperty("zone_enable").GetInt32(), f.PirSettingsZoneEnable);
            Assert.Equal(pir.GetProperty("zone_mask").GetInt32(), f.PirSettingsZoneMask);
        }

        [Fact]
        public void LightSettings_CapturedPayloadHasNoLightSettingsObject_LightBrightnessIsNull()
        {
            JsonElement root = LoadFixture();
            Assert.False(root.TryGetProperty("light_settings", out _));

            DeviceDetailFields? f = RingDeviceDetailsMapper.Find(StickupCamDevices(root), DeviceId)?.DeviceFields;

            Assert.NotNull(f);
            Assert.Null(f!.LightBrightness);
        }

        [Fact]
        public void ChimeSettings_CapturedPayload_MapsTypeEnableAndDuration()
        {
            JsonElement chime = LoadFixture().GetProperty("chime_settings");

            DeviceDetailFields f = DoorbotFields(LoadFixture(), includeZoneSettings: false);

            Assert.Equal(chime.GetProperty("type").GetInt32(), f.SettingsChimeType);
            Assert.Equal(chime.GetProperty("enable").GetBoolean(), f.SettingsChimeEnabled);
            Assert.Equal(chime.GetProperty("duration").GetInt32(), f.SettingsChimeDuration);
        }

        [Fact]
        public void Doorbot_CapturedPayloadWithoutRootVolumeOrMotion_MapsNestedVolumeAndMotionDetection()
        {
            // Real captures carry no root-level doorbell_volume or motion_detection_enabled; the values live in
            // volume_settings and motion_settings. Expected values are read from the fixture.
            JsonElement root = LoadFixture();
            Assert.False(root.TryGetProperty("doorbell_volume", out _));
            Assert.False(root.TryGetProperty("motion_detection_enabled", out _));

            DeviceDetailFields f = DoorbotFields(root, includeZoneSettings: false);

            Assert.Equal(root.GetProperty("volume_settings").GetProperty("doorbell_volume").GetInt32(), f.DoorbellVolume);
            Assert.Equal(root.GetProperty("motion_settings").GetProperty("motion_detection_enabled").GetBoolean(), f.MotionDetectionEnabled);
        }

        [Fact]
        public void Doorbot_RootVolumeAndMotionWithoutNestedObjects_UsesRootAsFallback()
        {
            DeviceDetailFields f = DoorbotFieldsFromSettings("{\"doorbell_volume\":7,\"motion_detection_enabled\":false}");

            Assert.Equal(7, f.DoorbellVolume);
            Assert.Equal(false, f.MotionDetectionEnabled);
        }

        [Fact]
        public void Doorbot_RootAndNestedVolumeAndMotionPresent_NestedValuesWin()
        {
            DeviceDetailFields f = DoorbotFieldsFromSettings(
                "{\"doorbell_volume\":7,\"motion_detection_enabled\":false," +
                "\"volume_settings\":{\"doorbell_volume\":11},\"motion_settings\":{\"motion_detection_enabled\":true}}");

            Assert.Equal(11, f.DoorbellVolume);
            Assert.Equal(true, f.MotionDetectionEnabled);
        }

        /// <summary>Maps a doorbot whose settings object is the given JSON, with no zone_settings.</summary>
        private static DeviceDetailFields DoorbotFieldsFromSettings(string settingsJson)
        {
            var json = new JsonObject
            {
                ["doorbots"] = new JsonArray(new JsonObject
                {
                    ["id"] = long.Parse(DeviceId),
                    ["settings"] = JsonNode.Parse(settingsJson)
                })
            };
            Devices devices = JsonSerializer.Deserialize<Devices>(json.ToJsonString())!;
            RingDeviceDetails? details = RingDeviceDetailsMapper.Find(devices, DeviceId);
            Assert.NotNull(details);
            return details!.DeviceFields!;
        }
    }
}
