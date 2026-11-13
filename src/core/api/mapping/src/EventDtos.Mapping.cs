using VideoForensics.Core.Api.Contracts;

namespace VideoForensics.Core.Api.Contracts
{
    /// <summary>Extension methods for mapping Event entities to/from EventDtos.</summary>
    public static class EventDtoMapping
    {
        /// <summary>
        /// Converts an Event entity to an EventDto.
        /// </summary>
        public static EventDto ToDto(this VideoForensics.Data.Common.Entities.Event entity)
        {
            return new EventDto(
                Id: entity.Id,
                DeviceId: entity.DeviceId,
                ProviderEventId: entity.ProviderEventId,
                EventType: entity.EventType,
                OccurredAtUtc: entity.OccurredAtUtc,
                SnapshotUrl: entity.SnapshotUrl,
                MetadataJson: entity.MetadataJson,
                DiscoveredAtUtc: entity.DiscoveredAtUtc,
                DownloadedAtUtc: entity.DownloadedAtUtc,
                ApiSourceHash: entity.ApiSourceHash,
                EventIntegrityHash: entity.EventIntegrityHash
            );
        }

        /// <summary>
        /// Converts an EventDto to an Event entity.
        /// </summary>
        public static VideoForensics.Data.Common.Entities.Event ToDomain(this EventDto dto)
        {
            return new VideoForensics.Data.Common.Entities.Event
            {
                Id = dto.Id,
                DeviceId = dto.DeviceId,
                ProviderEventId = dto.ProviderEventId,
                EventType = dto.EventType,
                OccurredAtUtc = dto.OccurredAtUtc,
                SnapshotUrl = dto.SnapshotUrl,
                MetadataJson = dto.MetadataJson,
                DiscoveredAtUtc = dto.DiscoveredAtUtc,
                DownloadedAtUtc = dto.DownloadedAtUtc,
                ApiSourceHash = dto.ApiSourceHash,
                EventIntegrityHash = dto.EventIntegrityHash
            };
        }
    }

    /// <summary>Extension methods for mapping LegalHold entities to/from LegalHoldDtos.</summary>
    public static class LegalHoldDtoMapping
    {
        /// <summary>
        /// Converts a LegalHold entity to a LegalHoldDto.
        /// </summary>
        public static LegalHoldDto ToDto(this VideoForensics.Data.Common.Entities.LegalHold entity)
        {
            return new LegalHoldDto(
                Id: entity.Id,
                MediaItemId: entity.MediaItemId,
                Reason: entity.Reason,
                CreatedBy: entity.CreatedBy,
                CreatedAtUtc: entity.CreatedAtUtc,
                ReleasedBy: entity.ReleasedBy,
                ReleasedAtUtc: entity.ReleasedAtUtc,
                ReleaseReason: entity.ReleaseReason
            );
        }

        /// <summary>
        /// Converts a LegalHoldDto to a LegalHold entity.
        /// </summary>
        public static VideoForensics.Data.Common.Entities.LegalHold ToDomain(this LegalHoldDto dto)
        {
            return new VideoForensics.Data.Common.Entities.LegalHold
            {
                Id = dto.Id,
                MediaItemId = dto.MediaItemId,
                Reason = dto.Reason,
                CreatedBy = dto.CreatedBy,
                CreatedAtUtc = dto.CreatedAtUtc,
                ReleasedBy = dto.ReleasedBy,
                ReleasedAtUtc = dto.ReleasedAtUtc,
                ReleaseReason = dto.ReleaseReason
            };
        }
    }
}
