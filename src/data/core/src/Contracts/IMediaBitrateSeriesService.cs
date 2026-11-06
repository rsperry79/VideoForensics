using VideoForensics.Core.Providers.Core;

namespace VideoForensics.Data.Core.Contracts
{
    /// <summary>The clip's per-second video bitrate: saved when first computed, read back after that.</summary>
    public interface IMediaBitrateSeriesService
    {
        /// <summary>Returns the series for a clip, computing and saving it on first use. Null when the clip is unknown or unreadable.</summary>
        /// <param name="mediaItemId">The clip to read.</param>
        /// <param name="ct">Cancellation token.</param>
        Task<IReadOnlyList<BitrateSecond>?> GetAsync(Guid mediaItemId, CancellationToken ct);
    }
}
