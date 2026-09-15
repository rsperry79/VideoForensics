using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Client.Common.Contracts
{
    /// <summary>
    /// JSON projection of a MediaItem for backup export/import: FilePath (absolute, machine-specific)
    /// is replaced with RelativeMediaPath (relative to the configured media root at export time), so
    /// the backup can be reimported against a different media root on another machine.
    /// </summary>
    public class ExportedMediaItem
    {
        public Guid Id { get; set; }
        public Guid DeviceId { get; set; }
        public Guid? DownloadEventId { get; set; }
        public string FileName { get; set; } = "";
        public string RelativeMediaPath { get; set; } = "";
        public string MediaFormat { get; set; } = "";
        public long FileSizeBytes { get; set; }
        public DateTime RecordedAtUtc { get; set; }
        public DateTime DownloadedAtUtc { get; set; }
        public string Sha256Hash { get; set; } = "";
        public string? VideoCodec { get; set; }
        public string? AudioCodec { get; set; }
        public string? Resolution { get; set; }
        public decimal? FrameRate { get; set; }
        public string? MetadataJson { get; set; }
        public string? ApiSourceHash { get; set; }

        public MediaItem ToMediaItem(string mediaRootPath)
        {
            return new MediaItem
            {
                Id = Id,
                DeviceId = DeviceId,
                DownloadEventId = DownloadEventId,
                FileName = FileName,
                FilePath = Path.Combine(mediaRootPath, RelativeMediaPath),
                MediaFormat = MediaFormat,
                FileSizeBytes = FileSizeBytes,
                RecordedAtUtc = RecordedAtUtc,
                DownloadedAtUtc = DownloadedAtUtc,
                Sha256Hash = Sha256Hash,
                VideoCodec = VideoCodec,
                AudioCodec = AudioCodec,
                Resolution = Resolution,
                FrameRate = FrameRate,
                MetadataJson = MetadataJson,
                ApiSourceHash = ApiSourceHash
            };
        }

        public static ExportedMediaItem FromMediaItem(MediaItem item, string downloadLocationRoot)
        {
            return new ExportedMediaItem
            {
                Id = item.Id,
                DeviceId = item.DeviceId,
                DownloadEventId = item.DownloadEventId,
                FileName = item.FileName,
                RelativeMediaPath = string.IsNullOrEmpty(downloadLocationRoot)
                ? item.FilePath
                : Path.GetRelativePath(downloadLocationRoot, item.FilePath),
                MediaFormat = item.MediaFormat,
                FileSizeBytes = item.FileSizeBytes,
                RecordedAtUtc = item.RecordedAtUtc,
                DownloadedAtUtc = item.DownloadedAtUtc,
                Sha256Hash = item.Sha256Hash,
                VideoCodec = item.VideoCodec,
                AudioCodec = item.AudioCodec,
                Resolution = item.Resolution,
                FrameRate = item.FrameRate,
                MetadataJson = item.MetadataJson,
                ApiSourceHash = item.ApiSourceHash
            };
        }
    }
}
