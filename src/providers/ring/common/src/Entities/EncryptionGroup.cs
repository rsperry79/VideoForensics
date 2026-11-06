using System.Text.Json;
using System.Text.Json.Serialization;

namespace VideoForensics.Providers.Ring.Entities
{
    /// <summary>The encryption_group object on a ring_devices entry. Null in every captured sample, so values are kept as JSON and read leniently.</summary>
    public class EncryptionGroup
    {
        [JsonPropertyName("id")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public JsonElement? Id { get; set; }

        [JsonPropertyName("account_group_revision_id")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public JsonElement? AccountGroupRevisionId { get; set; }
    }
}
