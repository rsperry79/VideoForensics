namespace VideoForensics.Api.Contracts
{
    /// <summary>
    /// Request DTO for capturing a still frame from a video's source media item ("grab still with hash").
    /// </summary>
    /// <param name="FrameOffsetMs">Offset in milliseconds into the source media at which to capture the frame (must be &gt;= 0).</param>
    /// <param name="PngBase64">Base64-encoded PNG bytes for a client-captured still. Only used as a fallback when server-side extraction is unavailable; ignored otherwise.</param>
    /// <param name="CaseId">Optional case to pin the resulting still to; requires <see cref="Reason"/>.</param>
    /// <param name="Reason">Reason for pinning to the case (required, max 1024 chars, only when <see cref="CaseId"/> is set).</param>
    public record CaptureStillRequestDto(
        long FrameOffsetMs,
        string? PngBase64,
        Guid? CaseId,
        string? Reason
    );

    /// <summary>
    /// Data transfer object describing a captured still and its forensic provenance.
    /// </summary>
    /// <param name="Id">The derived still's own media item ID.</param>
    /// <param name="SourceMediaItemId">The source video's media item ID the still was extracted from.</param>
    /// <param name="FrameOffsetMs">Offset in milliseconds into the source media at which the frame was captured.</param>
    /// <param name="Sha256Hash">SHA-256 hash of the still's own file content.</param>
    /// <param name="SourceSha256AtCapture">SHA-256 of the source media file's content, recomputed at capture time.</param>
    /// <param name="CapturedByOperator">Identity of the operator who captured this still.</param>
    /// <param name="CaptureMethod">How the still's pixel data was obtained ("Server" or "Client").</param>
    /// <param name="CreatedAtUtc">When this still was captured (UTC).</param>
    /// <param name="PinnedToCase">True if the still was pinned to a case as part of this capture.</param>
    public record MediaStillDto(
        Guid Id,
        Guid SourceMediaItemId,
        long FrameOffsetMs,
        string Sha256Hash,
        string SourceSha256AtCapture,
        string CapturedByOperator,
        string CaptureMethod,
        DateTime CreatedAtUtc,
        bool PinnedToCase
    );

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
