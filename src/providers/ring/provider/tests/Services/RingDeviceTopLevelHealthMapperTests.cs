using System.Text.Json;

using VideoForensics.Core.Providers.Common.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Providers.Ring.Entities;
using VideoForensics.Providers.Ring.Services;

using RingDeviceDetailsRow = VideoForensics.Data.Common.Entities.RingDeviceDetails;

using Xunit;

using RingDeviceDetails = VideoForensics.Providers.Ring.Services.RingDeviceDetails;

namespace VideoForensics.Providers.Ring.Tests
{
    /// <summary>Maps the top-level scalars, led/siren/do-not-disturb seconds and the health object (sanitized fixture) onto typed rows.</summary>
    public class RingDeviceTopLevelHealthMapperTests
    {
        private static readonly Guid DeviceId = Guid.Parse("11111111-2222-3333-4444-555555555555");

        private static RingDeviceDetails Load(string providerId)
        {
            string path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "device-top-level-health-sanitized.json");
            Devices devices = JsonSerializer.Deserialize<Devices>(File.ReadAllText(path))!;
            return RingDeviceDetailsMapper.Find(devices, providerId)!;
        }

        [Fact]
        public void ToDeviceFields_Doorbot_MapsAgnosticTimesAndRingRingId()
        {
            DeviceDetailFields f = Load("1001").DeviceFields!;

            Assert.Equal(new DateTime(2025, 1, 2, 3, 4, 5, DateTimeKind.Utc), f.DeactivatedAtUtc);
            Assert.Equal(new DateTime(2025, 2, 3, 4, 5, 6, DateTimeKind.Utc), f.SharedAtUtc);
            Assert.Equal("ring-fake-1", f.RingRingId);
            Assert.Equal("America/Los_Angeles", Load("1001").TimeZone);
        }

        [Fact]
        public void ToSettingsChildren_Doorbot_MapsRingOnlyTopLevelScalarsToRingDetailsRow()
        {
            RingDeviceDetailsRow r = RingDeviceDetailsMapper.ToSettingsChildrenWithFeatures(Load("1001"), DeviceId)!.RingDetails!;

            Assert.Equal(DeviceId, r.DeviceId);
            Assert.NotEqual(Guid.Empty, r.Id);
            Assert.Equal("schema-fake-0001", r.SchemaId);
            Assert.False(r.IsSidewalkGateway);
            Assert.Equal("rduri:test:0:device.ring|1001", r.DeviceResourceId);
            Assert.Equal("net-fake-1", r.RingNetId);
            Assert.Equal("00000000-0000-0000-0000-000000000abc", r.ActiveScheduleUuid);
            Assert.Equal("auto", r.FlickElimRecommendedMode);
            Assert.Equal(2, r.ExtPowerState);
            Assert.Equal("group-fake-1", r.EncryptionGroupId);
            Assert.Equal("rev-fake-9", r.AccountGroupRevisionId);
        }

        [Fact]
        public void ToSettingsChildren_AuthorizedDoorbotWithNullEncryptionGroup_LeavesGroupFieldsNull()
        {
            RingDeviceDetailsRow r = RingDeviceDetailsMapper.ToSettingsChildrenWithFeatures(Load("1002"), DeviceId)!.RingDetails!;

            Assert.Null(r.EncryptionGroupId);
            Assert.Null(r.AccountGroupRevisionId);
            Assert.Null(r.SchemaId);
        }

        [Fact]
        public void UnstoredNotes_SnoozeSettings_LoggedWithJsonTypeAndNotStored()
        {
            Assert.Contains(Load("2001").UnstoredNotes!, n => n == "snooze_settings not stored: no column, shape unknown (Null)");
            Assert.Contains(Load("1001").UnstoredNotes!, n => n.StartsWith("snooze_settings not stored") && n.EndsWith("(String)"));
        }

        [Fact]
        public void ToDeviceFields_StickupCam_MapsLedAndSirenSecondsAndNullScalars()
        {
            DeviceDetailFields f = Load("2001").DeviceFields!;

            Assert.Equal(7, f.LedSecondsRemaining);
            Assert.Equal(3, f.SirenSecondsRemaining);
            Assert.Null(f.DeactivatedAtUtc);
            Assert.Equal("ring-fake-2", f.RingRingId);
        }

