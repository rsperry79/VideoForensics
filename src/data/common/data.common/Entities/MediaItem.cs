namespace VideoForensics.Data.Common.Entities
{
    /// <summary>A media file downloaded from a device.</summary>
    public class MediaItem
    {
        public Guid Id { get; set; }
        public Guid DeviceId { get; set; }
        public Guid? DownloadEventId { get; set; }
        public required string FileName { get; set; }
        public required string FilePath { get; set; }
        public required string MediaFormat { get; set; }
        public long FileSizeBytes { get; set; }
        public DateTime RecordedAtUtc { get; set; }
        public DateTime DownloadedAtUtc { get; set; }
        public required string Sha256Hash { get; set; }
        public string? VideoCodec { get; set; }
        public string? AudioCodec { get; set; }
        public string? Resolution { get; set; }
        public decimal? FrameRate { get; set; }
        public bool IntegrityVerified { get; set; }
        public DateTime? LastVerifiedAtUtc { get; set; }
        public bool IsPurged { get; set; }
        public DateTime? PurgedAtUtc { get; set; }
        public string? PurgeReason { get; set; }
    }
}
