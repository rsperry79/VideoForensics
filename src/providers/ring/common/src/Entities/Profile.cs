using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace VideoForensics.Providers.Ring.Entities
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

        [JsonPropertyName("first_name_extra")]
        public string FirstNameExtra { get; set; }

        [JsonPropertyName("last_name_extra")]
        public string LastNameExtra { get; set; }

        [JsonPropertyName("hardware_id")]
        public string HardwareId { get; set; }

        [JsonPropertyName("country")]
        public string Country { get; set; }

        [JsonPropertyName("status")]
        public string Status { get; set; }

        /// <summary>ISO-8601 timestamp as Ring sends it (e.g. "2021-01-01T00:00:00.000Z"), kept as text.</summary>
        [JsonPropertyName("created_at")]
        public string CreatedAt { get; set; }

        [JsonPropertyName("tfa_enabled")]
        public bool? TfaEnabled { get; set; }

        [JsonPropertyName("tfa_phone_number")]
        public string TfaPhoneNumber { get; set; }

        [JsonPropertyName("account_type")]
        public string AccountType { get; set; }

        [JsonPropertyName("cfes_enrolled")]
        public bool? CfesEnrolled { get; set; }

        [JsonPropertyName("tsv_state")]
        public string TsvState { get; set; }

        [JsonPropertyName("user_preferences")]
        public UserPreferences UserPreferences { get; set; }
    }

    /// <summary>
    /// The profile's user_preferences object. Both maps hold arbitrary Ring-defined values, so each
    /// value is kept as raw JSON for the mapper to flatten.
    /// </summary>
    public class UserPreferences
    {
        [JsonPropertyName("settings")]
        public Dictionary<string, JsonElement> Settings { get; set; }

        [JsonPropertyName("preferences")]
        public Dictionary<string, JsonElement> Preferences { get; set; }
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
