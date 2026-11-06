namespace VideoForensics.Data.Common.Entities
{
    /// <summary>
    /// Provider-agnostic device-reported health: one row per device, replaced on every sync that carries a health object. Every value is a
    /// typed column; null means the provider did not send it. Separate from the DeviceHealths time-series captures. Batteries are in
    /// <see cref="DeviceReportedBattery"/>; provider-only readings live in provider extras tables (see <see cref="RingDeviceHealthExtras"/>).
    /// </summary>
    public class DeviceReportedHealth
    {
        public Guid Id { get; set; }
        public Guid DeviceId { get; set; }

        /// <summary>Ring health.connected.</summary>
        public bool? Connected { get; set; }

        /// <summary>Ring health.floodlight_on.</summary>
        public bool? FloodlightOn { get; set; }

        /// <summary>Unix epoch seconds (UTC) of Ring's last health update.</summary>
        public long? LastUpdateTime { get; set; }

        /// <summary>Signal strength in dBm.</summary>
        public double? Rssi { get; set; }

        /// <summary>Ring health.white_led_on.</summary>
        public bool? WhiteLedOn { get; set; }

        /// <summary>
        /// Battery temperature as Ring sends it (seen as 2921.0). Unit is an ASSUMPTION (deci-kelvin: tenths of a kelvin) pending a check on
        /// 2026-11-01; see <see cref="BatteryTempCelsius"/>.
        /// </summary>
        public double? BatteryTemp { get; set; }

        /// <summary>BatteryTemp converted to Celsius (raw / 10 - 273.15, 2 dp) under the deci-kelvin assumption. Null when absent or the raw value is out of range.</summary>
        public decimal? BatteryTempCelsius { get; set; }

        /// <summary>Ring bandwidth reading as sent (unit not documented by Ring).</summary>
        public long? Bandwidth { get; set; }

        /// <summary>Ring health.siren_on.</summary>
        public bool? SirenOn { get; set; }

        /// <summary>Ring status_time as sent: ticks of a 32768 Hz clock since the Unix epoch (seen as 5.8e13). See <see cref="StatusAtUtc"/>.</summary>
        public long? StatusTime { get; set; }

        /// <summary>StatusTime converted to UTC (ticks / 32768 seconds after the Unix epoch). Null when absent or outside 2000..2100.</summary>
        public DateTime? StatusAtUtc { get; set; }

        /// <summary>Ring health.battery_error.</summary>
        public bool? BatteryError { get; set; }

        /// <summary>Ring sends this as a numeric string; stored as a number, null if it does not parse.</summary>
        public long? FirmwareAvgBitrate { get; set; }

        /// <summary>stream_resolution as sent (e.g. 100).</summary>
        public int? StreamResolution { get; set; }

        /// <summary>Ring sends this as a numeric string; stored as a number, null if it does not parse.</summary>
        public long? VideoPacketsTotal { get; set; }

        /// <summary>Device uptime in seconds.</summary>
        public long? UptimeSec { get; set; }

        /// <summary>Ring current_bandwidth as sent (unit not documented by Ring).</summary>
        public long? CurrentBandwidth { get; set; }

        /// <summary>current_bandwidth_mb: current bandwidth in megabytes as Ring reports it.</summary>
        public decimal? CurrentBandwidthMb { get; set; }

        /// <summary>UTC time this row was written by a sync (not a Ring value).</summary>
        public DateTime UpdatedAtUtc { get; set; }
    }
}
