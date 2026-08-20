using System.Text.Json.Serialization;

namespace Ring.Api.Entities
{
    /// <summary>
    /// A pending invitation for a user to be granted access to a Ring location. Field shape is
    /// inferred, same caveat as SharedUser.cs - not confirmed against a live capture.
    /// </summary>
    public class Invitation
    {
        [JsonPropertyName("id")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public long? Id { get; set; }

        [JsonPropertyName("invited_email")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string InvitedEmail { get; set; }

        [JsonPropertyName("status")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string Status { get; set; }

        [JsonPropertyName("role")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string Role { get; set; }
    }
}
