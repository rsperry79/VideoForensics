namespace VideoForensics.Client.Core.Services
{
    /// <summary>One JPEG frame grabbed from a live camera, ready to store as an evidence still.</summary>
    /// <param name="DeviceId">The camera the frame came from.</param>
    /// <param name="MediaRoot">The resolved Media root (the same root snapshots are written under).</param>
    /// <param name="ProviderName">Provider folder name for the camera's account.</param>
    /// <param name="LocationName">Location folder name for the camera.</param>
    /// <param name="DeviceName">Camera name, used in the file name.</param>
    /// <param name="CapturedAtUtc">When the frame was received by the server (UTC).</param>
    /// <param name="Jpeg">The frame's JPEG bytes.</param>
    /// <param name="OperatorName">Who grabbed the frame; written to the action log.</param>
    public sealed record LiveStillRequest(
        Guid DeviceId,
        string MediaRoot,
        string ProviderName,
        string LocationName,
        string DeviceName,
        DateTime CapturedAtUtc,
        byte[] Jpeg,
        string OperatorName);

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
        StorageFailed = 3
    }

    /// <summary>Outcome of storing a live still.</summary>
    public sealed record LiveStillResult(bool Success, Guid? MediaItemId, LiveStillError Error, string? Message)
    {
        /// <summary>A stored (or already stored, identical) still.</summary>
        public static LiveStillResult Ok(Guid mediaItemId) => new(true, mediaItemId, LiveStillError.None, null);

        /// <summary>A still that was not stored.</summary>
        public static LiveStillResult Fail(LiveStillError error, string message) => new(false, null, error, message);
    }

    /// <summary>Stores a JPEG grabbed from a live camera as a <c>Still</c> media item with its file and action-log entry.</summary>
    public interface ILiveStillStore
    {
        /// <summary>
        /// Stores the frame. The instant is the server's capture time truncated to the millisecond, so the same
        /// camera and instant identify one still. A second request for the same instant with identical bytes
        /// returns the existing id; with different bytes it is a conflict.
        /// </summary>
        Task<LiveStillResult> StoreAsync(LiveStillRequest request, CancellationToken ct);
    }
}
