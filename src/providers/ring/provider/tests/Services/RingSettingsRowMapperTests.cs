using System.Text.Json;

using VideoForensics.Core.Providers.Common.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Providers.Ring.Entities;
using VideoForensics.Providers.Ring.Services;

using Xunit;

using RingDeviceDetails = VideoForensics.Providers.Ring.Services.RingDeviceDetails;

namespace VideoForensics.Providers.Ring.Tests
{
    /// <summary>Maps the RingDeviceSettings, DeviceGeneralSettings, created_at and alerts.ota_status values (sanitized fixture) onto rows.</summary>
    public class RingSettingsRowMapperTests
    {
        private static readonly Guid DeviceId = Guid.Parse("22222222-3333-4444-5555-666666666666");

        private static RingDeviceDetails Load(string providerId)
        {
            string path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "device-settings-rows-sanitized.json");
            Devices devices = JsonSerializer.Deserialize<Devices>(File.ReadAllText(path))!;
            return RingDeviceDetailsMapper.Find(devices, providerId)!;
        }

        [Fact]
        public void ToSettingsChildren_Doorbot_WritesRingSettingsAndGeneralRows()
        {
            DeviceSettingsChildren children = RingDeviceDetailsMapper.ToSettingsChildrenWithFeatures(Load("7001"), DeviceId)!;

            RingDeviceSettings ring = children.RingSettings!;
            Assert.Equal(DeviceId, ring.DeviceId);
            Assert.NotEqual(Guid.Empty, ring.Id);
            Assert.True(ring.StarkEnabled);
            Assert.False(ring.StarkEnrolled);
            Assert.Equal("autoreply", ring.TermsOfServiceAccepted);
            Assert.Equal(336, ring.Lite24x7FootageTtl);
            Assert.False(ring.Lite24x7Enabled);
            Assert.True(ring.Lite24x7Subscribed);
            Assert.Equal(360, ring.Lite24x7ResolutionP);
            Assert.Equal("rms-fake.example.invalid", ring.RingMediaServerHost);
            Assert.Equal("fake-mode", ring.EncryptionEligibilityMode);
            Assert.True(ring.NaturalLanguageSearchEnabled);
            Assert.Equal(10000, ring.ConciergeAlexaDelayMs);
            Assert.Equal(5, ring.PirZone1Sensitivity);
            Assert.Equal(0, ring.PirZone6Sensitivity);
            Assert.True(ring.EnableLiveViewAudioOverride);
            Assert.Equal("fake-flick", ring.FlickElimRecommendedMode);
            Assert.Equal(new DateTime(2021, 8, 29, 23, 7, 38, DateTimeKind.Utc), ring.RingCreatedAt);

            DeviceDetailFields doorbotFields = Load("7001").DeviceFields!;
            Assert.True(doorbotFields.EnableVod);
            Assert.Equal(2, doorbotFields.ExposureControl);
            Assert.False(doorbotFields.VodSuspended);
            Assert.Equal(10, doorbotFields.PirSensitivity1);
            Assert.Equal("middle", doorbotFields.LiveViewPresetProfile);
            Assert.Equal("low", doorbotFields.MotionSnoozePresetProfile);

            DeviceGeneralSettings general = children.GeneralSettings!;
            Assert.Equal(DeviceId, general.DeviceId);
            Assert.Equal(11, general.VoiceVolume);
            Assert.True(general.MotionAnnouncement);
            Assert.Equal(2, general.ExtendedLiveView);
            Assert.Equal(7, general.FloodlightBrightness);
        }

        [Fact]
        public void ToSettingsChildren_StickupCam_RingSettingsRowKeepsOtherScalars()
        {
            RingDeviceDetails details = Load("7002");
            DeviceSettingsChildren children = RingDeviceDetailsMapper.ToSettingsChildrenWithFeatures(details, DeviceId)!;

            RingDeviceSettings ring = children.RingSettings!;
            Assert.Equal(new DateTime(2024, 10, 8, 22, 43, 15, DateTimeKind.Utc), ring.RingCreatedAt);
            Assert.Equal(5, ring.PirZone1Sensitivity);
            Assert.True(ring.Lite24x7Enabled);

            Assert.Equal(11, children.GeneralSettings!.VoiceVolume);
            Assert.Equal(10, children.GeneralSettings.FloodlightBrightness);
        }

        [Fact]
        public void ToSettingsChildren_StickupCam_StoredDeviceFieldsStillMapToDevicesColumns()
        {
            DeviceDetailFields fields = Load("7002").DeviceFields!;

            Assert.True(fields.EnableVod);
            Assert.True(fields.VodSuspended);
            Assert.Equal("none", fields.MotionSnoozePresetProfile);
            Assert.Equal("middle", fields.LiveViewPresetProfile);
        }

