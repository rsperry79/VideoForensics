using System;
using System.Collections.Generic;
using System.Text.Json;

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

        /// <summary>True when both documents are byte-for-byte the same JSON text.</summary>
        public static bool Same(JsonElement before, JsonElement after) => before.GetRawText() == after.GetRawText();
    }
}
