using System.Text.Json;

using VideoForensics.Data.Common.Entities;
using VideoForensics.Providers.Ring.Services;

using Xunit;

namespace VideoForensics.Providers.Ring.Tests
{
    /// <summary>Typed parsing of the cv_paid_features, other_paid_features and network_settings groups of a device settings object.</summary>
    public class RingDeviceNetworkParserTests
    {
        private static Dictionary<string, JsonElement> Fields(string json)
        {
            return JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)!;
        }

        [Fact]
        public void RingDeviceNetworkParser_CvPaidFeatures_MapsEveryFlagToItsColumn()
        {
            RingNetworkGroups groups = RingDeviceNetworkParser.Parse(Fields("""
                {"cv_paid_features": {"baby_cry": true, "car_alarm": false, "co2_smoke_alarm": true, "dog_bark": false, "general_sound": true,
                "glass_break": false, "human": true, "loitering": false, "motion": true, "other_motion": false, "package_delivery": true,
                "package_pickup": false, "cv_triggers": true, "vehicle": false, "animal": true}}
                """));

            RingDevicePaidFeatures p = Assert.IsType<RingDevicePaidFeatures>(groups.PaidFeatures);
            Assert.True(p.CvPaidBabyCry);
            Assert.False(p.CvPaidCarAlarm);
            Assert.True(p.CvPaidCo2SmokeAlarm);
            Assert.False(p.CvPaidDogBark);
            Assert.True(p.CvPaidGeneralSound);
            Assert.False(p.CvPaidGlassBreak);
            Assert.True(p.CvPaidHuman);
            Assert.False(p.CvPaidLoitering);
            Assert.True(p.CvPaidMotion);
            Assert.False(p.CvPaidOtherMotion);
            Assert.True(p.CvPaidPackageDelivery);
            Assert.False(p.CvPaidPackagePickup);
            Assert.True(p.CvPaidCvTriggers);
            Assert.False(p.CvPaidVehicle);
            Assert.True(p.CvPaidAnimal);
            Assert.Null(p.OtherPaidDingCall);
            Assert.Empty(groups.Unstored);
        }

        [Fact]
        public void RingDeviceNetworkParser_OtherPaidFeatures_MapsEveryFlagToItsColumn()
        {
            RingNetworkGroups groups = RingDeviceNetworkParser.Parse(Fields("""
                {"other_paid_features": {"alexa_concierge": true, "sheila_cv": false, "sheila_recording": true, "critical_alerts": false,
                "system_level_pip": true, "extended_live_view": false, "snapshot_capture_plus": true, "live_speak": false,
                "closed_caption": true, "daily_digest": false, "ding_call": true, "natural_language_search": false,
                "multicam_live_view": true, "package_protection": false, "alarm_triggered_recording": true, "recording_24x7": false}}
                """));

            RingDevicePaidFeatures p = Assert.IsType<RingDevicePaidFeatures>(groups.PaidFeatures);
            Assert.True(p.OtherPaidAlexaConcierge);
            Assert.False(p.OtherPaidSheilaCv);
            Assert.True(p.OtherPaidSheilaRecording);
            Assert.False(p.OtherPaidCriticalAlerts);
            Assert.True(p.OtherPaidSystemLevelPip);
            Assert.False(p.OtherPaidExtendedLiveView);
            Assert.True(p.OtherPaidSnapshotCapturePlus);
            Assert.False(p.OtherPaidLiveSpeak);
            Assert.True(p.OtherPaidClosedCaption);
            Assert.False(p.OtherPaidDailyDigest);
            Assert.True(p.OtherPaidDingCall);
            Assert.False(p.OtherPaidNaturalLanguageSearch);
            Assert.True(p.OtherPaidMulticamLiveView);
            Assert.False(p.OtherPaidPackageProtection);
            Assert.True(p.OtherPaidAlarmTriggeredRecording);
            Assert.False(p.OtherPaidRecording24x7);
            Assert.Null(p.CvPaidBabyCry);
        }

        [Fact]
        public void RingDeviceNetworkParser_PaidFeatureWrongType_NotStoredAndNotedWithoutTheValue()
        {
            RingNetworkGroups groups = RingDeviceNetworkParser.Parse(Fields("""{"cv_paid_features": {"baby_cry": "SECRET-TEXT-1"}}"""));

            Assert.Null(Assert.IsType<RingDevicePaidFeatures>(groups.PaidFeatures).CvPaidBabyCry);
            string note = Assert.Single(groups.Unstored);
            Assert.StartsWith("cv_paid_features.baby_cry not stored", note);
            Assert.EndsWith("(String)", note);
            Assert.DoesNotContain("SECRET-TEXT-1", note);
        }

