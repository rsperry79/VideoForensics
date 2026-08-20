using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

using KoenZomers.Ring.Api.Converters;

namespace KoenZomers.Ring.Api.Entities
{
    /// <summary>
    /// Connectivity/battery telemetry embedded directly in the "health" object of each device
    /// returned by GET /clients_api/ring_devices - no separate API call is required to obtain this.
    /// </summary>
    public class DeviceHealth
    {
        [JsonPropertyName("connected")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public bool? Connected { get; set; }

        [JsonPropertyName("device_type")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string DeviceType { get; set; }

        [JsonPropertyName("firmware_version")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string FirmwareVersion { get; set; }

        [JsonPropertyName("firmware_version_status")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string FirmwareVersionStatus { get; set; }

        [JsonPropertyName("battery_present")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public bool? BatteryPresent { get; set; }

        // Ring returns this as a JSON string (e.g. "100") rather than a number - confirmed via a
        // live ApiTester run (doorbot-health) after the naive long? deserialization threw.
        [JsonPropertyName("battery_percentage")]
        [JsonConverter(typeof(BatteryLifeConverter))]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? BatteryPercentage { get; set; }

        [JsonPropertyName("battery_percentage_category")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string BatteryPercentageCategory { get; set; }

        [JsonPropertyName("battery_voltage")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public decimal? BatteryVoltage { get; set; }

        [JsonPropertyName("battery_voltage_category")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string BatteryVoltageCategory { get; set; }

        [JsonPropertyName("rssi")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public double? Rssi { get; set; }

        [JsonPropertyName("rssi_category")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string RssiCategory { get; set; }

        [JsonPropertyName("network_connection_value")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string NetworkConnectionValue { get; set; }

        [JsonPropertyName("wifi_name")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string WifiName { get; set; }

        [JsonPropertyName("packet_loss")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public double? PacketLoss { get; set; }

        [JsonPropertyName("packet_loss_category")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string PacketLossCategory { get; set; }

        [JsonPropertyName("ota_status")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string OtaStatus { get; set; }

        [JsonPropertyName("ext_power_state")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? ExtPowerState { get; set; }

        [JsonPropertyName("last_update_time")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public long? LastUpdateTime { get; set; }

        [JsonPropertyName("id")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public long? Id { get; set; }

        [JsonPropertyName("latest_signal_strength")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public double? LatestSignalStrength { get; set; }

        [JsonPropertyName("latest_signal_category")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string LatestSignalCategory { get; set; }

        [JsonPropertyName("average_signal_strength")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public double? AverageSignalStrength { get; set; }

        [JsonPropertyName("average_signal_category")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string AverageSignalCategory { get; set; }

        [JsonPropertyName("firmware")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string Firmware { get; set; }

        [JsonPropertyName("updated_at")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string UpdatedAt { get; set; }

        [JsonPropertyName("packet_loss_strength")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public double? PacketLossStrength { get; set; }

        [JsonPropertyName("wifi_is_ring_network")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public bool? WifiIsRingNetwork { get; set; }

        [JsonPropertyName("ptz_connected")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public bool? PtzConnected { get; set; }

        [JsonPropertyName("supported_rpc_commands")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<string> SupportedRpcCommands { get; set; }
    }
}
