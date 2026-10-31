namespace VideoForensics.Data.Common.Entities
{
    /// <summary>
    /// Ring-only health readings from the ring_devices <c>health</c> object (1:1 with the device, replaced with <see cref="DeviceReportedHealth"/>).
    /// Typed columns named after the Ring key; null means Ring did not send the key. Types of keys null in every sample are best guesses.
    /// </summary>
    public class RingDeviceHealthExtras
    {
        public Guid Id { get; set; }
        public Guid DeviceId { get; set; }

        /// <summary>Ring health.device_type.</summary>
        public string? DeviceType { get; set; }

        /// <summary>Ring health.firmware_version.</summary>
        public string? FirmwareVersion { get; set; }

        /// <summary>Ring health.firmware_version_status.</summary>
        public string? FirmwareVersionStatus { get; set; }

        /// <summary>Ring health.external_connection.</summary>
        public bool? ExternalConnection { get; set; }

        /// <summary>Ring health.network_connection_value.</summary>
        public string? NetworkConnectionValue { get; set; }

        /// <summary>Ring health.night_mode_on.</summary>
        public bool? NightModeOn { get; set; }

        /// <summary>Ring health.ota_status.</summary>
        public string? OtaStatus { get; set; }

        /// <summary>Ring health.rss_connected.</summary>
        public bool? RssConnected { get; set; }

        /// <summary>Ring health.rssi_category.</summary>
        public string? RssiCategory { get; set; }

        /// <summary>Ring health.battery_percentage_category.</summary>
        public string? BatteryPercentageCategory { get; set; }

        /// <summary>Ring health.battery_voltage_category.</summary>
        public string? BatteryVoltageCategory { get; set; }

        /// <summary>Ring health.battery_present.</summary>
        public bool? BatteryPresent { get; set; }

        /// <summary>Ring health.battery_percentage.</summary>
        public int? BatteryPercentage { get; set; }

        /// <summary>Ring health.battery_voltage.</summary>
        public decimal? BatteryVoltage { get; set; }

        /// <summary>Ring health.second_battery_percentage_category.</summary>
        public string? SecondBatteryPercentageCategory { get; set; }

        /// <summary>Ring health.second_battery_voltage_category.</summary>
        public string? SecondBatteryVoltageCategory { get; set; }

        /// <summary>Ring health.sidewalk_connection.</summary>
        public bool? SidewalkConnection { get; set; }

        /// <summary>Ring health.transformer_voltage.</summary>
        public decimal? TransformerVoltage { get; set; }

        /// <summary>Ring health.transformer_voltage_category.</summary>
        public string? TransformerVoltageCategory { get; set; }

        /// <summary>Ring health.transformer_score.</summary>
        public string? TransformerScore { get; set; }

        /// <summary>Ring health.packet_loss.</summary>
        public double? PacketLoss { get; set; }

        /// <summary>Ring health.packet_loss_category.</summary>
        public string? PacketLossCategory { get; set; }

        /// <summary>Ring health.hatch_open.</summary>
        public bool? HatchOpen { get; set; }

        /// <summary>Ring health.vod_enabled.</summary>
        public bool? VodEnabled { get; set; }

        /// <summary>Unix epoch seconds (UTC) of the last bandwidth measurement.</summary>
        public long? BandwidthLastTime { get; set; }

        /// <summary>Ring health.wifi_is_ring_network.</summary>
        public bool? WifiIsRingNetwork { get; set; }

        /// <summary>Ring health.wifi_name.</summary>
        public string? WifiName { get; set; }

        /// <summary>Ring health.package_warning_active.</summary>
        public bool? PackageWarningActive { get; set; }

        /// <summary>Ring health.ext_power_state.</summary>
        public int? ExtPowerState { get; set; }

        /// <summary>Ring health.ptz_connected.</summary>
        public bool? PtzConnected { get; set; }

        /// <summary>UTC time this row was written by a sync (not a Ring value).</summary>
        public DateTime UpdatedAtUtc { get; set; }
    }
}
