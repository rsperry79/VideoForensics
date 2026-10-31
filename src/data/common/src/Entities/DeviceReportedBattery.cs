namespace VideoForensics.Data.Common.Entities
{
    /// <summary>
    /// One entry of the <c>health.batteries</c> array: one row per battery per device, replaced with the health row on each sync.
    /// Types of keys that were null in every captured sample (BatteryId, AuthStatus, Family, FwVersion, OtaStatus) are best guesses.
    /// </summary>
    public class DeviceReportedBattery
    {
        public Guid Id { get; set; }
        public Guid DeviceId { get; set; }

        /// <summary>Index within the Ring array.</summary>
        public int Position { get; set; }

        /// <summary>Ring battery entry battery_number.</summary>
        public int? BatteryNumber { get; set; }

        /// <summary>Ring battery entry battery_id.</summary>
        public string? BatteryId { get; set; }

        /// <summary>Ring battery entry battery_percentage.</summary>
        public int? Percentage { get; set; }

        /// <summary>Ring battery entry battery_present.</summary>
        public bool? Present { get; set; }

        /// <summary>Ring battery entry battery_charging.</summary>
        public bool? Charging { get; set; }

        /// <summary>Ring battery entry battery_voltage.</summary>
        public decimal? Voltage { get; set; }

        /// <summary>Ring battery entry battery_percentage_category.</summary>
        public string? PercentageCategory { get; set; }

        /// <summary>Ring battery entry battery_voltage_category.</summary>
        public string? VoltageCategory { get; set; }

        /// <summary>Ring battery entry battery_temp. Temperature as Ring sends it (unit not documented).</summary>
        public double? Temp { get; set; }

        /// <summary>Ring battery entry battery_auth_status.</summary>
        public string? AuthStatus { get; set; }

        /// <summary>Ring battery entry battery_family.</summary>
        public string? Family { get; set; }

        /// <summary>Ring battery entry battery_fw_version.</summary>
        public string? FwVersion { get; set; }

        /// <summary>Ring battery entry battery_ota_status.</summary>
        public string? OtaStatus { get; set; }
    }
}
