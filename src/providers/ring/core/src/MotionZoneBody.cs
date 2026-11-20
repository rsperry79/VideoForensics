using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;

using VideoForensics.Providers.Ring.Entities;

namespace VideoForensics.Providers.Ring
{
    /// <summary>
    /// Builds the PATCH body for motion zones. Ring stores zones under <c>advanced_motion_settings</c> as
    /// <c>zone_1</c>..<c>zone_3</c>, each with a name, a state and eight vertices (<c>vertex1</c>..<c>vertex8</c>).
    /// This is the shape the Ring app writes; the earlier <c>motion_zones</c> key is not one the device has.
    /// </summary>
    public static class MotionZoneBody
    {
        /// <summary>The body for a set of zones. A zone that was not supplied is left out of the body.</summary>
        public static string FromZones(AdvancedMotionZones zones)
        {
            var advanced = new JsonObject();
            AddZone(advanced, "zone_1", zones.Zone1);
            AddZone(advanced, "zone_2", zones.Zone2);
            AddZone(advanced, "zone_3", zones.Zone3);

            return new JsonObject { ["advanced_motion_settings"] = advanced }.ToJsonString();
        }

        /// <summary>The body that sends a captured <c>advanced_motion_settings</c> document back unchanged.</summary>
        public static string Wrap(JsonElement advancedMotionSettings)
        {
            return new JsonObject { ["advanced_motion_settings"] = JsonNode.Parse(advancedMotionSettings.GetRawText()) }.ToJsonString();
        }

        private static void AddZone(JsonObject advanced, string key, Zone? zone)
        {
            if (zone is null)
            {
                return;
            }

            var node = new JsonObject
            {
                ["name"] = zone.Name,
                ["state"] = zone.State,
            };

            Vertex?[] vertices = [zone.Vertex1, zone.Vertex2, zone.Vertex3, zone.Vertex4, zone.Vertex5, zone.Vertex6, zone.Vertex7, zone.Vertex8];
            for (int i = 0; i < vertices.Length; i++)
            {
                node[$"vertex{i + 1}"] = new JsonObject
                {
                    ["x"] = vertices[i]?.X,
                    ["y"] = vertices[i]?.Y,
                };
            }

            advanced[key] = node;
        }
    }
}
