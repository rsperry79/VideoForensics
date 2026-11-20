using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace VideoForensics.Providers.Ring
{
    /// <summary>
    /// Checks that writing a location's Location Mode settings back leaves them unchanged. The raw document captured
    /// before the write is the only body sent, so no field the typed model lacks can be lost.
    /// </summary>
    public static class LocationModeSettingsRoundTrip
    {
        /// <summary>The raw settings read before the write, per location, so the restore sends exactly what was there.</summary>
        public static Dictionary<Guid, JsonElement> Captured { get; set; } = [];

        /// <summary>
        /// True when both documents are the same, ignoring lastUpdateTimeMS. Ring stamps that field on every write,
        /// so a plain text compare always fails after a write that changed nothing.
        /// </summary>
        public static bool Same(JsonElement before, JsonElement after) => Normalized(before) == Normalized(after);

        private static string Normalized(JsonElement document)
        {
            JsonNode? node = JsonNode.Parse(document.GetRawText());
            if (node is JsonObject obj)
            {
                _ = obj.Remove("lastUpdateTimeMS");
            }

            return node?.ToJsonString() ?? string.Empty;
        }
    }
}
