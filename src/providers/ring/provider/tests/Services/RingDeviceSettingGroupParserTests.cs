using System.Text.Json;
using System.Text.Json.Nodes;

using VideoForensics.Core.Providers.Common.Contracts;
using VideoForensics.Providers.Ring.Services;

using Xunit;

namespace VideoForensics.Providers.Ring.Tests.Services
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
        public void RingDeviceSettingGroupParser_RemainderJson_RemovesOnlyTypedKeysInTheFourGroups()
        {
            Dictionary<string, JsonElement> fields = Fields("""
                {"offline_motion_event_settings": {"enabled": true, "invented_offline_key": "x"},
                 "cv_paid_features": {"baby_cry": true, "invented_cv_key": 1},
                 "other_paid_features": {"ding_call": false, "invented_other_key": [1]},
                 "network_settings": {"multi_net_pref": 1, "invented_network_key": "y",
                   "network_diagnosis": {"tcp_port": 5201, "invented_diag_key": true}}}
                """);

            JsonObject root = JsonNode.Parse(RingDeviceSettingGroupParser.RemainderJson(fields)!)!.AsObject();

            JsonObject offline = root["offline_motion_event_settings"]!.AsObject();
            Assert.False(offline.ContainsKey("enabled"));
            Assert.True(offline.ContainsKey("invented_offline_key"));
            JsonObject cv = root["cv_paid_features"]!.AsObject();
            Assert.False(cv.ContainsKey("baby_cry"));
            Assert.True(cv.ContainsKey("invented_cv_key"));
            JsonObject other = root["other_paid_features"]!.AsObject();
            Assert.False(other.ContainsKey("ding_call"));
            Assert.True(other.ContainsKey("invented_other_key"));
            JsonObject network = root["network_settings"]!.AsObject();
            Assert.False(network.ContainsKey("multi_net_pref"));
            Assert.True(network.ContainsKey("invented_network_key"));
            JsonObject diagnosis = network["network_diagnosis"]!.AsObject();
            Assert.False(diagnosis.ContainsKey("tcp_port"));
            Assert.True(diagnosis.ContainsKey("invented_diag_key"));
        }

        [Fact]
        public void RingDeviceSettingGroupParser_RemainderJson_GroupsWithOnlyTypedKeysAreRemoved()
        {
            Assert.Null(RingDeviceSettingGroupParser.RemainderJson(Fields("""
                {"cv_paid_features": {"baby_cry": true}, "other_paid_features": {"ding_call": false},
                 "network_settings": {"multi_net_pref": 1, "network_diagnosis": {"tcp_port": 5201}}}
                """)));
        }

        [Fact]
        public void RingDeviceSettingGroupParser_RemainderJson_EveryKeyTheParsersStoreLeavesNoRemainder()
        {
            // Every key the four parsers store, with a value of the right type: no note and nothing left in the remainder.
            Dictionary<string, JsonElement> fields = Fields("""
                {"offline_motion_event_settings": {"subscribed": true, "enabled": true, "max_upload_kb": 1, "resolution_p": 1, "frequency_after_secs": 1, "period_after_secs": 1},
                 "cv_paid_features": {"baby_cry": true, "car_alarm": true, "co2_smoke_alarm": true, "dog_bark": true, "general_sound": true, "glass_break": true,
                   "human": true, "loitering": true, "motion": true, "other_motion": true, "package_delivery": true, "package_pickup": true, "cv_triggers": true, "vehicle": true, "animal": true},
                 "other_paid_features": {"alexa_concierge": true, "sheila_cv": true, "sheila_recording": true, "critical_alerts": true, "system_level_pip": true,
                   "extended_live_view": true, "snapshot_capture_plus": true, "live_speak": true, "closed_caption": true, "daily_digest": true, "ding_call": true,
                   "natural_language_search": true, "multicam_live_view": true, "package_protection": true, "alarm_triggered_recording": true, "recording_24x7": true},
                 "network_settings": {"mac_address_ble": "a", "mac_address_wifi_24": "a", "mac_address_wifi_5": "a", "max_dynamic_listen_interval": "a",
                   "multi_net_pref": 1, "data_restriction_enabled": true,
                   "network_diagnosis": {"channel_analysis": true, "periodic_diagnosis": true, "lan_performance": true, "tcp_iperf": true, "udp_iperf": true,
                     "tcp_host": "a", "udp_host": "a", "tcp_port": 1, "udp_port": 1, "udp_bandwidth": 1}}}
                """);

            Assert.Empty(RingDeviceNetworkParser.Parse(fields).Unstored);
            Assert.Null(RingDeviceSettingGroupParser.RemainderJson(fields));
        }

        [Fact]
        public void RingDeviceSettingGroupParser_RemainderJson_NonObjectGroupsStayInTheRemainder()
        {
            // The parsers store nothing from a group that is not an object, so its value is kept rather than dropped.
            string? remainder = RingDeviceSettingGroupParser.RemainderJson(Fields("""{"offline_motion_event_settings": "on", "cv_paid_features": 3}"""));

            Assert.NotNull(remainder);
            JsonObject root = JsonNode.Parse(remainder!)!.AsObject();
            Assert.Equal("on", root["offline_motion_event_settings"]!.GetValue<string>());
            Assert.Equal(3, root["cv_paid_features"]!.GetValue<int>());
        }
    }
}
