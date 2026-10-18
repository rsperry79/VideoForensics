using System.Text.Json.Serialization;

namespace VideoForensics.Providers.Ring.Entities
{
    /// <summary>Response shape of the location mode sharing call: whether shared users take part in the mode.</summary>
    public class LocationModeSharingResponse
    {
        [JsonPropertyName("sharedUsersEnabled")]
        public bool? SharedUsersEnabled { get; set; }

        [JsonPropertyName("lastUpdateTimeMS")]
        public long? LastUpdateTimeMs { get; set; }
    }
}
