using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Ring.Api.Entities
{
    /// <summary>
    /// Response shape of GET https://api.ring.com/groups/v1/locations/{locationId}/groups
    /// </summary>
    public class GroupsResponse
    {
        [JsonPropertyName("device_groups")]
        public List<Group> DeviceGroups { get; set; }
    }

    /// <summary>
    /// A Ring Smart Lighting group - a set of light fixtures at a location that can be controlled
    /// together, independent of camera-attached floodlights (see Session.SetLight for those).
    /// </summary>
    public class Group
    {
        [JsonPropertyName("device_group_id")]
        public string DeviceGroupId { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }
    }
}
