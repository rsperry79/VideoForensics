using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Core.Api.Contracts
{
    /// <summary>Maps the SnapshotCapture entity to its wire DTO.</summary>
    public static class SnapshotCaptureDtoMapping
    {
        /// <summary>Converts a SnapshotCapture entity to a SnapshotCaptureDto.</summary>
        public static SnapshotCaptureDto ToDto(this SnapshotCapture entity) =>
            new(entity.Id, entity.DeviceId, entity.CapturedAtUtc, entity.Trigger, entity.HttpStatus, entity.IsCurrent, entity.MediaFilePath, entity.DeviceHealthId);
    }
}
