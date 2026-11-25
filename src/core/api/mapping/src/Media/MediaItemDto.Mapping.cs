using VideoForensics.Core.Api.Contracts;

namespace VideoForensics.Core.Api.Contracts
{
    /// <summary>Extension methods for mapping MediaItem entities to/from MediaItemDtos.</summary>
    public static class MediaItemDtoMapping
    {
        /// <summary>
        /// Converts a MediaItem entity to a MediaItemDto.
        /// </summary>
        public static MediaItemDto ToDto(this VideoForensics.Data.Common.Entities.MediaItem entity)
        {
            return new MediaItemDto(
                Id: entity.Id,
                DeviceId: entity.DeviceId,
                DownloadEventId: entity.DownloadEventId,
                FileName: entity.FileName,
                FilePath: entity.FilePath,
                MediaFormat: entity.MediaFormat,
                MediaKind: entity.MediaKind,
                FileSizeBytes: entity.FileSizeBytes,
                RecordedAtUtc: entity.RecordedAtUtc,
                DownloadedAtUtc: entity.DownloadedAtUtc,
                Sha256Hash: entity.Sha256Hash,
                VideoCodec: entity.VideoCodec,
                AudioCodec: entity.AudioCodec,
                Resolution: entity.Resolution,
                FrameRate: entity.FrameRate,
                IntegrityVerified: entity.IntegrityVerified,
                LastVerifiedAtUtc: entity.LastVerifiedAtUtc,
                IsPurged: entity.IsPurged,
                PurgedAtUtc: entity.PurgedAtUtc,
                PurgeReason: entity.PurgeReason,
                MetadataJson: entity.MetadataJson,
                ApiSourceHash: entity.ApiSourceHash,
                DurationSeconds: entity.DurationSeconds,
                PreviewPath: entity.PreviewPath
            );
        }

        /// <summary>
        /// Converts a MediaItemDto to a MediaItem entity.
        /// </summary>
        public static VideoForensics.Data.Common.Entities.MediaItem ToDomain(this MediaItemDto dto)
        {
            return new VideoForensics.Data.Common.Entities.MediaItem
            {
                Id = dto.Id,
                DeviceId = dto.DeviceId,
                DownloadEventId = dto.DownloadEventId,
                FileName = dto.FileName,
                FilePath = dto.FilePath,
                MediaFormat = dto.MediaFormat,
                MediaKind = dto.MediaKind,
                FileSizeBytes = dto.FileSizeBytes,
                RecordedAtUtc = dto.RecordedAtUtc,
                DownloadedAtUtc = dto.DownloadedAtUtc,
                Sha256Hash = dto.Sha256Hash,
                VideoCodec = dto.VideoCodec,
                AudioCodec = dto.AudioCodec,
                Resolution = dto.Resolution,
                FrameRate = dto.FrameRate,
                IntegrityVerified = dto.IntegrityVerified,
                LastVerifiedAtUtc = dto.LastVerifiedAtUtc,
                IsPurged = dto.IsPurged,
                PurgedAtUtc = dto.PurgedAtUtc,
                PurgeReason = dto.PurgeReason,
                MetadataJson = dto.MetadataJson,
                ApiSourceHash = dto.ApiSourceHash,
                DurationSeconds = dto.DurationSeconds,
                PreviewPath = dto.PreviewPath
            };
        }
    }
}
