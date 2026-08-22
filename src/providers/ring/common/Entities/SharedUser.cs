using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace VideoForensics.Providers.Ring.Entities
{
    /// <summary>
    /// A single device grant within a <see cref="SharedUser"/>'s access - Ring grants shared access
    /// per-device, not per-location, so a user's role/permissions are scoped to each device they can
    /// see rather than the user as a whole.
    /// </summary>
    public class SharedUserDevice
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("role")]
        public string Role { get; set; }

        [JsonPropertyName("device_type")]
        public string DeviceType { get; set; }

        [JsonPropertyName("permissions")]
        public object Permissions { get; set; }
    }

    /// <summary>
    /// A user who has been granted access to a Ring location. Confirmed against a live capture of
    /// GET clients_api/locations/{id}/users, which returns a bare JSON array of these (see
    /// Session.SharedUsers.cs.GetSharedUsers) - not the {"users": [...]} wrapper this class used to
    /// assume. Access (role/device_type) is granted per-device, hence <see cref="Devices"/> rather
    /// than a single top-level role.
    /// </summary>
    public class SharedUser
    {
        [JsonPropertyName("id")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public long? Id { get; set; }

        [JsonPropertyName("verified")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public bool? Verified { get; set; }

        [JsonPropertyName("first_name")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string FirstName { get; set; }

        [JsonPropertyName("last_name")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string LastName { get; set; }

        [JsonPropertyName("email")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string Email { get; set; }

        [JsonPropertyName("object_type")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string ObjectType { get; set; }

        [JsonPropertyName("devices")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<SharedUserDevice> Devices { get; set; }
    }
}
