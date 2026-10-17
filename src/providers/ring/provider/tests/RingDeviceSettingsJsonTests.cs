using System.Text.Json;

using VideoForensics.Providers.Ring.Entities;

using Xunit;

namespace VideoForensics.Providers.Ring.Tests
{
    /// <summary>Confirms the JSON property names and shapes of the device settings entities added for the remaining audit fields.</summary>
    public class RingDeviceSettingsJsonTests
    {
        [Fact]
        public void DoorbotSettings_Deserializes_DoorbellVolumeNightModeMotionAndChime()
        {
            DoorbotSettings? s = JsonSerializer.Deserialize<DoorbotSettings>(
                "{\"doorbell_volume\":3,\"night_mode_on\":true,\"motion_detection_enabled\":false,\"chime_settings\":{\"type\":2,\"enable\":true,\"duration\":5}}");

            Assert.NotNull(s);
            Assert.Equal(3, s!.DoorbellVolume);
            Assert.True(s.NightModeOn);
            Assert.False(s.MotionDetectionEnabled);
            Assert.Equal(2, s.ChimeSettings!.Type);
            Assert.True(s.ChimeSettings.Enable);
            Assert.Equal(5, s.ChimeSettings.Duration);
            Assert.Null(s.AdditionalFields);
        }

        [Fact]
        public void StickupCamPirSettings_Deserializes_SensitivityUnderscoreKeysAndZoneFields()
        {
            StickupCamSettings? s = JsonSerializer.Deserialize<StickupCamSettings>(
                "{\"pir_settings\":{\"sensitivity_1\":1,\"sensitivity_2\":2,\"sensitivity_3\":3,\"zone_enable\":1,\"zone_mask\":15}}");

            Assert.NotNull(s?.PirSettings);
            Assert.Equal(1, s!.PirSettings!.Sensitivity1);
            Assert.Equal(2, s.PirSettings.Sensitivity2);
            Assert.Equal(3, s.PirSettings.Sensitivity3);
            Assert.Equal(1, s.PirSettings.ZoneEnable);
            Assert.Equal(15, s.PirSettings.ZoneMask);
        }

        [Fact]
        public void ZoneSettings_Deserializes_MotionAndPackageArrays()
        {
            ZoneSettings? z = JsonSerializer.Deserialize<ZoneSettings>(
                "{\"motion\":[{\"id\":\"7\",\"name\":\"Porch\",\"state\":1,\"properties\":{\"detection_types\":[\"human\"],\"style\":\"solid\"},\"vertices\":[{\"x\":0.5,\"y\":0.25}]}],\"package\":[]}");

            Assert.NotNull(z);
            RingZone motion = Assert.Single(z!.Motion!);
            Assert.Equal("7", motion.Id);
            Assert.Equal("Porch", motion.Name);
            Assert.Equal(1, motion.State);
            Assert.Equal(["human"], motion.Properties!.DetectionTypes);
            Assert.Equal("solid", motion.Properties.Style);
            Assert.Equal(0.25, motion.Vertices!.Single().Y);
            Assert.Empty(z.Package!);
        }

        [Fact]
        public void LightSettings_UnknownKeys_CapturedInAdditionalFields()
        {
            LightSettings? l = JsonSerializer.Deserialize<LightSettings>("{\"brightness\":40,\"schedule\":{\"on\":1}}");

            Assert.NotNull(l);
            Assert.Equal(40, l!.Brightness);
            Assert.NotNull(l.AdditionalFields);
            Assert.True(l.AdditionalFields!.ContainsKey("schedule"));
        }

        [Fact]
        public void Doorbot_Deserializes_SettingsZoneSettingsAndMotionSnoozeObject()
        {
            Doorbot? d = JsonSerializer.Deserialize<Doorbot>(
                "{\"id\":1,\"settings\":{\"doorbell_volume\":2},\"zone_settings\":{\"motion\":[]},\"motion_snooze\":{\"until\":5}}");

            Assert.NotNull(d);
            Assert.Equal(2, d!.Settings!.DoorbellVolume);
            Assert.NotNull(d.ZoneSettings);
            Assert.Equal("{\"until\":5}", d.MotionSnooze!.Value.GetRawText());
        }

        [Fact]
        public void StickupCam_Deserializes_SirenStatusObjectAndLedStatusObject()
        {
            StickupCam? c = JsonSerializer.Deserialize<StickupCam>(
                "{\"id\":9,\"siren_status\":{\"seconds_remaining\":30},\"led_status\":{\"seconds_remaining\":0}}");

            Assert.NotNull(c);
            Assert.Equal(30, c!.SirenStatus!.SecondsRemaining);
            Assert.Equal("{\"seconds_remaining\":0}", c.LedStatus!.Value.GetRawText());
        }

        [Fact]
        public void DeviceHealth_Deserializes_IdAndSupportedRpcCommands()
        {
            DeviceHealth? h = JsonSerializer.Deserialize<DeviceHealth>("{\"id\":77,\"supported_rpc_commands\":[\"reboot\",\"siren\"]}");

            Assert.NotNull(h);
            Assert.Equal(77, h!.Id);
            Assert.Equal(["reboot", "siren"], h.SupportedRpcCommands);
        }
    }
}