        [Fact]
        public void ToSettingsChildren_Chime_WritesGeneralRowAndCreatedAtOnly()
        {
            DeviceSettingsChildren children = RingDeviceDetailsMapper.ToSettingsChildrenWithFeatures(Load("7003"), DeviceId)!;

            Assert.False(children.GeneralSettings!.StatusLedEnabled);
            Assert.Null(children.GeneralSettings.VoiceVolume);
            Assert.Equal(new DateTime(2021, 11, 24, 0, 54, 31, DateTimeKind.Utc), children.RingSettings!.RingCreatedAt);
            Assert.Null(children.RingSettings.StarkEnabled);
        }

        [Fact]
        public void ToSettingsChildren_SettingsKeysAreRemovedFromRemainderJson()
        {
            DeviceDetailFields fields = Load("7001").DeviceFields!;

            Assert.NotNull(fields.SettingsMetadataJson);
            Assert.Contains("unmodeled_fake_key", fields.SettingsMetadataJson);
            Assert.DoesNotContain("stark_enabled", fields.SettingsMetadataJson);
            Assert.DoesNotContain("lite_24x7", fields.SettingsMetadataJson);
            Assert.DoesNotContain("voice_volume", fields.SettingsMetadataJson);
            Assert.DoesNotContain("enable_vod", fields.SettingsMetadataJson);
            Assert.DoesNotContain("live_view_preset_profile", fields.SettingsMetadataJson);
        }

        [Fact]
        public void ToSettingsChildren_Doorbot_WritesEndpointGroupRowsWithDeviceId()
        {
            DeviceSettingsChildren children = RingDeviceDetailsMapper.ToSettingsChildrenWithFeatures(Load("7001"), DeviceId)!;

            RingDeviceSettingGroupRows groups = children.RingSettingGroups!;
            Assert.Equal(DeviceId, groups.Video!.DeviceId);
            Assert.NotEqual(Guid.Empty, groups.Video.Id);
            Assert.Equal(3, groups.Video.ExposureControl);
            Assert.Equal("fake-ratio", groups.Video.AspectRatio);
            Assert.Equal(1, groups.Motion!.EnableIr);
            Assert.Equal(2, groups.MotionSnoozeProfiles!.Count);
            Assert.All(groups.MotionSnoozeProfiles, p => Assert.Equal(DeviceId, p.DeviceId));
            Assert.Equal("fake-v1", Assert.Single(groups.CvThresholdVersions!).Version);
            Assert.Equal(0.5, Assert.Single(groups.CvThresholdValues!).Value);
        }

        [Fact]
        public void ToSettingsChildren_Doorbot_EndpointGroupKeysLeaveTheRemainderJson()
        {
            string? remainder = Load("7001").DeviceFields!.SettingsMetadataJson;

            Assert.NotNull(remainder);
            Assert.Contains("unmodeled_fake_key", remainder);
            Assert.DoesNotContain("motion_snooze_profile", remainder);
            Assert.DoesNotContain("threshold_overrides", remainder);
            Assert.DoesNotContain("aspect_ratio", remainder);
        }

        [Fact]
        public void ToAlerts_DoorbotWithOtaStatus_AddsOtaStatusRowBesideConnection()
        {
            IReadOnlyList<DeviceAlerts> alerts = RingDeviceDetailsMapper.ToAlerts(Load("7001"), DeviceId);

            Assert.Equal(2, alerts.Count);
            DeviceAlerts ota = Assert.Single(alerts, a => a.AlertType == "ota_status");
            Assert.Equal("successful", ota.AlertValue);
            Assert.Null(ota.IsEnabled);
            Assert.Equal(DeviceId, ota.DeviceId);
            Assert.Equal("online", Assert.Single(alerts, a => a.AlertType == "connection").AlertValue);
        }

        [Fact]
        public void ToAlerts_StickupCamWithoutOtaStatus_HasOnlyConnectionRow()
        {
            IReadOnlyList<DeviceAlerts> alerts = RingDeviceDetailsMapper.ToAlerts(Load("7002"), DeviceId);

            DeviceAlerts connection = Assert.Single(alerts);
            Assert.Equal("connection", connection.AlertType);
        }

        [Fact]
        public void ToAlerts_ChimeWithOtaStatus_AddsOtaStatusRow()
        {
            IReadOnlyList<DeviceAlerts> alerts = RingDeviceDetailsMapper.ToAlerts(Load("7003"), DeviceId);

            Assert.Equal("successful", Assert.Single(alerts, a => a.AlertType == "ota_status").AlertValue);
        }
    }
}
