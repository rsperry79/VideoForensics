using System.Collections.Generic;

namespace VideoForensics.Providers.Ring
{
    /// <summary>
    /// Holds the PIR zone 1 sensitivity read before the probe, per device, so the restore sends the captured value.
    /// </summary>
    public static class PirZoneProbe
    {
        /// <summary>Captured zone 1 sensitivity, keyed by doorbot id.</summary>
        public static Dictionary<long, int> Captured { get; set; } = [];

        /// <summary>The value to probe with: the captured value moved by one step, staying inside 0 to 10.</summary>
        public static int Probe(int captured) => captured >= 10 ? captured - 1 : captured + 1;

        /// <summary>The partial PATCH body that sets only zone 1 sensitivity.</summary>
        public static string Body(int zone1Sensitivity) =>
            "{\"motion_settings\":{\"advanced_pir_motion_zones\":{\"zone1_sensitivity\":" + zone1Sensitivity + "}}}";

        /// <summary>The device JSON path that holds the PIR zone sensitivities: motion_settings.advanced_pir_motion_zones.</summary>
        public static int ReadZone1(System.Text.Json.JsonElement deviceSettings) =>
            deviceSettings.GetProperty("motion_settings").GetProperty("advanced_pir_motion_zones").GetProperty("zone1_sensitivity").GetInt32();
    }
}