        [Fact]
        public void RingDeviceNetworkParser_PaidFeatureNull_StoredAsNullWithoutANote()
        {
            RingNetworkGroups groups = RingDeviceNetworkParser.Parse(Fields("""{"other_paid_features": {"ding_call": null}}"""));

            Assert.Null(Assert.IsType<RingDevicePaidFeatures>(groups.PaidFeatures).OtherPaidDingCall);
            Assert.Empty(groups.Unstored);
        }

        [Fact]
        public void RingDeviceNetworkParser_UnknownPaidKey_IsNoted()
        {
            RingNetworkGroups groups = RingDeviceNetworkParser.Parse(Fields("""{"cv_paid_features": {"future_cv_flag": true}}"""));

            string note = Assert.Single(groups.Unstored);
            Assert.StartsWith("cv_paid_features.future_cv_flag not stored", note);
        }

        [Fact]
        public void RingDeviceNetworkParser_EmptyPaidObject_YieldsRowOfNulls()
        {
            RingNetworkGroups groups = RingDeviceNetworkParser.Parse(Fields("""{"cv_paid_features": {}}"""));

            RingDevicePaidFeatures p = Assert.IsType<RingDevicePaidFeatures>(groups.PaidFeatures);
            Assert.Null(p.CvPaidBabyCry);
            Assert.Null(p.OtherPaidRecording24x7);
            Assert.Empty(groups.Unstored);
        }

        [Fact]
        public void RingDeviceNetworkParser_PaidGroupNotAnObject_IsNotedAndYieldsNoRow()
        {
            RingNetworkGroups groups = RingDeviceNetworkParser.Parse(Fields("""{"other_paid_features": "on"}"""));

            Assert.Null(groups.PaidFeatures);
            string note = Assert.Single(groups.Unstored);
            Assert.Equal("other_paid_features not stored: expected an object (String)", note);
        }

        [Fact]
        public void RingDeviceNetworkParser_NoGroups_ReturnsNullRowsAndNoNotes()
        {
            RingNetworkGroups groups = RingDeviceNetworkParser.Parse(Fields("""{"motion_snooze_preset_profile": "low"}"""));

            Assert.Null(groups.PaidFeatures);
            Assert.Null(groups.NetworkSettings);
            Assert.Null(groups.NetworkDiagnosis);
            Assert.Empty(groups.Unstored);
        }

        [Fact]
        public void RingDeviceNetworkParser_NullFields_ReturnsNullRowsAndNoNotes()
        {
            RingNetworkGroups groups = RingDeviceNetworkParser.Parse(null);

            Assert.Null(groups.PaidFeatures);
            Assert.Null(groups.NetworkSettings);
            Assert.Null(groups.NetworkDiagnosis);
            Assert.Empty(groups.Unstored);
        }

        [Fact]
        public void RingDeviceNetworkParser_NetworkSettings_MapsMacAddressesAndScalars()
        {
            RingNetworkGroups groups = RingDeviceNetworkParser.Parse(Fields("""
                {"network_settings": {"mac_address_ble": "02:00:00:00:00:01", "mac_address_wifi_24": "02:00:00:00:00:02",
                "mac_address_wifi_5": "02:00:00:00:00:03", "max_dynamic_listen_interval": "30", "multi_net_pref": 2, "data_restriction_enabled": true}}
                """));

            DeviceNetworkSettings n = Assert.IsType<DeviceNetworkSettings>(groups.NetworkSettings);
            Assert.Equal("02:00:00:00:00:01", n.MacAddressBle);
            Assert.Equal("02:00:00:00:00:02", n.MacAddressWifi24);
            Assert.Equal("02:00:00:00:00:03", n.MacAddressWifi5);
            Assert.Equal("30", n.MaxDynamicListenInterval);
            Assert.Equal(2, n.MultiNetPref);
            Assert.True(n.DataRestrictionEnabled);
            Assert.Empty(groups.Unstored);
        }

        [Fact]
        public void RingDeviceNetworkParser_NetworkSettingsGuessedNulls_StoredAsNullWithoutANote()
        {
            RingNetworkGroups groups = RingDeviceNetworkParser.Parse(Fields("""
                {"network_settings": {"max_dynamic_listen_interval": null, "data_restriction_enabled": null}}
                """));

            DeviceNetworkSettings n = Assert.IsType<DeviceNetworkSettings>(groups.NetworkSettings);
            Assert.Null(n.MaxDynamicListenInterval);
            Assert.Null(n.DataRestrictionEnabled);
            Assert.Empty(groups.Unstored);
        }

