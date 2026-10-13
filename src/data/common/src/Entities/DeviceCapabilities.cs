namespace VideoForensics.Data.Common.Entities
{
    /// <summary>Device capability and spec information: resolution, audio, night vision, storage.</summary>
    public class DeviceCapabilities
    {
        public Guid Id { get; set; }
        public Guid DeviceId { get; set; }
        public string? Resolution { get; set; }
        public bool? HasAudio { get; set; }
        public bool? HasNightVision { get; set; }
        public bool? HasMotionDetection { get; set; }
        public bool? HasCloudStorage { get; set; }
        public string? StorageType { get; set; }
        public int? MaxStorageDays { get; set; }
        public string? FirmwareVersion { get; set; }
        public string? HardwareModel { get; set; }
        public DateTime? LastSyncedUtc { get; set; }
        public SyncStatus SyncStatus { get; set; } = SyncStatus.Pending;
        public string? ApiResponseHash { get; set; }
        public string? MetadataJson { get; set; }
    }
}
