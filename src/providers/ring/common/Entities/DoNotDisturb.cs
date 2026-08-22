using System.Text.Json.Serialization;

namespace VideoForensics.Providers.Ring.Entities
{
    public class DoNotDisturb
    {
        [JsonPropertyName("seconds_left")]
        public decimal? SecondsLeft { get; set; }
    }
}
