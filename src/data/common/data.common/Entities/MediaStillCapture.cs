namespace VideoForensics.Data.Common.Entities
{
    /// <summary>How a derived still's pixel data was obtained.</summary>
    public enum MediaStillCaptureMethod
    {
        /// <summary>Extracted server-side from the original source file at the requested timestamp (deterministic; preferred).</summary>
        Server = 0,

        /// <summary>Captured client-side (e.g. a MAUI canvas snapshot) and uploaded, because server-side extraction was unavailable.</summary>
        Client = 1
    }

    /// <summary>
    /// Forensic provenance for a derived "still" media item captured from a video frame. One row per
    /// derived <see cref="MediaItem"/> (1:1 via <see cref="MediaItemId"/>). The still itself is stored
    /// as an ordinary <see cref="MediaItem"/> row (so it participates in Evidence, integrity records,
    /// and case pinning exactly like any other media file); this table holds only the
    /// forensic-specific chain-of-custody fields that don't belong on every MediaItem.
    /// </summary>
    public class MediaStillCapture
    {
        /// <summary>The derived still's MediaItem ID (primary key here, foreign key to MediaItem).</summary>
        public Guid MediaItemId { get; set; }

        /// <summary>The source video's MediaItem ID that this still was extracted from.</summary>
        public Guid SourceMediaItemId { get; set; }

        /// <summary>Offset in milliseconds into the source media at which the frame was captured.</summary>
        public long FrameOffsetMs { get; set; }

        /// <summary>SHA-256 of the source media file's content, recomputed at the moment of capture.</summary>
        public required string SourceSha256AtCapture { get; set; }

        /// <summary>Identity (operator ID or username) of the operator who captured this still.</summary>
        public required string CapturedByOperator { get; set; }

        /// <summary>How the still's pixel data was obtained.</summary>
        public MediaStillCaptureMethod CaptureMethod { get; set; }

        /// <summary>When this still was captured (UTC).</summary>
        public DateTime CreatedAtUtc { get; set; }
    }
}
