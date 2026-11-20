using System.Collections.Generic;
using System.Text.Json;

namespace VideoForensics.Providers.Ring
{
    /// <summary>
    /// Holds each doorbot's <c>advanced_motion_settings</c> as read before a motion zone write, so the restore sends
    /// exactly what the device had.
    /// </summary>
    public static class MotionZoneCaptures
    {
        /// <summary>Captured zone document, keyed by doorbot id.</summary>
        public static Dictionary<long, JsonElement> Captured { get; set; } = [];
    }
}
