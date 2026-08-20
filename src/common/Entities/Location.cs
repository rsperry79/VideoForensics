using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace KoenZomers.Ring.Api.Entities
{
    /// <summary>
    /// Response shape of GET https://api.ring.com/devices/v1/locations
    /// </summary>
    public class UserLocationsResponse
    {
        [JsonPropertyName("user_locations")]
        public List<Location> UserLocations { get; set; }
    }

    public class Location
    {
        [JsonPropertyName("location_id")]
        public Guid? Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("address")]
        public LocationAddress Address { get; set; }

        [JsonPropertyName("is_owner")]
        public bool? IsOwner { get; set; }
    }

    public class LocationAddress
    {
        [JsonPropertyName("address1")]
        public string Address1 { get; set; }

        [JsonPropertyName("city")]
        public string City { get; set; }

        [JsonPropertyName("state")]
        public string State { get; set; }

        [JsonPropertyName("zip_code")]
        public string ZipCode { get; set; }

        [JsonPropertyName("timezone")]
        public string TimeZone { get; set; }
    }
}