        [Fact]
        public void RingDeviceNetworkParser_NetworkSettingsWrongType_NotStoredAndNoted()
        {
            RingNetworkGroups groups = RingDeviceNetworkParser.Parse(Fields("""
                {"network_settings": {"multi_net_pref": "SECRET-FAST", "data_restriction_enabled": 1}}
                """));

            DeviceNetworkSettings n = Assert.IsType<DeviceNetworkSettings>(groups.NetworkSettings);
            Assert.Null(n.MultiNetPref);
            Assert.Null(n.DataRestrictionEnabled);
            Assert.Equal(2, groups.Unstored.Count);
            Assert.Contains(groups.Unstored, note => note.StartsWith("network_settings.multi_net_pref not stored"));
            Assert.Contains(groups.Unstored, note => note.StartsWith("network_settings.data_restriction_enabled not stored") && note.EndsWith("(Number)"));
            Assert.DoesNotContain(groups.Unstored, note => note.Contains("SECRET-FAST"));
        }

        [Fact]
        public void RingDeviceNetworkParser_NetworkSettingsUnknownKey_IsNoted()
        {
            RingNetworkGroups groups = RingDeviceNetworkParser.Parse(Fields("""{"network_settings": {"future_network_key": "x"}}"""));

            Assert.Contains(groups.Unstored, note => note.StartsWith("network_settings.future_network_key not stored"));
        }

        [Fact]
        public void RingDeviceNetworkParser_NetworkDiagnosisAllNull_YieldsRowOfNullsWithoutNotes()
        {
            RingNetworkGroups groups = RingDeviceNetworkParser.Parse(Fields("""
                {"network_settings": {"network_diagnosis": {"channel_analysis": null, "tcp_host": null, "udp_bandwidth": null}}}
                """));

            RingDeviceNetworkDiagnosis d = Assert.IsType<RingDeviceNetworkDiagnosis>(groups.NetworkDiagnosis);
            Assert.Null(d.ChannelAnalysis);
            Assert.Null(d.TcpHost);
            Assert.Null(d.UdpBandwidth);
            Assert.Empty(groups.Unstored);
        }

        [Fact]
        public void RingDeviceNetworkParser_NetworkDiagnosisTypedValues_MapsEachKey()
        {
            RingNetworkGroups groups = RingDeviceNetworkParser.Parse(Fields("""
                {"network_settings": {"network_diagnosis": {"channel_analysis": true, "periodic_diagnosis": false, "lan_performance": true,
                "tcp_iperf": false, "udp_iperf": true, "tcp_host": "diag-host-fake", "udp_host": "diag-host-udp", "tcp_port": 5201,
                "udp_port": 5202, "udp_bandwidth": 1000000}}}
                """));

            RingDeviceNetworkDiagnosis d = Assert.IsType<RingDeviceNetworkDiagnosis>(groups.NetworkDiagnosis);
            Assert.True(d.ChannelAnalysis);
            Assert.False(d.PeriodicDiagnosis);
            Assert.True(d.LanPerformance);
            Assert.False(d.TcpIperf);
            Assert.True(d.UdpIperf);
            Assert.Equal("diag-host-fake", d.TcpHost);
            Assert.Equal("diag-host-udp", d.UdpHost);
            Assert.Equal(5201, d.TcpPort);
            Assert.Equal(5202, d.UdpPort);
            Assert.Equal(1000000L, d.UdpBandwidth);
            Assert.Empty(groups.Unstored);
        }

        [Fact]
        public void RingDeviceNetworkParser_NetworkDiagnosisWrongTypes_NotStoredAndNoted()
        {
            RingNetworkGroups groups = RingDeviceNetworkParser.Parse(Fields("""
                {"network_settings": {"network_diagnosis": {"tcp_port": "SECRET-PORT", "udp_bandwidth": 1.5, "tcp_iperf": "yes"}}}
                """));

            RingDeviceNetworkDiagnosis d = Assert.IsType<RingDeviceNetworkDiagnosis>(groups.NetworkDiagnosis);
            Assert.Null(d.TcpPort);
            Assert.Null(d.UdpBandwidth);
            Assert.Null(d.TcpIperf);
            Assert.Equal(3, groups.Unstored.Count);
            Assert.All(groups.Unstored, note => Assert.StartsWith("network_settings.network_diagnosis.", note));
            Assert.DoesNotContain(groups.Unstored, note => note.Contains("SECRET-PORT"));
        }

        [Fact]
        public void RingDeviceNetworkParser_NetworkDiagnosisUnknownKey_IsNoted()
        {
            RingNetworkGroups groups = RingDeviceNetworkParser.Parse(Fields("""
                {"network_settings": {"network_diagnosis": {"mystery_probe": 3}}}
                """));

            Assert.Contains(groups.Unstored, note => note.StartsWith("network_settings.network_diagnosis.mystery_probe not stored"));
        }

        [Fact]
        public void RingDeviceNetworkParser_NetworkSettingsWithoutDiagnosis_LeavesDiagnosisNull()
        {
            RingNetworkGroups groups = RingDeviceNetworkParser.Parse(Fields("""{"network_settings": {"multi_net_pref": 1}}"""));

            Assert.NotNull(groups.NetworkSettings);
            Assert.Null(groups.NetworkDiagnosis);
        }
    }
}
