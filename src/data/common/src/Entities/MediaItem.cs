namespace VideoForensics.Data.Common.Entities
{
    /// <summary>A media file downloaded from a device.</summary>
    public class MediaItem
    {
        public Guid Id { get; set; }
        public Guid DeviceId { get; set; }
        public Guid? DownloadEventId { get; set; }
        public Guid? MediaItemDetectionId { get; set; }
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
        public string? MetadataJson { get; set; }
        public string? ApiSourceHash { get; set; }

        /// <summary>Video bitrate in bits per second, from ffprobe at download time; null if never analyzed.</summary>
        public long? VideoBitrateBps { get; set; }

        /// <summary>Duration of the file in seconds, from ffprobe at download time; null if never analyzed.</summary>
        public double? DurationSeconds { get; set; }

        /// <summary>True when <see cref="VideoBitrateBps"/> was derived from file size and duration because the stream reported none.</summary>
        public bool? BitrateEstimated { get; set; }

        /// <summary>When the bitrate analysis ran; null if it has not been analyzed.</summary>
        public DateTime? BitrateAnalyzedAtUtc { get; set; }
    }
}
