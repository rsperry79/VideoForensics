using VideoForensics.Client.Common.Contracts;
using VideoForensics.Core.Api.Contracts;

namespace VideoForensics.Core.Api.Contracts
{
    /// <summary>Extension methods for mapping MediaStillInfo to/from MediaStillDto.</summary>
    public static class MediaStillDtoMapping
    {
        /// <summary>Converts a MediaStillInfo (service-layer capture result) to a MediaStillDto.</summary>
        public static MediaStillDto ToDto(this VideoForensics.Client.Common.Contracts.MediaStillInfo info)
        {
            return new MediaStillDto(
                Id: info.Id,
                SourceMediaItemId: info.SourceMediaItemId,
                FrameOffsetMs: info.FrameOffsetMs,
                Sha256Hash: info.Sha256Hash,
                SourceSha256AtCapture: info.SourceSha256AtCapture,
                CapturedByOperator: info.CapturedByOperator,
                CaptureMethod: info.CaptureMethod,
                CreatedAtUtc: info.CreatedAtUtc,
                PinnedToCase: info.PinnedToCase
            );
        }

        /// <summary>Converts a MediaStillDto back to a MediaStillInfo.</summary>
        public static VideoForensics.Client.Common.Contracts.MediaStillInfo ToDomain(this MediaStillDto dto)
        {
            return new VideoForensics.Client.Common.Contracts.MediaStillInfo
            {
                Id = dto.Id,
                SourceMediaItemId = dto.SourceMediaItemId,
                FrameOffsetMs = dto.FrameOffsetMs,
                Sha256Hash = dto.Sha256Hash,
                SourceSha256AtCapture = dto.SourceSha256AtCapture,
                CapturedByOperator = dto.CapturedByOperator,
                CaptureMethod = dto.CaptureMethod,
                CreatedAtUtc = dto.CreatedAtUtc,
                PinnedToCase = dto.PinnedToCase
            };
        }
    }
}
