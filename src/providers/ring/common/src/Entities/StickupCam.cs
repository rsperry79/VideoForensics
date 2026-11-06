using System;
using System.Text.Json;
using System.Text.Json.Serialization;

using VideoForensics.Providers.Ring.Converters;

namespace VideoForensics.Providers.Ring.Entities
{
    public class StickupCam
    {
        [JsonPropertyName("id")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public long? Id { get; set; }

        [JsonPropertyName("description")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string Description { get; set; }

        [JsonPropertyName("device_id")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string DeviceId { get; set; }

        [JsonPropertyName("time_zone")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string TimeZone { get; set; }

        [JsonPropertyName("subscribed")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public bool? Subscribed { get; set; }

        [JsonPropertyName("subscribed_motions")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public bool? SubscribedMotions { get; set; }

        [JsonPropertyName("battery_life")]
        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public long? BatteryLife { get; set; }

        [JsonPropertyName("external_connection")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public bool? ExternalConnection { get; set; }

        [JsonPropertyName("firmware_version")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string FirmwareVersion { get; set; }

        [JsonPropertyName("kind")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string Kind { get; set; }

        [JsonPropertyName("latitude")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public double? Latitude { get; set; }

        [JsonPropertyName("longitude")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public double? Longitude { get; set; }

        [JsonPropertyName("address")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string Address { get; set; }

        [JsonPropertyName("settings")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public StickupCamSettings Settings { get; set; }

        [JsonPropertyName("features")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public StickupCamFeatures Features { get; set; }

        [JsonPropertyName("owned")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public bool? Owned { get; set; }

        [JsonPropertyName("alerts")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public StickupCamAlerts Alerts { get; set; }

        [JsonPropertyName("motion_snooze")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public JsonElement? MotionSnooze { get; set; }

        [JsonPropertyName("stolen")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public bool? Stolen { get; set; }

        [JsonPropertyName("location_id")]
        [JsonConverter(typeof(LenientNullableGuidConverter))]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Guid? LocationId { get; set; }

        [JsonPropertyName("ring_id")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string RingId { get; set; }

        /// <summary>Top-level created_at as Ring sends it (ISO-8601 text). Parsed to UTC by the settings parser.</summary>
        [JsonPropertyName("created_at")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? CreatedAt { get; set; }

        [JsonPropertyName("owner")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Owner Owner { get; set; }

        [JsonPropertyName("battery_life_2")]
        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public long? BatteryLife2 { get; set; }

        [JsonPropertyName("battery_voltage")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public decimal? BatteryVoltage { get; set; }

        [JsonPropertyName("battery_voltage_2")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public decimal? BatteryVoltage2 { get; set; }

        [JsonPropertyName("led_status")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public JsonElement? LedStatus { get; set; }

        [JsonPropertyName("siren_status")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public DeviceStatus.Status SirenStatus { get; set; }

        [JsonPropertyName("night_mode")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public long? NightMode { get; set; }

        [JsonPropertyName("zone_settings")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public ZoneSettings ZoneSettings { get; set; }

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
    }
}
