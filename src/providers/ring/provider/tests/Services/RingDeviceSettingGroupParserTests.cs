using System.Text.Json;
using System.Text.Json.Nodes;

using VideoForensics.Core.Providers.Common.Contracts;
using VideoForensics.Providers.Ring.Services;

using Xunit;

namespace VideoForensics.Providers.Ring.Tests
{
    /// <summary>Typed parsing of the settings keys that Ring keeps in AdditionalFields (offline motion, CV detection types and thresholds).</summary>
    public class RingDeviceSettingGroupParserTests
    {
        private const string Sample = """
            {
              "motion_snooze_preset_profile": "low",
              "advanced_motion_detection_enabled": true,
              "offline_motion_event_settings": {"subscribed": true, "enabled": false, "max_upload_kb": 5000, "resolution_p": 360, "frequency_after_secs": 2, "period_after_secs": 30},
              "cv_settings": {
                "detection_types": {
                  "human": {"enabled": true, "mode": "cloud", "record": false, "notification": true},
                  "loitering": {"enabled": false, "mode": "none", "record": false, "notification": false}
                },
                "threshold": {"loitering": 10.0, "package_delivery": 3.0, "triggers": [], "search_types": {"natural_language_search": {"enabled": false, "mode": "none"}}},
                "concierge_settings": {"mode": "disabled"}
              }
            }
            """;

        private static Dictionary<string, JsonElement> Fields(string json)
        {
            return JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)!;
        }

        [Fact]
        public void RingDeviceSettingGroupParser_OfflineMotion_MapsTypedColumns()
        {
            TypedSettingDetails groups = RingDeviceSettingGroupParser.Parse(Fields(Sample));
            OfflineMotionDetail offline = Assert.IsType<OfflineMotionDetail>(groups.OfflineMotion);
            Assert.Equal(true, offline.Subscribed);
            Assert.Equal(false, offline.Enabled);
            Assert.Equal(5000, offline.MaxUploadKb);
            Assert.Equal(360, offline.ResolutionP);
            Assert.Equal(2, offline.FrequencyAfterSecs);
            Assert.Equal(30, offline.PeriodAfterSecs);
        }

        [Fact]
        public void RingDeviceSettingGroupParser_DetectionTypes_OneTypedRowPerType()
        {
            TypedSettingDetails groups = RingDeviceSettingGroupParser.Parse(Fields(Sample));
            Assert.Equal(2, groups.DetectionTypes.Count);
            DetectionTypeDetail human = groups.DetectionTypes.Single(t => t.DetectionType == "human");
            Assert.Equal(true, human.Enabled);
            Assert.Equal("cloud", human.Mode);
            Assert.Equal(false, human.Record);
            Assert.Equal(true, human.Notification);
        }

        [Fact]
        public void RingDeviceSettingGroupParser_Thresholds_OnlyNumericEntries()
        {
            TypedSettingDetails groups = RingDeviceSettingGroupParser.Parse(Fields(Sample));
            Assert.Equal(2, groups.Thresholds.Count);
            Assert.Equal(10.0, groups.Thresholds.Single(t => t.DetectionType == "loitering").Threshold);
            Assert.Equal(3.0, groups.Thresholds.Single(t => t.DetectionType == "package_delivery").Threshold);
        }

        [Fact]
        public void RingDeviceSettingGroupParser_RemainderJson_RemovesTypedKeysAndKeepsUnmodeledOnes()
        {
            string? remainder = RingDeviceSettingGroupParser.RemainderJson(Fields(Sample));

            Assert.NotNull(remainder);
            JsonObject root = JsonNode.Parse(remainder!)!.AsObject();
            Assert.False(root.ContainsKey("offline_motion_event_settings"));
            Assert.False(root.ContainsKey("motion_snooze_preset_profile"));
            Assert.False(root.ContainsKey("advanced_motion_detection_enabled"));
            JsonObject cv = root["cv_settings"]!.AsObject();
            Assert.False(cv.ContainsKey("detection_types"));
            Assert.True(cv.ContainsKey("concierge_settings"));
            JsonObject threshold = cv["threshold"]!.AsObject();
            Assert.False(threshold.ContainsKey("loitering"));
            Assert.True(threshold.ContainsKey("triggers"));
            Assert.True(threshold.ContainsKey("search_types"));
        }

        [Fact]
        public void RingDeviceSettingGroupParser_NoTypedKeys_ReturnsEmptyGroupsAndOriginalRemainder()
        {
            Dictionary<string, JsonElement> fields = Fields("{\"unmodeled_fake_key\":\"low\"}");

            TypedSettingDetails groups = RingDeviceSettingGroupParser.Parse(fields);

            Assert.Null(groups.OfflineMotion);
            Assert.Empty(groups.DetectionTypes);
            Assert.Empty(groups.Thresholds);
            Assert.Equal("{\"unmodeled_fake_key\":\"low\"}", RingDeviceSettingGroupParser.RemainderJson(fields)!.Replace(" ", string.Empty));
        }

        [Fact]
        public void RingDeviceSettingGroupParser_NullFields_ReturnsEmptyGroupsAndNullRemainder()
        {
            TypedSettingDetails groups = RingDeviceSettingGroupParser.Parse(null);

            Assert.Null(groups.OfflineMotion);
            Assert.Empty(groups.DetectionTypes);
            Assert.Null(RingDeviceSettingGroupParser.RemainderJson(null));
        }

        [Fact]
        public void RingDeviceSettingGroupParser_RemainderJson_ExcludesPaidFeatureAndNetworkGroups()
        {
            Dictionary<string, JsonElement> fields = Fields("""
                {"unmodeled_fake_key": "low",
                 "cv_paid_features": {"baby_cry": true},
                 "other_paid_features": {"ding_call": false},
                 "network_settings": {"multi_net_pref": 1, "network_diagnosis": {"tcp_port": 5201}}}
                """);

            string? remainder = RingDeviceSettingGroupParser.RemainderJson(fields);

            Assert.NotNull(remainder);
            JsonObject root = JsonNode.Parse(remainder!)!.AsObject();
            Assert.True(root.ContainsKey("unmodeled_fake_key"));
            Assert.False(root.ContainsKey("cv_paid_features"));
            Assert.False(root.ContainsKey("other_paid_features"));
            Assert.False(root.ContainsKey("network_settings"));
            Assert.Null(RingDeviceSettingGroupParser.RemainderJson(Fields("""{"network_settings": {"multi_net_pref": 1}}""")));
        }
    }
}
