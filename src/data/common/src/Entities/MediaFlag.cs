namespace VideoForensics.Data.Common.Entities
{
    /// <summary>
    /// Review state for one detected anomaly. The detection result stays on the media item (RedFlags); this row only
    /// records whether an operator has looked at it. Purging or deleting the media item keeps the flag: the link is cleared
    /// on delete and the device and window identify the flag afterwards.
    /// </summary>
    public class MediaFlag
    {
        public Guid Id { get; set; }
        public MediaFlagType FlagType { get; set; }
        public Guid DeviceId { get; set; }

        /// <summary>The flagged clip. Null for flags without a clip (RSSI) and after the clip row was deleted.</summary>
        public Guid? MediaItemId { get; set; }

        public DateTime WindowStartUtc { get; set; }
        public DateTime WindowEndUtc { get; set; }
        public DateTime RaisedAtUtc { get; set; }
        public MediaFlagSeverity Severity { get; set; } = MediaFlagSeverity.Warn;
        public MediaFlagStatus Status { get; set; } = MediaFlagStatus.Unreviewed;
        public string? ReviewedBy { get; set; }
        public DateTime? ReviewedAtUtc { get; set; }
        public string? ReviewNote { get; set; }

        /// <summary>The originating notice, if one was raised with the flag.</summary>
        public Guid? NoticeId { get; set; }

        /// <summary>Optimistic concurrency token. Incremented on every update; SQLite has no native row version.</summary>
        public int RowVersion { get; set; }

        /// <summary>
        /// How many std devs the flagged worst reading sat from the camera's all-time mean, when the flag was raised. Null
        /// for flags with no sigma (bit rate collapse) and for rows raised before the value was stored.
        /// </summary>
        public double? DeviationSigma { get; set; }

        /// <summary>
        /// Event type shown with the flag. Not stored: the repository fills it on read. A flag with a clip takes its clip's
        /// download event type; a clip-less RSSI flag takes the type of a clip on its camera within the review search pad.
        /// </summary>
        public string? EventType { get; set; }
    }
}