        [Fact]
        public void ToDeviceFields_Chime_MapsRingIdSchemaAndDoNotDisturbSeconds()
        {
            DeviceDetailFields f = Load("3001").DeviceFields!;

            Assert.Equal("ring-fake-3", f.RingRingId);
            Assert.Equal(12, f.DoNotDisturbSecondsLeft);
        }

        [Fact]
        public void ToAlerts_ConnectionString_IsStoredAsDeviceAlertsRowValue()
        {
            DeviceAlerts alert = Assert.Single(RingDeviceDetailsMapper.ToAlerts(Load("2001"), DeviceId));

            Assert.Equal("connection", alert.AlertType);
            Assert.Equal("offline", alert.AlertValue);
        }

        [Fact]
        public void ToSettingsChildren_DoorbotHealth_MapsGenericLeavesToHealthRow()
        {
            DeviceReportedHealth h = RingDeviceDetailsMapper.ToSettingsChildrenWithFeatures(Load("1001"), DeviceId)!.ReportedHealth!;

            Assert.Equal(DeviceId, h.DeviceId);
            Assert.NotEqual(Guid.Empty, h.Id);
            Assert.True(h.Connected);
            Assert.False(h.FloodlightOn);
            Assert.False(h.WhiteLedOn);
            Assert.False(h.SirenOn);
            Assert.Equal(1700000000L, h.LastUpdateTime);
            Assert.Equal(-62.0, h.Rssi);
            Assert.Equal(2921.0, h.BatteryTemp);
            Assert.Equal(5753L, h.Bandwidth);
            Assert.Equal(58766634686231L, h.StatusTime);
            Assert.False(h.BatteryError);
            Assert.Equal(863L, h.FirmwareAvgBitrate);
            Assert.Equal(100, h.StreamResolution);
            Assert.Equal(11627L, h.VideoPacketsTotal);
            Assert.Equal(113674L, h.UptimeSec);
            Assert.Equal(5753L, h.CurrentBandwidth);
            Assert.Equal(5.8m, h.CurrentBandwidthMb);
        }

        [Fact]
        public void ToSettingsChildren_DoorbotHealth_MapsRingOnlyLeavesToExtrasRow()
        {
            RingDeviceHealthExtras x = RingDeviceDetailsMapper.ToSettingsChildrenWithFeatures(Load("1001"), DeviceId)!.RingHealthExtras!;

            Assert.Equal(DeviceId, x.DeviceId);
            Assert.NotEqual(Guid.Empty, x.Id);
            Assert.True(x.RssConnected);
            Assert.Equal("unknown", x.SecondBatteryPercentageCategory);
            Assert.Equal("good", x.SecondBatteryVoltageCategory);
            Assert.False(x.SidewalkConnection);
            Assert.Equal(17.55m, x.TransformerVoltage);
            Assert.Equal("good", x.TransformerVoltageCategory);
            Assert.Equal("best", x.TransformerScore);
            Assert.False(x.HatchOpen);
            Assert.Equal(1650000000L, x.BandwidthLastTime);
            Assert.True(x.PackageWarningActive);
            Assert.Equal("doorbell_test", x.DeviceType);
            Assert.Equal("1.2.3", x.FirmwareVersion);
            Assert.Equal("Up to Date", x.FirmwareVersionStatus);
            Assert.Equal("wifi", x.NetworkConnectionValue);
            Assert.Equal("TestNet", x.WifiName);
            Assert.True(x.VodEnabled);
            Assert.False(x.NightModeOn);
            Assert.Equal(0.0, x.PacketLoss);
            Assert.Equal("good", x.PacketLossCategory);
        }

        [Fact]
        public void UnstoredNotes_FixtureWithUnknownAndNullGuessedValues_NotesKeyAndTypeButNeverValues()
        {
            RingDeviceDetails d = Load("1001");

            Assert.DoesNotContain(d.UnstoredNotes!, n => n.Contains("TestNet") || n.Contains("bat-fake-1"));
            Assert.Contains(d.UnstoredNotes!, n => n.Contains("health.batteries.battery_id is null"));
            RingDeviceDetails stickup = Load("2001");
            Assert.Contains(stickup.UnstoredNotes!, n => n.StartsWith("ring_net_id is null"));
        }

