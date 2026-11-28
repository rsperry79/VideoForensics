namespace VideoForensics.Client.Common.Contracts
{
    /// <summary>Why a live still was not stored.</summary>
    public enum LiveStillError
    {
        /// <summary>No error.</summary>
        None = 0,

        /// <summary>The request is not a usable JPEG, or a field is missing.</summary>
        InvalidInput = 1,

        /// <summary>Another frame from the same camera at the same millisecond has different bytes.</summary>
        Conflict = 2,

        /// <summary>The file or its row could not be written.</summary>
        StorageFailed = 3,

        /// <summary>The camera is unknown or not visible to any linked account.</summary>
        NotFound = 4
    }

    /// <summary>Outcome of storing a live still.</summary>
    public sealed record LiveStillResult(bool Success, Guid? MediaItemId, LiveStillError Error, string? Message)
    {
        /// <summary>A stored (or already stored, identical) still.</summary>
        public static LiveStillResult Ok(Guid mediaItemId) => new(true, mediaItemId, LiveStillError.None, null);

        /// <summary>A still that was not stored.</summary>
        public static LiveStillResult Fail(LiveStillError error, string message) => new(false, null, error, message);
    }

    /// <summary>
    /// Stores a JPEG grabbed from a live camera. The server implementation resolves the camera's account and
    /// location and writes the still; a remote client posts it to the upload endpoint.
    /// </summary>
    public interface ILiveStillCaptureService
    {
        /// <summary>Stores one frame from a camera. The operator is recorded in the action log.</summary>
        Task<LiveStillResult> CaptureAsync(Guid deviceId, byte[] jpeg, string operatorId, CancellationToken ct);
    }
}
