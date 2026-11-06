using System.Text.Json;

using VideoForensics.Core.Providers.Common.Contracts;
using VideoForensics.Providers.Ring.Entities;
using VideoForensics.Providers.Ring.Services;

using Xunit;

using RingDeviceDetails = VideoForensics.Providers.Ring.Services.RingDeviceDetails;

namespace VideoForensics.Providers.Ring.Tests.Services
{
    /// <summary>
    /// Doorbots read advanced motion zones, live view and snooze presets, and settings.zone_settings the same way stickup cams do.
    /// All values are fake; the shapes follow the captured devices payload.
    /// </summary>
    public class RingDoorbotZoneSettingsTests
    {
        private const string DoorbotJson =
            "{\"id\":4242,\"time_zone\":\"Fake/Zone\",\"settings\":{" +
            "\"advanced_motion_zones\":{\"zone1\":{\"name\":\"Zone A\",\"state\":2," +
            "\"vertex1\":{\"x\":0,\"y\":0.25},\"vertex2\":{\"x\":0.5,\"y\":0.75}}}," +
            "\"live_view_presets\":[\"low\",\"high\"]," +
            "\"motion_snooze_presets\":[\"none\",\"medium\"]," +
            "\"zone_settings\":{\"motion\":[{\"id\":\"z-1\",\"name\":\"Zone M\",\"state\":\"enabled\"," +
            "\"properties\":{\"detection_types\":[\"motion\"],\"style\":1}," +
            "\"vertices\":[{\"x\":0,\"y\":0.1},{\"x\":1,\"y\":0.9}]}]}}}";

        private static RingDeviceDetails? DoorbotDetails()
        {
            var devices = new Devices { Doorbots = [JsonSerializer.Deserialize<Doorbot>(DoorbotJson)!] };
            return RingDeviceDetailsMapper.Find(devices, "4242");
        }

        [Fact]
        public void Find_Doorbot_MapsAdvancedMotionZonesWithVertices()
        {
            DeviceDetailFields? fields = DoorbotDetails()?.DeviceFields;

            AdvancedMotionZoneDetail zone = Assert.Single(fields!.AdvancedMotionZones!);
            Assert.Equal("zone1", zone.ZoneKey);
            Assert.Equal("Zone A", zone.Name);
            Assert.Equal(2, zone.State);
            Assert.Equal(2, zone.Vertices.Count);
            Assert.Equal(0.75, zone.Vertices[1].Y);
        }

        [Fact]
        public void Find_Doorbot_MapsLiveViewAndMotionSnoozePresetsInOrder()
        {
            DeviceDetailFields? fields = DoorbotDetails()?.DeviceFields;

            Assert.Equal(new[] { "low", "high" }, fields!.LiveViewPresets);
            Assert.Equal(new[] { "none", "medium" }, fields.MotionSnoozePresets);
        }

        [Fact]
        public void Find_Doorbot_MapsSettingsZoneSettingsAsMotionZoneRows()
        {
            DeviceDetailFields? fields = DoorbotDetails()?.DeviceFields;

            DeviceZoneDetail zone = Assert.Single(fields!.Zones!);
            Assert.Equal("motion", zone.Source);
            Assert.Equal("z-1", zone.ZoneId);
            Assert.Equal(2, zone.Vertices.Count);
        }
    }
}
