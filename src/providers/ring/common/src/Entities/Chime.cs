using System;
using System.Text.Json;
using System.Text.Json.Serialization;

using VideoForensics.Providers.Ring.Converters;

namespace VideoForensics.Providers.Ring.Entities
{
    public class Chime
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("location_id")]
        [JsonConverter(typeof(LenientNullableGuidConverter))]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Guid? LocationId { get; set; }

        [JsonPropertyName("description")]
        public string Description { get; set; }

        [JsonPropertyName("device_id")]
        public string DeviceId { get; set; }

        [JsonPropertyName("time_zone")]
        public string TimeZone { get; set; }

        [JsonPropertyName("firmware_version")]
        public string FirmwareVersion { get; set; }

        [JsonPropertyName("kind")]
        public string Kind { get; set; }

        [JsonPropertyName("latitude")]
        public double? Latitude { get; set; }

        [JsonPropertyName("longitude")]
        public double? Longitude { get; set; }

        [JsonPropertyName("address")]
        public string Address { get; set; }

        [JsonPropertyName("settings")]
        public ChimeSettings Settings { get; set; }

        [JsonPropertyName("features")]
        public ChimeFeatures Features { get; set; }

        [JsonPropertyName("owned")]
        public bool? Owned { get; set; }

        [JsonPropertyName("alerts")]
        public ChimeAlerts Alerts { get; set; }

        [JsonPropertyName("do_not_disturb")]
        public DoNotDisturb DoNotDisturb { get; set; }

        [JsonPropertyName("stolen")]
        public bool? Stolen { get; set; }

        [JsonPropertyName("owner")]
        public Owner Owner { get; set; }

        [JsonPropertyName("health")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public DeviceHealth Health { get; set; }

        [JsonPropertyName("schema_id")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string SchemaId { get; set; }

        [JsonPropertyName("is_sidewalk_gateway")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public bool? IsSidewalkGateway { get; set; }

        [JsonPropertyName("deactivated_at")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public JsonElement? DeactivatedAt { get; set; }

        [JsonPropertyName("device_resource_id")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string DeviceResourceId { get; set; }

        [JsonPropertyName("shared_at")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public JsonElement? SharedAt { get; set; }

        [JsonPropertyName("ring_net_id")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public JsonElement? RingNetId { get; set; }

        [JsonPropertyName("active_schedule_uuid")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public JsonElement? ActiveScheduleUuid { get; set; }

        [JsonPropertyName("ext_power_state")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public JsonElement? ExtPowerState { get; set; }

        [JsonPropertyName("snooze_settings")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public JsonElement? SnoozeSettings { get; set; }

        [JsonPropertyName("encryption_group")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public EncryptionGroup EncryptionGroup { get; set; }

        [JsonPropertyName("flick_elim_recommended_mode")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public JsonElement? FlickElimRecommendedMode { get; set; }

        [JsonPropertyName("ring_id")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string RingId { get; set; }

        /// <summary>Top-level created_at as Ring sends it (ISO-8601 text). Parsed to UTC by the settings parser.</summary>
        [JsonPropertyName("created_at")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? CreatedAt { get; set; }
    }
}
