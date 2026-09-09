namespace VideoForensics.Data.Common.Entities
{
    /// <summary>Time-series health metrics for a device (P0 entity for provider metadata normalization).</summary>
    public class DeviceHealth
    {
        public Guid Id { get; set; }
        public Guid DeviceId { get; set; }
        public decimal? BatteryPercentage { get; set; }
        public decimal? BatteryVoltageValue { get; set; }
        public int? WifiSignalRssi { get; set; }
        public string? WifiName { get; set; }
        public bool? IsExternalPowerConnected { get; set; }
        public string? OtaStatus { get; set; }
        public bool? IsOnline { get; set; }
        public DateTime? LastHeartbeatUtc { get; set; }
        public DateTime CapturedAtUtc { get; set; }
    }
}
