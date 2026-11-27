using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Core.Api.Contracts
{
    /// <summary>Maps the SnapshotCapture entity to its wire DTO.</summary>
    public static class SnapshotCaptureDtoMapping
    {
        /// <summary>Converts a SnapshotCapture entity to a SnapshotCaptureDto.</summary>
        public static SnapshotCaptureDto ToDto(this SnapshotCapture entity) =>
            new(entity.Id, entity.DeviceId, entity.CapturedAtUtc, entity.Trigger, entity.HttpStatus, entity.IsCurrent, entity.MediaFilePath, entity.DeviceHealthId, ToSkipReasonDto(entity.SkipReason));

        /// <summary>Converts a SnapshotCaptureDto back to a SnapshotCapture entity, keeping its id. Uses the stored-row path, so the invariants are not re-checked.</summary>
        public static SnapshotCapture ToDomain(this SnapshotCaptureDto dto) =>
            SnapshotCapture.Rehydrate(dto.Id, dto.DeviceId, dto.CapturedAtUtc, dto.Trigger, dto.HttpStatus, dto.IsCurrent, dto.MediaFilePath, dto.DeviceHealthId, ToDomainSkipReason(dto.SkipReason));

        private static SnapshotSkipReasonDto? ToSkipReasonDto(SnapshotSkipReason? reason) => reason switch
        {
            null => null,
            SnapshotSkipReason.Offline => SnapshotSkipReasonDto.Offline,
            SnapshotSkipReason.IdenticalFrame => SnapshotSkipReasonDto.IdenticalFrame,
            _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, "Unknown snapshot skip reason."),
        };

        private static SnapshotSkipReason? ToDomainSkipReason(SnapshotSkipReasonDto? reason) => reason switch
        {
            null => null,
            SnapshotSkipReasonDto.Offline => SnapshotSkipReason.Offline,
            SnapshotSkipReasonDto.IdenticalFrame => SnapshotSkipReason.IdenticalFrame,
            _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, "Unknown snapshot skip reason."),
        };
    }
}
