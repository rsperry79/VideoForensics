using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Ring.Api.Entities
{
    /// <summary>
    /// A chime ringtone available to assign via UpdateChime(). Field shape confirmed against a
    /// live ApiTester run of GetRingtones() - notably the response wraps the list under "audios"
    /// (see <see cref="RingtonesResponse"/>), not "ringtones" as originally assumed, and "id" is a
    /// non-numeric string (e.g. "chime_default_ding_2"), not the long? originally assumed.
    /// </summary>
    public class Ringtone
    {
        [JsonPropertyName("id")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string Id { get; set; }

        [JsonPropertyName("description")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string Description { get; set; }

        [JsonPropertyName("kind")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string Kind { get; set; }

        [JsonPropertyName("category")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string Category { get; set; }

        [JsonPropertyName("available")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public bool? Available { get; set; }

        [JsonPropertyName("checksum")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? Checksum { get; set; }

        [JsonPropertyName("sample_rate_khz")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? SampleRateKhz { get; set; }

        [JsonPropertyName("supported_device_kinds")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<string> SupportedDeviceKinds { get; set; }

        [JsonPropertyName("url")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string Url { get; set; }

        [JsonPropertyName("url_amz")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string UrlAmz { get; set; }

        [JsonPropertyName("user_id")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string UserId { get; set; }
    }
}
