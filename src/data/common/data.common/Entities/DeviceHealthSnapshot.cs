namespace VideoForensics.Data.Common.Entities
{
    /// <summary>
    /// A point-in-time snapshot of device health/connectivity telemetry. Captured once per
    /// download batch (not tied to a single event), so DeviceId is the primary way to look these
    /// up; DownloadEventId is an optional cross-reference when a snapshot happens to coincide with
    /// a specific download.
    /// </summary>
    public class DeviceHealthSnapshot
    {
        public Guid Id { get; set; }
        public Guid? DeviceId { get; set; }
        public Guid? DownloadEventId { get; set; }
        public bool? Connected { get; set; }
        public decimal? BatteryPercentage { get; set; }
        public int? Rssi { get; set; }
        public string? WifiName { get; set; }
        public string? FirmwareVersion { get; set; }
        public DateTime CapturedAtUtc { get; set; }
    }
}
