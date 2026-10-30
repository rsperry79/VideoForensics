using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Core.Contracts
{
    /// <summary>Makes sure a video has a short 1 fps preview MP4 for the evidence strip.</summary>
    public interface IMediaPreviewService
    {
        /// <summary>
        /// Ensures a preview sidecar exists for a video and returns true when it is on disk afterwards. An existing
        /// non-empty preview is reused without running ffmpeg. Failures are logged with the clip name and return false.
        /// Never throws except for cancellation.
        /// </summary>
        Task<bool> EnsurePreviewAsync(MediaItem media, CancellationToken ct);
    }
}
