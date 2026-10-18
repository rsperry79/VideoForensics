using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace VideoForensics.Providers.Ring.Entities
{
    /// <summary>
    /// Response shape of the location mode settings call: per mode, which device actions apply.
    /// Each mode key ("disarmed", "home", "away") holds one entry per device.
    /// </summary>
    public class LocationModeSettingsResponse
    {
        [JsonPropertyName("disarmed")]
        public List<LocationModeSettingsEntry> Disarmed { get; set; }

        [JsonPropertyName("home")]
        public List<LocationModeSettingsEntry> Home { get; set; }

        [JsonPropertyName("away")]
        public List<LocationModeSettingsEntry> Away { get; set; }

        [JsonPropertyName("options")]
        public LocationModeSettingsOptions Options { get; set; }

        [JsonPropertyName("lastUpdateTimeMS")]
        public long? LastUpdateTimeMs { get; set; }
    }

    /// <summary>One device's actions within a single mode.</summary>
    public class LocationModeSettingsEntry
    {
        [JsonPropertyName("deviceId")]
        public string DeviceId { get; set; }

        [JsonPropertyName("deviceIdType")]
        public string DeviceIdType { get; set; }

        /// <summary>Actions enabled for this device in this mode.</summary>
        [JsonPropertyName("actions")]
        public List<string> Actions { get; set; }

        /// <summary>Actions the device supports, keyed by action group (e.g. "liveView", "motionDetection").</summary>
        [JsonPropertyName("supportedActions")]
        public Dictionary<string, List<string>> SupportedActions { get; set; }
    }

    /// <summary>Options block of the location mode settings response.</summary>
    public class LocationModeSettingsOptions
    {
        [JsonPropertyName("supportedActionGroups")]
        public List<string> SupportedActionGroups { get; set; }

        [JsonPropertyName("defaultCameraOnlyMode")]
        public string DefaultCameraOnlyMode { get; set; }
    }
}
