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

        /// <summary>
        /// File name of the stored thumbnail, relative to the clip's own folder (e.g. "clip.thumb.png").
        /// Null when no thumbnail exists yet, so the backfill can select exactly the rows that need one.
        /// </summary>
        public string? ThumbnailPath { get; set; }
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

        /// <summary>Lighting class from the video: "Ir", "ColorNight" or "Day"; null if not analysed.</summary>
        public string? LightingMode { get; set; }

        /// <summary>Mean colour saturation (SATAVG) of the video.</summary>
        public double? MeanSaturation { get; set; }

        /// <summary>Mean luma (YAVG, 0-255) of the video.</summary>
        public double? MeanLuma { get; set; }

        /// <summary>Audio noise floor in dB at the end of the clip.</summary>
        public double? NoiseFloorDb { get; set; }

        /// <summary>Clipped audio peak count (cumulative).</summary>
        public int? ClippedPeakCount { get; set; }

        /// <summary>Silent audio gaps at or above the minimum dropout length.</summary>
        public int? SilentGapCount { get; set; }

        /// <summary>Fraction of the clip that is silent.</summary>
        public double? SilenceFraction { get; set; }

        /// <summary>Comma-separated red flags raised for this clip; empty when none.</summary>
        public string? RedFlags { get; set; }

        /// <summary>True when the red flags point to jamming.</summary>
        public bool? JammingSuspected { get; set; }

        /// <summary>Version of the baseline snapshot this clip was judged against.</summary>
        public int? BaselineVersion { get; set; }
    }
}
