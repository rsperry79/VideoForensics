using System.Text.Json;

using VideoForensics.Data.Common.Entities;
using VideoForensics.Providers.Ring.Services;

using Xunit;

namespace VideoForensics.Providers.Ring.Tests
{
    /// <summary>Typed parsing of the settings keys that become RingDeviceSettings and DeviceGeneralSettings rows.</summary>
    public class RingSettingsRowParserTests
    {
        private static Dictionary<string, JsonElement> Fields(string json)
        {
            return JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)!;
        }

        [Fact]
        public void RingDeviceSettingsParser_TopLevelAndGroupKeys_MapEachKeyToItsColumn()
        {
            RingSettingsGroups groups = RingDeviceSettingsParser.Parse(true, Fields("""
                {"stark_enabled": true, "stark_enrolled": false, "enable_rich_notifications": true,
                 "rich_notifications_billing_eligible": true, "rich_notifications_face_crop_enabled": false,
                 "rich_notifications_scene_source": "cloud", "people_detection_eligible": true,
                 "terms_of_service_accepted": "autoreply", "motion_sensitivity_profile": 2, "lite_24x7_footage_ttl": 336,
                 "lite_24x7": {"enabled": true, "frequency_secs": 180, "resolution_p": 360, "subscribed": false},
                 "server_settings": {"ring_media_server_enabled": true, "ring_media_server_host": "rms-fake.example.invalid"},
                 "sheila_settings": {"cv_processing_enabled": false, "local_storage_enabled": true},
                 "video_settings": {"encryption_enabled": true, "encryption_method": 3, "encryption_eligibility_mode": "eligible", "recording_24x7_mode": 2},
                 "cv_settings": {"search_types": {"natural_language_search": {"enabled": true, "mode": "cloud"}}},
                 "concierge_settings": {"mode": "autoreply", "alexa_settings": {"delay_ms": 1000}, "autoreply_settings": {"delay_ms": 2000}},
                 "audio_settings": {"enable_live_view_audio_override": false},
                 "active_streaming_event_led_enabled": true, "flick_elim_recommended_mode": "fake-mode"}
                """), "2024-10-08T22:43:15Z");

            RingDeviceSettings r = Assert.IsType<RingDeviceSettings>(groups.Ring);
            Assert.True(r.StarkEnabled);
            Assert.False(r.StarkEnrolled);
            Assert.True(r.EnableRichNotifications);
            Assert.True(r.RichNotificationsBillingEligible);
            Assert.False(r.RichNotificationsFaceCropEnabled);
            Assert.Equal("cloud", r.RichNotificationsSceneSource);
            Assert.True(r.PeopleDetectionEligible);
            Assert.Equal("autoreply", r.TermsOfServiceAccepted);
            Assert.Equal(2, r.MotionSensitivityProfile);
            Assert.Equal(336, r.Lite24x7FootageTtl);
            Assert.True(r.Lite24x7Enabled);
            Assert.Equal(180, r.Lite24x7FrequencySecs);
            Assert.Equal(360, r.Lite24x7ResolutionP);
            Assert.False(r.Lite24x7Subscribed);
            Assert.True(r.RingMediaServerEnabled);
            Assert.Equal("rms-fake.example.invalid", r.RingMediaServerHost);
            Assert.False(r.SheilaCvProcessingEnabled);
            Assert.True(r.SheilaLocalStorageEnabled);
            Assert.True(r.EncryptionEnabled);
            Assert.Equal(3, r.EncryptionMethod);
            Assert.Equal("eligible", r.EncryptionEligibilityMode);
            Assert.Equal(2, r.Recording24x7Mode);
            Assert.True(r.NaturalLanguageSearchEnabled);
            Assert.Equal("cloud", r.NaturalLanguageSearchMode);
            Assert.Equal("autoreply", r.ConciergeMode);
            Assert.Equal(1000, r.ConciergeAlexaDelayMs);
            Assert.Equal(2000, r.ConciergeAutoreplyDelayMs);
            Assert.False(r.EnableLiveViewAudioOverride);
            Assert.True(r.ActiveStreamingEventLedEnabled);
            Assert.Equal("fake-mode", r.FlickElimRecommendedMode);
            Assert.Empty(groups.Unstored);
        }

        [Fact]
        public void RingDeviceSettingsParser_ZoneSensitivities_MapToSixColumns()
        {
            RingSettingsGroups groups = RingDeviceSettingsParser.Parse(true, Fields("""
                {"advanced_pir_motion_zones": {"zone1_sensitivity": 5, "zone2_sensitivity": 4, "zone3_sensitivity": 3,
                 "zone4_sensitivity": 2, "zone5_sensitivity": 1, "zone6_sensitivity": 0}}
                """), null);

            RingDeviceSettings r = Assert.IsType<RingDeviceSettings>(groups.Ring);
            Assert.Equal(5, r.PirZone1Sensitivity);
            Assert.Equal(4, r.PirZone2Sensitivity);
            Assert.Equal(3, r.PirZone3Sensitivity);
            Assert.Equal(2, r.PirZone4Sensitivity);
            Assert.Equal(1, r.PirZone5Sensitivity);
            Assert.Equal(0, r.PirZone6Sensitivity);
        }

