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
    /// <summary>
    /// Ring-only zone rows: ignore zones with vertices, the motion_zones and advanced_motion_detection_types arrays, the two settings
    /// scalars, and the unstored notes for snooze values. All values are fake.
    /// </summary>
    public class RingDeviceZoneRowsTests
    {
        private static readonly Guid DeviceId = Guid.Parse("11111111-2222-3333-4444-555555555555");

        private const string StickupJson =
            "{\"id\":777,\"settings\":{" +
            "\"ignore_zones\":{\"zone1\":{\"name\":\"Zone 1\",\"state\":0,\"vertex1\":{\"x\":0,\"y\":0}}," +
            "\"zone2\":{\"name\":\"Fake Zone\",\"state\":1,\"vertex1\":{\"x\":0.1,\"y\":0.2},\"vertex2\":{\"x\":0.3,\"y\":0.5}}}," +
            "\"motion_zones\":[0,1,0]," +
            "\"advanced_motion_detection_types\":[\"human\"]," +
            "\"advanced_motion_zones_enabled\":true,\"advanced_motion_zones_type\":\"8vertices\"}}";

        private static RingDeviceDetails? StickupDetails(string json)
        {
            var devices = new Devices { StickupCams = [JsonSerializer.Deserialize<StickupCam>(json)!] };
            return RingDeviceDetailsMapper.Find(devices, "777");
        }

        [Fact]
        public void ToSettingsChildren_StickupCam_IgnoreZonesBecomeZoneAndVertexRows()
        {
            RingDeviceDetails? details = StickupDetails(StickupJson);

            DeviceSettingsChildren? children = RingDeviceDetailsMapper.ToSettingsChildrenWithFeatures(details!, DeviceId);

            Assert.Equal(2, children!.RingIgnoreZones!.Count);
            RingDeviceIgnoreZone zone = children.RingIgnoreZones.Single(z => z.ZoneNumber == 2);
            Assert.Equal("Fake Zone", zone.Name);
            Assert.Equal(1, zone.State);
            Assert.Equal(DeviceId, zone.DeviceId);
            Assert.Equal(2, zone.Vertices.Count);
            Assert.Equal(2, zone.Vertices[1].Position);
            Assert.Equal(0.5, zone.Vertices[1].Y);
        }

        [Fact]
        public void ToSettingsChildren_StickupCam_MotionZonesAndDetectionTypesKeepRingOrder()
        {
            RingDeviceDetails? details = StickupDetails(StickupJson);

            DeviceSettingsChildren? children = RingDeviceDetailsMapper.ToSettingsChildrenWithFeatures(details!, DeviceId);

            Assert.Equal(new long?[] { 0, 1, 0 }, children!.RingMotionZones!.OrderBy(r => r.Position).Select(r => r.Value).ToArray());
            RingDeviceAdvancedMotionDetectionType type = Assert.Single(children.RingAdvancedMotionDetectionTypes!);
            Assert.Equal("human", type.DetectionType);
            Assert.Equal(0, type.Position);
        }

        [Fact]
        public void ToSettingsChildren_Chime_LeavesRingZoneRowsNull()
        {
            var devices = new Devices { Chimes = [JsonSerializer.Deserialize<Chime>("{\"id\":5,\"kind\":\"chime\"}")!] };
            RingDeviceDetails? details = RingDeviceDetailsMapper.Find(devices, "5");

            DeviceSettingsChildren? children = RingDeviceDetailsMapper.ToSettingsChildrenWithFeatures(details!, DeviceId);

            Assert.Null(children!.RingIgnoreZones);
            Assert.Null(children.RingMotionZones);
            Assert.Null(children.RingAdvancedMotionDetectionTypes);
        }

        [Fact]
        public void Parse_AdvancedMotionZonesScalars_MapToRingSettingsAndLeaveTheRemainder()
        {
            var fields = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
                "{\"advanced_motion_zones_enabled\":true,\"advanced_motion_zones_type\":\"8vertices\",\"other\":1}")!;

            RingSettingsGroups settings = RingDeviceSettingsParser.Parse(true, fields, null);
            string? remainder = RingDeviceSettingGroupParser.RemainderJson(fields);

            Assert.True(settings.Ring!.AdvancedMotionZonesEnabled);
            Assert.Equal("8vertices", settings.Ring.AdvancedMotionZonesType);
            Assert.NotNull(remainder);
            Assert.DoesNotContain("advanced_motion_zones_enabled", remainder);
            Assert.Contains("other", remainder);
        }

        [Fact]
        public void Find_DoorbotWithSnoozeValues_NotesTheJsonTypeOnly()
        {
            var devices = new Devices
            {
                Doorbots = [JsonSerializer.Deserialize<Doorbot>("{\"id\":9,\"motion_snooze\":{\"until\":12345},\"snooze_settings\":[1,2]}")!]
            };

            RingDeviceDetails? details = RingDeviceDetailsMapper.Find(devices, "9");

            Assert.Contains("motion_snooze not stored as columns: shape unknown (Object)", details!.UnstoredNotes!);
            Assert.Contains("snooze_settings not stored: no column, shape unknown (Array)", details.UnstoredNotes!);
            Assert.DoesNotContain(details.UnstoredNotes!, n => n.Contains("12345"));
        }

        [Fact]
        public void Find_DoorbotWithoutMotionSnooze_AddsNoMotionSnoozeNote()
        {
            var devices = new Devices { Doorbots = [JsonSerializer.Deserialize<Doorbot>("{\"id\":10}")!] };

            RingDeviceDetails? details = RingDeviceDetailsMapper.Find(devices, "10");

            Assert.DoesNotContain(details!.UnstoredNotes!, n => n.StartsWith("motion_snooze"));
        }
    }
}
