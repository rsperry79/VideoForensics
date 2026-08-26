namespace VideoForensics.Data.Common.Entities
{
    /// <summary>A snapshot of device health information captured during a download event.</summary>
    public class DeviceHealthSnapshot
    {
        public Guid Id { get; set; }
        public Guid DownloadEventId { get; set; }
        public bool? Connected { get; set; }
        public decimal? BatteryPercentage { get; set; }
        public int? Rssi { get; set; }
        public string? WifiName { get; set; }
        public string? FirmwareVersion { get; set; }
        public DateTime CapturedAtUtc { get; set; }
    }
}