        [Fact]
        public void RingDeviceSettingsParser_DeviceKeysAlsoTypedOnDevices_NotStoredOnRingRowOrRemainder()
        {
            // The nine keys are stored on Devices, so the Ring row does not carry them and the remainder JSON does not repeat them.
            const string json = """
                {"enable_vod": 1, "exposure_control": 2, "vod_suspended": 0, "pir_sensitivity_1": 10,
                 "enable_audio_recording": true, "advanced_motion_detection_enabled": true,
                 "advanced_motion_detection_human_only_mode": false, "live_view_preset_profile": "middle",
                 "motion_snooze_preset_profile": "low", "motion_settings": {"motion_snooze_preset_profile": "high"},
                 "stark_enabled": true}
                """;
            Dictionary<string, JsonElement> fields = Fields(json);

            RingSettingsGroups groups = RingDeviceSettingsParser.Parse(true, fields, null);

            RingDeviceSettings r = Assert.IsType<RingDeviceSettings>(groups.Ring);
            Assert.True(r.StarkEnabled);
            Assert.Empty(groups.Unstored);
            string? remainder = RingDeviceSettingGroupParser.RemainderJson(fields);
            Assert.DoesNotContain("enable_vod", remainder ?? string.Empty);
            Assert.DoesNotContain("motion_snooze_preset_profile", remainder ?? string.Empty);
            Assert.DoesNotContain("live_view_preset_profile", remainder ?? string.Empty);
        }

        [Fact]
        public void RingDeviceSettingsParser_GeneralKeys_MapToProviderAgnosticRow()
        {
            RingSettingsGroups groups = RingDeviceSettingsParser.Parse(true, Fields("""
                {"voice_volume": 11, "live_view_disabled": false, "motion_announcement": true, "loitering_threshold": 10,
                 "user_specified_recording_ttl": 180, "extended_live_view": 2, "status_led_enable": false,
                 "floodlight_settings": {"brightness": 7}}
                """), null);

            DeviceGeneralSettings g = Assert.IsType<DeviceGeneralSettings>(groups.General);
            Assert.Equal(11, g.VoiceVolume);
            Assert.False(g.LiveViewDisabled);
            Assert.True(g.MotionAnnouncement);
            Assert.Equal(10, g.LoiteringThreshold);
            Assert.Equal(180, g.UserSpecifiedRecordingTtl);
            Assert.Equal(2, g.ExtendedLiveView);
            Assert.False(g.StatusLedEnabled);
            Assert.Equal(7, g.FloodlightBrightness);
        }

        [Fact]
        public void RingDeviceSettingsParser_SettingsObjectAbsent_LeavesBothRowsNull()
        {
            RingSettingsGroups groups = RingDeviceSettingsParser.Parse(false, null, "2024-10-08T22:43:15Z");

            Assert.Null(groups.Ring);
            Assert.Null(groups.General);
            Assert.Empty(groups.Unstored);
        }

        [Fact]
        public void RingDeviceSettingsParser_EmptySettingsObject_WritesRowsOfNulls()
        {
            RingSettingsGroups groups = RingDeviceSettingsParser.Parse(true, Fields("{}"), null);

            Assert.NotNull(groups.Ring);
            Assert.Null(groups.Ring!.StarkEnabled);
            Assert.Null(groups.Ring.RingCreatedAt);
            Assert.NotNull(groups.General);
            Assert.Null(groups.General!.VoiceVolume);
            Assert.Empty(groups.Unstored);
        }

        [Fact]
        public void RingDeviceSettingsParser_CreatedAtIsoText_StoredAsUtc()
        {
            RingSettingsGroups groups = RingDeviceSettingsParser.Parse(true, Fields("{}"), "2024-10-08T22:43:15Z");

            Assert.Equal(new DateTime(2024, 10, 8, 22, 43, 15, DateTimeKind.Utc), groups.Ring!.RingCreatedAt);
            Assert.Empty(groups.Unstored);
        }

        [Fact]
        public void RingDeviceSettingsParser_CreatedAtNotADate_NotStoredAndNotedWithoutValue()
        {
            RingSettingsGroups groups = RingDeviceSettingsParser.Parse(true, Fields("{}"), "SECRET-NOT-A-DATE");

            Assert.Null(groups.Ring!.RingCreatedAt);
            string note = Assert.Single(groups.Unstored);
            Assert.StartsWith("created_at not stored", note);
            Assert.DoesNotContain("SECRET-NOT-A-DATE", note);
        }

        [Fact]
        public void RingDeviceSettingsParser_WrongTypeTypedValue_NotStoredAndNotedWithKindOnly()
        {
            RingSettingsGroups groups = RingDeviceSettingsParser.Parse(true, Fields("""{"stark_enabled": "SECRET-TEXT-1", "voice_volume": "SECRET-TEXT-2"}"""), null);

            Assert.Null(groups.Ring!.StarkEnabled);
            Assert.Null(groups.General!.VoiceVolume);
            Assert.Contains(groups.Unstored, n => n.StartsWith("stark_enabled not stored") && n.Contains("String"));
            Assert.Contains(groups.Unstored, n => n.StartsWith("voice_volume not stored") && n.Contains("String"));
            Assert.DoesNotContain(groups.Unstored, n => n.Contains("SECRET-TEXT"));
        }

        [Fact]
        public void RingDeviceSettingsParser_TypedNull_ProducesNoNote()
        {
            RingSettingsGroups groups = RingDeviceSettingsParser.Parse(true, Fields("""{"terms_of_service_accepted": null, "lite_24x7": null}"""), null);

            Assert.Null(groups.Ring!.TermsOfServiceAccepted);
            Assert.Null(groups.Ring.Lite24x7Enabled);
            Assert.Empty(groups.Unstored);
        }

        [Fact]
        public void RingDeviceSettingsParser_GroupNotAnObject_NotStoredAndNoted()
        {
            RingSettingsGroups groups = RingDeviceSettingsParser.Parse(true, Fields("""{"lite_24x7": "SECRET-TEXT-3"}"""), null);

            Assert.Null(groups.Ring!.Lite24x7Enabled);
            string note = Assert.Single(groups.Unstored);
            Assert.StartsWith("lite_24x7 not stored", note);
            Assert.DoesNotContain("SECRET-TEXT-3", note);
        }
    }
}
