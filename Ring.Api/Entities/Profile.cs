using System.Text.Json.Serialization;

namespace KoenZomers.Ring.Api.Entities
{
    /// <summary>
    /// Response shape of GET https://api.ring.com/clients_api/profile - confirmed via a live
    /// ApiTester run to be wrapped in a top-level "profile" key, not the bare object this entity
    /// itself models. See <see cref="ProfileResponse"/> for the wrapper GetProfile() unwraps.
    /// </summary>
    public class Profile
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("email")]
        public string Email { get; set; }

        [JsonPropertyName("first_name")]
        public string FirstName { get; set; }

        [JsonPropertyName("last_name")]
        public string LastName { get; set; }

        // Confirmed via a live capture to be a plain string (e.g. "+12065551234"), not the
        // ambiguous "object" this previously declared.
        [JsonPropertyName("phone_number")]
        public string PhoneNumber { get; set; }

        [JsonPropertyName("authentication_token")]
        public string AuthenticationToken { get; set; }

        [JsonPropertyName("features")]
        public SessionFeatures Features { get; set; }

        [JsonPropertyName("app_brand")]
        public string AppBrand { get; set; }

        [JsonPropertyName("user_flow")]
        public string UserFlow { get; set; }

        [JsonPropertyName("explorer_program_terms")]
        public string ExplorerProgramTerms { get; set; }
    }

    /// <summary>
    /// Top-level response shape of GET https://api.ring.com/clients_api/profile.
    /// </summary>
    public class ProfileResponse
    {
        [JsonPropertyName("profile")]
        public Profile Profile { get; set; }
    }
}