        [Fact]
        public void Parse_UnknownHealthKeyAndWrongShape_AreNotedNotStored()
        {
            var health = JsonSerializer.Deserialize<Entities.DeviceHealth>("{\"connected\":true,\"mystery_key\":{\"a\":1},\"bandwidth\":\"abc\",\"batteries\":[5]}")!;

            RingReportedHealth parsed = RingDeviceHealthParser.Parse(health)!;

            Assert.True(parsed.Health.Connected);
            Assert.Null(parsed.Health.Bandwidth);
            Assert.Empty(parsed.Batteries);
            Assert.Contains(parsed.Unstored, n => n == "health.mystery_key not stored: unknown key or unexpected shape (Object)");
            Assert.Contains(parsed.Unstored, n => n.StartsWith("health.bandwidth not stored"));
            Assert.Contains(parsed.Unstored, n => n.StartsWith("health.batteries[0] not stored"));
        }

        [Fact]
        public void ToSettingsChildren_DoorbotHealth_MapsBatteriesWithPositions()
        {
            IReadOnlyList<DeviceReportedBattery> batteries = RingDeviceDetailsMapper.ToSettingsChildrenWithFeatures(Load("1001"), DeviceId)!.ReportedBatteries!;

            Assert.Equal(2, batteries.Count);
            DeviceReportedBattery first = batteries[0];
            Assert.Equal(0, first.Position);
            Assert.Equal(1, first.BatteryNumber);
            Assert.Equal("bat-fake-1", first.BatteryId);
            Assert.Equal(88, first.Percentage);
            Assert.True(first.Present);
            Assert.False(first.Charging);
            Assert.Equal(3.9m, first.Voltage);
            Assert.Equal("good", first.PercentageCategory);
            Assert.Equal(2921.0, first.Temp);
            Assert.Equal("authentic", first.AuthStatus);
            Assert.Equal("family-a", first.Family);
            Assert.Equal("0.9", first.FwVersion);
            Assert.Equal("none", first.OtaStatus);
            Assert.All(batteries, b => Assert.Equal(DeviceId, b.DeviceId));
            Assert.Equal(1, batteries[1].Position);
            Assert.Null(batteries[1].BatteryId);
            Assert.Null(batteries[1].Charging);
            Assert.Equal("unknown", batteries[1].VoltageCategory);
        }

        [Fact]
        public void ToSettingsChildren_StickupCamEmptyHealthObject_YieldsRowOfNullsAndNoBatteries()
        {
            DeviceSettingsChildren children = RingDeviceDetailsMapper.ToSettingsChildrenWithFeatures(Load("2001"), DeviceId)!;

            Assert.NotNull(children.ReportedHealth);
            Assert.Null(children.ReportedHealth!.Connected);
            Assert.Empty(children.ReportedBatteries!);
        }

        [Fact]
        public void ToSettingsChildren_NoHealthObject_LeavesHealthAndBatteriesNull()
        {
            DeviceSettingsChildren? children = RingDeviceDetailsMapper.ToSettingsChildrenWithFeatures(Load("1002"), DeviceId);

            Assert.Null(children?.ReportedHealth);
            Assert.Null(children?.ReportedBatteries);
        }

        [Fact]
        public void ToSettingsChildren_ChimeHealth_MapsPresentKeysOnly()
        {
            DeviceSettingsChildren children = RingDeviceDetailsMapper.ToSettingsChildrenWithFeatures(Load("3001"), DeviceId)!;
            DeviceReportedHealth h = children.ReportedHealth!;

            Assert.True(h.Connected);
            Assert.Equal(-56.0, h.Rssi);
            Assert.Equal(1700000500L, h.LastUpdateTime);
            Assert.Null(h.UptimeSec);
            Assert.Equal("TestNet", children.RingHealthExtras!.WifiName);
            Assert.Null(children.RingHealthExtras.TransformerVoltage);
        }
    }
}
