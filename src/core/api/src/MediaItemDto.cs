namespace VideoForensics.Core.Api.Contracts
{
    /// <summary>
    /// Data transfer object for a media file downloaded from a device.
    /// </summary>
    /// <param name="Id">Unique identifier for the media item.</param>
    /// <param name="DeviceId">The device this media originated from.</param>
    /// <param name="DownloadEventId">The download event that retrieved this media, if applicable. Null if not associated with a tracked download event.</param>
    /// <param name="FileName">Original file name (e.g., "2024-01-15_120000.mp4").</param>
    /// <param name="FilePath">Absolute or relative path where the media file is stored locally.</param>
    /// <param name="MediaFormat">File format (e.g., "mp4", "jpeg", "h264").</param>
    /// <param name="MediaKind">What the row holds (Clip, Snapshot, Timelapse, Still). Part of the server's unique key.</param>
    /// <param name="FileSizeBytes">Size of the media file in bytes.</param>
    /// <param name="RecordedAtUtc">Timestamp when the media was recorded at the source device, in UTC.</param>
    /// <param name="DownloadedAtUtc">Timestamp when the media was downloaded and stored locally, in UTC.</param>
    /// <param name="Sha256Hash">SHA-256 hash of the media file content for integrity verification.</param>
    /// <param name="VideoCodec">Video codec used (e.g., "h264", "h265"), if applicable. Null for non-video media.</param>
    /// <param name="AudioCodec">Audio codec used (e.g., "aac", "mp3"), if applicable. Null if no audio track.</param>
    /// <param name="Resolution">Video resolution (e.g., "1920x1080"), if applicable. Null for non-video media.</param>
    /// <param name="FrameRate">Frame rate in frames per second (e.g., 30.0, 60.0), if applicable. Null for non-video media.</param>
    /// <param name="IntegrityVerified">True if the file has been verified to match its hash at least once.</param>
    /// <param name="LastVerifiedAtUtc">Timestamp of the most recent successful integrity verification, in UTC. Null if never verified.</param>
    /// <param name="IsPurged">True if the media file has been deleted or purged from storage.</param>
    /// <param name="PurgedAtUtc">Timestamp when the file was purged, in UTC. Null if not purged.</param>
    /// <param name="PurgeReason">Reason for purging the file (e.g., "retention_policy", "user_request"). Null if not purged.</param>
    /// <param name="MetadataJson">Additional media metadata in JSON format, provider-specific or forensic-specific.</param>
    /// <param name="ApiSourceHash">Hash of the source API response for change detection; null if not applicable.</param>
    /// <param name="DurationSeconds">Length of a video clip in seconds; null for stills or when unknown. The review timeline needs it to place the clip.</param>
    /// <param name="PreviewPath">Path of the short preview clip, null when none was generated. Clients only use it to know a preview exists.</param>
    public record MediaItemDto(
        Guid Id,
        Guid DeviceId,
        Guid? DownloadEventId,
        string FileName,
        string FilePath,
        string MediaFormat,
        string MediaKind,
        long FileSizeBytes,
        DateTime RecordedAtUtc,
        DateTime DownloadedAtUtc,
        string Sha256Hash,
        string? VideoCodec,
        string? AudioCodec,
        string? Resolution,
        decimal? FrameRate,
        bool IntegrityVerified,
        DateTime? LastVerifiedAtUtc,
        bool IsPurged,
        DateTime? PurgedAtUtc,
        string? PurgeReason,
        string? MetadataJson,
        string? ApiSourceHash,
        double? DurationSeconds = null,
        string? PreviewPath = null
    );
}
