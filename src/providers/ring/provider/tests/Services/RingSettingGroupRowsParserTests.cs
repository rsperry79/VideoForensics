using System.Text.Json;

using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Providers.Ring.Services;

using Xunit;

namespace VideoForensics.Providers.Ring.Tests
{
    /// <summary>Typed parsing of the settings keys that become the endpoint group rows: the RingDevice group tables, the misc table, and the snooze and threshold child rows.</summary>
    public class RingSettingGroupRowsParserTests
    {
        private static Dictionary<string, JsonElement> Fields(string json)
        {
            return JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)!;
        }

        [Fact]
        public void RingDeviceSettingGroupRowsParser_VideoAndMotionKeys_MapToTheirColumns()
        {
            RingSettingsGroups groups = RingDeviceSettingsParser.Parse(true, Fields("""
                {"video_settings": {"exposure_control": 2, "aspect_ratio": "fake-ratio", "hdr_enable": true,
                   "ignore_zones": {"zone1": {"name": "Zone A", "state": 1, "vertex1": {"x": 1, "y": 0}}}},
                 "motion_settings": {"enable_ir": 1, "advanced_motion_detection_enabled": true, "loitering_threshold": 3,
                   "advanced_pir_motion_zones": {"zone1_sensitivity": 7}}}
                """), null);

            RingDeviceSettingGroupRows rows = Assert.IsType<RingDeviceSettingGroupRows>(groups.Groups);
            RingDeviceVideoSettings video = rows.Video!;
            Assert.Equal(2, video.ExposureControl);
            Assert.Equal("fake-ratio", video.AspectRatio);
            Assert.True(video.HdrEnable);
            Assert.Equal("Zone A", video.IgnoreZonesZone1Name);
            Assert.Equal(1, video.IgnoreZonesZone1State);
            Assert.Equal(1, video.IgnoreZonesZone1Vertex1X);
            Assert.Equal(0, video.IgnoreZonesZone1Vertex1Y);
            RingDeviceMotionSettings motion = rows.Motion!;
            Assert.Equal(1, motion.EnableIr);
            Assert.True(motion.AdvancedMotionDetectionEnabled);
            Assert.Equal(3, motion.LoiteringThreshold);
            Assert.Equal(7, motion.AdvancedPirMotionZonesZone1Sensitivity);
            Assert.Empty(groups.Unstored);
        }

        [Fact]
        public void RingDeviceSettingGroupRowsParser_SnoozeAndThresholdArrays_BecomeChildRows()
        {
            RingSettingsGroups groups = RingDeviceSettingsParser.Parse(true, Fields("""
                {"motion_settings": {"motion_snooze_profile": [5, 15]},
                 "cv_settings": {"threshold_overrides": {"package_delivery": {"versions": [
                   {"version": "fake-v1", "values": [0.5, 0.75]}, {"version": "fake-v2", "values": [0.25]}]}}}}
                """), null);

            RingDeviceSettingGroupRows rows = groups.Groups!;
            Assert.Equal(2, rows.MotionSnoozeProfiles!.Count);
            Assert.Equal(0, rows.MotionSnoozeProfiles[0].Position);
            Assert.Equal(5, rows.MotionSnoozeProfiles[0].Value);
            Assert.Equal(15, rows.MotionSnoozeProfiles[1].Value);
            Assert.Equal(2, rows.CvThresholdVersions!.Count);
            Assert.Equal("fake-v2", rows.CvThresholdVersions[1].Version);
            Assert.Equal(1, rows.CvThresholdVersions[1].Position);
            Assert.Equal(3, rows.CvThresholdValues!.Count);
            Assert.Equal(1, rows.CvThresholdValues[2].VersionPosition);
            Assert.Equal(0, rows.CvThresholdValues[2].Position);
            Assert.Equal(0.25, rows.CvThresholdValues[2].Value);
            Assert.Empty(groups.Unstored);
        }

        [Fact]
        public void RingDeviceSettingGroupRowsParser_SettingsObjectAbsent_LeavesGroupRowsNull()
        {
            RingSettingsGroups groups = RingDeviceSettingsParser.Parse(false, null, null);

            Assert.Null(groups.Groups);
        }

        [Fact]
        public void RingDeviceSettingGroupRowsParser_EmptySettingsObject_WritesRowsOfNullsAndEmptyLists()
        {
            RingDeviceSettingGroupRows rows = RingDeviceSettingsParser.Parse(true, Fields("{}"), null).Groups!;

            Assert.NotNull(rows.Video);
            Assert.Null(rows.Video!.ExposureControl);
            Assert.NotNull(rows.Motion);
            Assert.Null(rows.Motion!.EnableIr);
            Assert.Empty(rows.MotionSnoozeProfiles!);
            Assert.Empty(rows.CvThresholdVersions!);
            Assert.Empty(rows.CvThresholdValues!);
        }

        [Fact]
        public void RingDeviceSettingGroupRowsParser_WrongTypeNewKey_NotStoredAndNotedWithKindOnly()
        {
            RingSettingsGroups groups = RingDeviceSettingsParser.Parse(true, Fields("""{"video_settings": {"exposure_control": "SECRET-TEXT-4", "hdr_enable": null}}"""), null);

            RingDeviceVideoSettings video = groups.Groups!.Video!;
            Assert.Null(video.ExposureControl);
            Assert.Null(video.HdrEnable);
            string note = Assert.Single(groups.Unstored);
            Assert.StartsWith("video_settings.exposure_control not stored", note);
            Assert.Contains("String", note);
            Assert.DoesNotContain("SECRET-TEXT-4", note);
        }

        [Fact]
        public void RingDeviceSettingGroupRowsParser_TypedKeys_RemovedFromRemainderJson()
        {
            string? remainder = RingDeviceSettingGroupParser.RemainderJson(Fields("""
                {"video_settings": {"exposure_control": 2, "unmodeled_fake_key": "fake"},
                 "motion_settings": {"motion_snooze_profile": [5], "enable_ir": 1}}
                """));

            Assert.NotNull(remainder);
            Assert.Contains("unmodeled_fake_key", remainder);
            Assert.DoesNotContain("exposure_control", remainder);
            Assert.DoesNotContain("motion_snooze_profile", remainder);
            Assert.DoesNotContain("enable_ir", remainder);
        }
    }
}
