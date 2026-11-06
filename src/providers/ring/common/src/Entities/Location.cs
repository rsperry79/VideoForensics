using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

using VideoForensics.Providers.Ring.Converters;

namespace VideoForensics.Providers.Ring.Entities
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
        [JsonConverter(typeof(LenientNullableGuidConverter))]
        public Guid? Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("address")]
        public LocationAddress Address { get; set; }

        [JsonPropertyName("is_owner")]
        public bool? IsOwner { get; set; }

        // Raw JSON for the same reason as LocationAddress: values are read leniently by RingLocationDetailsMapper.
        [JsonPropertyName("created_at")]
        public System.Text.Json.JsonElement? CreatedAt { get; set; }

        [JsonPropertyName("updated_at")]
        public System.Text.Json.JsonElement? UpdatedAt { get; set; }

        [JsonPropertyName("owner_id")]
        public System.Text.Json.JsonElement? OwnerId { get; set; }

        [JsonPropertyName("user_verified")]
        public System.Text.Json.JsonElement? UserVerified { get; set; }

        [JsonPropertyName("is_jobsite")]
        public System.Text.Json.JsonElement? IsJobsite { get; set; }

        [JsonPropertyName("location_resource_id")]
        public System.Text.Json.JsonElement? LocationResourceId { get; set; }

        [JsonPropertyName("location_type")]
        public System.Text.Json.JsonElement? LocationType { get; set; }

        [JsonPropertyName("geo_service_verified")]
        public System.Text.Json.JsonElement? GeoServiceVerified { get; set; }

        [JsonPropertyName("geo_coordinates")]
        public System.Text.Json.JsonElement? GeoCoordinates { get; set; }

        [JsonPropertyName("entitlements")]
        public System.Text.Json.JsonElement? Entitlements { get; set; }
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

        // Read as raw JSON so a Ring value of an unexpected type cannot fail the whole locations response.
        [JsonPropertyName("address2")]
        public System.Text.Json.JsonElement? Address2 { get; set; }

        [JsonPropertyName("country")]
        public System.Text.Json.JsonElement? Country { get; set; }

        [JsonPropertyName("cross_street")]
        public System.Text.Json.JsonElement? CrossStreet { get; set; }
    }
}
