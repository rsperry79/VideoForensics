namespace VideoForensics.Api.Contracts
{
    /// <summary>
    /// Data transfer object for an event detected by a device, independent of whether it was downloaded.
    /// </summary>
    /// <param name="Id">Unique identifier for the event.</param>
    /// <param name="DeviceId">The device that detected this event.</param>
    /// <param name="ProviderEventId">The provider's unique identifier for this event (e.g., Ring event ID).</param>
    /// <param name="EventType">Type of event (e.g., "motion", "person", "package").</param>
    /// <param name="OccurredAtUtc">Timestamp when the event occurred at the device, in UTC.</param>
    /// <param name="SnapshotUrl">URL to a snapshot image captured during the event, if available.</param>
    /// <param name="MetadataJson">Additional event metadata in JSON format, provider-specific.</param>
    /// <param name="DiscoveredAtUtc">Timestamp when this event was first discovered/synced, in UTC.</param>
    /// <param name="DownloadedAtUtc">Timestamp when associated media was downloaded, in UTC. Null if no media has been downloaded.</param>
    /// <param name="ApiSourceHash">Hash of the source API response for change detection; null if not applicable.</param>
    /// <param name="EventIntegrityHash">Hash value for event integrity verification; null if not set.</param>
    public record EventDto(
        Guid Id,
        Guid DeviceId,
        string ProviderEventId,
        string EventType,
        DateTime OccurredAtUtc,
        string? SnapshotUrl,
        string? MetadataJson,
        DateTime DiscoveredAtUtc,
        DateTime? DownloadedAtUtc,
        string? ApiSourceHash,
        string? EventIntegrityHash
    );

    /// <summary>
    /// Data transfer object for a legal hold that exempts a media item from retention-policy auto-deletion.
    /// </summary>
    /// <param name="Id">Unique identifier for this legal hold.</param>
    /// <param name="MediaItemId">The media item this legal hold protects from deletion.</param>
    /// <param name="Reason">The reason or justification for placing this legal hold (e.g., "pending investigation").</param>
    /// <param name="CreatedBy">Identifier of the user or system that placed this legal hold.</param>
    /// <param name="CreatedAtUtc">Timestamp when the legal hold was placed, in UTC.</param>
    /// <param name="ReleasedBy">Identifier of the user or system that released this legal hold, if applicable. Null if still active.</param>
    /// <param name="ReleasedAtUtc">Timestamp when the legal hold was released, in UTC. Null if still active.</param>
    /// <param name="ReleaseReason">Reason for releasing the legal hold, if applicable. Null if still active.</param>
    public record LegalHoldDto(
        Guid Id,
        Guid MediaItemId,
        string Reason,
        string CreatedBy,
        DateTime CreatedAtUtc,
        string? ReleasedBy,
        DateTime? ReleasedAtUtc,
        string? ReleaseReason
    );

    /// <summary>
    /// Request DTO for placing a legal hold on a media item.
    /// </summary>
    /// <param name="MediaItemId">The ID of the media item to place a legal hold on.</param>
    /// <param name="Reason">The reason or justification for the legal hold (e.g., "pending investigation", "chain of custody").</param>
    public record PlaceLegalHoldRequestDto(
        Guid MediaItemId,
        string Reason
    );

    /// <summary>
    /// Request DTO for releasing an active legal hold.
    /// </summary>
    /// <param name="LegalHoldId">The ID of the legal hold to release.</param>
    /// <param name="ReleaseReason">The reason for releasing the legal hold (e.g., "investigation concluded").</param>
    public record ReleaseLegalHoldRequestDto(
        Guid LegalHoldId,
        string ReleaseReason
    );

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
