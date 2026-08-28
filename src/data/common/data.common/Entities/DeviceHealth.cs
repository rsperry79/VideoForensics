namespace VideoForensics.Data.Common.Entities
{
    /// <summary>Current device health state: battery, WiFi, connectivity.</summary>
    public class DeviceHealth
    {
        public Guid Id { get; set; }
        public Guid DeviceId { get; set; }
        public decimal? BatteryPercentage { get; set; }
        public int? WifiSignalRssi { get; set; }
        public string? WifiName { get; set; }
        public bool? IsOnline { get; set; }
        public DateTime? LastHeartbeatUtc { get; set; }
        public string? Status { get; set; }
        public DateTime? LastSyncedUtc { get; set; }
        public SyncStatus SyncStatus { get; set; } = SyncStatus.Pending;
        public string? ApiResponseHash { get; set; }
        public string? MetadataJson { get; set; }
    }
}
