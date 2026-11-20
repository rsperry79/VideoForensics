using System.Text.Json;

using VideoForensics.Providers.Ring.Entities;

using Xunit;

namespace VideoForensics.Providers.Ring.Tests
{
    /// <summary>
    /// The motion zone write goes to advanced_motion_settings with zone_1..zone_3 keys and vertex1..vertex8 points,
    /// the shape the Ring app writes on a device. The old motion_zones body is not a key the device has.
    /// </summary>
    public class MotionZoneBodyTests
    {
        private static Zone Zone(string name, double x) => new()
        {
            Name = name,
            State = 2,
            Vertex1 = new Vertex { X = x, Y = 0.4 },
            Vertex2 = new Vertex { X = 1, Y = 1 },
            Vertex3 = new Vertex { X = 0, Y = 1 },
            Vertex4 = new Vertex { X = 0.5, Y = 0.5 },
            Vertex5 = new Vertex { X = 0.25, Y = 0.75 },
            Vertex6 = new Vertex { X = 0.75, Y = 0.25 },
            Vertex7 = new Vertex { X = 0.1, Y = 0.9 },
            Vertex8 = new Vertex { X = 0.9, Y = 0.1 },
        };

        [Fact]
        public void FromZones_WritesAdvancedMotionSettingsWithZoneNKeys()
        {
            var zones = new AdvancedMotionZones { Zone1 = Zone("Default Zone", 0), Zone2 = Zone("Zone 2", 0.67) };

            using JsonDocument doc = JsonDocument.Parse(MotionZoneBody.FromZones(zones));
            JsonElement advanced = doc.RootElement.GetProperty("advanced_motion_settings");

            Assert.Equal("Default Zone", advanced.GetProperty("zone_1").GetProperty("name").GetString());
            Assert.Equal(0.67, advanced.GetProperty("zone_2").GetProperty("vertex1").GetProperty("x").GetDouble());
            Assert.Equal(2, advanced.GetProperty("zone_1").GetProperty("state").GetInt32());
            Assert.False(advanced.TryGetProperty("zone_3", out _), "a zone that was not supplied is not written");
            Assert.False(doc.RootElement.TryGetProperty("motion_zones", out _), "the old motion_zones key is gone");
        }

        [Fact]
        public void FromZones_WritesEightVertices()
        {
            using JsonDocument doc = JsonDocument.Parse(MotionZoneBody.FromZones(new AdvancedMotionZones { Zone1 = Zone("Z", 0) }));
            JsonElement zone1 = doc.RootElement.GetProperty("advanced_motion_settings").GetProperty("zone_1");

            for (int i = 1; i <= 8; i++)
            {
                Assert.True(zone1.TryGetProperty($"vertex{i}", out _), $"vertex{i} is written");
            }
        }

        [Fact]
        public void Wrap_SendsTheCapturedDocumentUnderAdvancedMotionSettings()
        {
            JsonElement captured = JsonDocument.Parse("{\"zone_1\":{\"name\":\"Default Zone\",\"state\":2}}").RootElement.Clone();

            using JsonDocument doc = JsonDocument.Parse(MotionZoneBody.Wrap(captured));

            Assert.Equal("Default Zone", doc.RootElement.GetProperty("advanced_motion_settings").GetProperty("zone_1").GetProperty("name").GetString());
        }
    }
}
