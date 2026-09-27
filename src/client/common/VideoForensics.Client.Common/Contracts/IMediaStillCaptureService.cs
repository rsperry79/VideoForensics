namespace VideoForensics.Client.Common.Contracts
{
    /// <summary>Why a still capture request failed, so callers (the API endpoint) can map it to the right HTTP status.</summary>
    public enum MediaStillCaptureError
    {
        /// <summary>No error; the capture succeeded.</summary>
        None,

        /// <summary>The source media item does not exist.</summary>
        MediaNotFound,

        /// <summary>The request itself was invalid (bad offset, missing reason for a case pin, etc.).</summary>
        InvalidInput,

        /// <summary>Extraction (server or client) failed for a reason outside the caller's input.</summary>
        ExtractionFailed
    }

    /// <summary>Forensic provenance for a captured still, returned to the caller after a successful capture.</summary>
    public class MediaStillInfo
    {
        /// <summary>The derived still's own media item ID.</summary>
        public Guid Id { get; set; }

        /// <summary>The source video's media item ID the still was extracted from.</summary>
        public Guid SourceMediaItemId { get; set; }

        /// <summary>Offset in milliseconds into the source media at which the frame was captured.</summary>
        public long FrameOffsetMs { get; set; }

        /// <summary>SHA-256 hash of the still's own file content.</summary>
        public required string Sha256Hash { get; set; }

        /// <summary>SHA-256 of the source media file's content, recomputed at capture time.</summary>
        public required string SourceSha256AtCapture { get; set; }

        /// <summary>Identity of the operator who captured this still.</summary>
        public required string CapturedByOperator { get; set; }

        /// <summary>How the still's pixel data was obtained ("Server" or "Client").</summary>
        public required string CaptureMethod { get; set; }

        /// <summary>When this still was captured (UTC).</summary>
        public DateTime CreatedAtUtc { get; set; }

        /// <summary>True if the still was pinned to a case as part of this capture.</summary>
        public bool PinnedToCase { get; set; }
    }

    /// <summary>Result of a still-capture attempt.</summary>
    public class MediaStillCaptureResult
    {
        /// <summary>True if the capture succeeded (Error is <see cref="MediaStillCaptureError.None"/>).</summary>
        public bool Success => Error == MediaStillCaptureError.None;

        /// <summary>The failure reason, or <see cref="MediaStillCaptureError.None"/> on success.</summary>
        public MediaStillCaptureError Error { get; set; }

        /// <summary>Human-readable error detail, set when Error is not None.</summary>
        public string? ErrorMessage { get; set; }

        /// <summary>The captured still's provenance, set only on success.</summary>
        public MediaStillInfo? Still { get; set; }

        /// <summary>Builds a successful result.</summary>
        public static MediaStillCaptureResult Ok(MediaStillInfo still) =>
            new() { Error = MediaStillCaptureError.None, Still = still };

        /// <summary>Builds a failed result.</summary>
        public static MediaStillCaptureResult Fail(MediaStillCaptureError error, string message) =>
            new() { Error = error, ErrorMessage = message };
    }

    /// <summary>
    /// Orchestrates capturing a video frame as a new, independently-hashed "still" media item for
    /// evidence purposes (Evidence "grab still with hash"). The original source file is never
    /// modified. Implemented locally (server-tier hosts) by an orchestrator over
    /// IMediaFrameExtractor/IMediaItemRepository/IMediaStillRepository, and remotely (MAUI) by an
    /// HTTP-backed Remote* class calling POST /api/v1/media/{id}/stills - both sides of the
    /// client/server split share this one interface (CLAUDE.md's "interface names don't change" rule).
    /// </summary>
    public interface IMediaStillCaptureService
    {
        /// <summary>
        /// Captures a still frame from the given source video at <paramref name="frameOffsetMs"/>.
        /// Prefers server-side extraction from the original file; falls back to
        /// <paramref name="clientPngBase64"/> (a client-captured PNG) only when server-side
        /// extraction is unavailable or fails. Optionally pins the resulting still to
        /// <paramref name="caseId"/> with <paramref name="pinReason"/> (required together).
        /// </summary>
        /// <param name="sourceMediaItemId">The source video's media item ID.</param>
        /// <param name="frameOffsetMs">Offset in milliseconds into the source media (must be &gt;= 0).</param>
        /// <param name="clientPngBase64">Base64-encoded PNG bytes from a client-side capture fallback, or null.</param>
        /// <param name="caseId">Optional case to pin the resulting still to.</param>
        /// <param name="pinReason">Reason for the case pin; required when caseId is set.</param>
        /// <param name="capturedBy">Identity of the capturing operator (ignored client-side; the server derives the actor from the bearer token).</param>
        /// <param name="ct">Cancellation token.</param>
        Task<MediaStillCaptureResult> CaptureStillAsync(
            Guid sourceMediaItemId,
            long frameOffsetMs,
            string? clientPngBase64,
            Guid? caseId,
            string? pinReason,
            string capturedBy,
            CancellationToken ct);
    }
}
