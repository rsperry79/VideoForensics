namespace VideoForensics.Core.Providers.Common.Contracts
{
    /// <summary>
    /// One still image placed on a timeline. The still is shown from <see cref="CapturedAt"/> for
    /// <see cref="Hold"/>, i.e. it covers the half-open interval [CapturedAt, CapturedAt + Hold).
    /// </summary>
    /// <param name="ImagePath">Path to an existing image file (PNG, JPEG, or any format ffmpeg reads).</param>
    /// <param name="CapturedAt">When the still was captured. Stills are ordered by this value.</param>
    /// <param name="Hold">How long the still is shown. Must be positive.</param>
    public sealed record TimelineStill(string ImagePath, DateTimeOffset CapturedAt, TimeSpan Hold);

    /// <summary>Input to <see cref="ITimelineVideoBuilder.BuildAsync"/>.</summary>
    /// <param name="Stills">Stills to place on the timeline. Must not be empty.</param>
    /// <param name="OutputPath">Destination MP4 path. An existing file is overwritten.</param>
    public sealed record TimelineVideoRequest(IReadOnlyList<TimelineStill> Stills, string OutputPath);

    /// <summary>Outcome of a timeline build. ffmpeg failures are reported here, not thrown.</summary>
    /// <param name="Success">True if the MP4 was written.</param>
    /// <param name="OutputPath">The written MP4 path when <paramref name="Success"/> is true; otherwise null.</param>
    /// <param name="ErrorMessage">Failure reason when <paramref name="Success"/> is false; otherwise null.</param>
    /// <param name="FrameCount">Number of 1 fps frames in the video: the sum of each still's whole-second hold.</param>
    public sealed record TimelineVideoResult(bool Success, string? OutputPath, string? ErrorMessage, int FrameCount);

    /// <summary>
    /// Builds a single MP4 from a list of timed still images, at a fixed 1 frame per second.
    /// Provider-agnostic: it knows nothing about where the stills came from.
    /// </summary>
    public interface ITimelineVideoBuilder
    {
        /// <summary>
        /// Writes an H.264 yuv420p MP4 at 1 fps in which each still is repeated for its hold.
        /// </summary>
        /// <param name="request">The stills and output path.</param>
        /// <param name="ct">Cancels the build; the ffmpeg process is killed.</param>
        /// <returns>The build result. ffmpeg failures and timeouts return Success = false.</returns>
        /// <exception cref="ArgumentException">
        /// The still list is empty, a still's image file does not exist, a hold is not positive, or the output path is blank.
        /// </exception>
        /// <exception cref="OperationCanceledException">The caller cancelled the build.</exception>
        Task<TimelineVideoResult> BuildAsync(TimelineVideoRequest request, CancellationToken ct);
    }
}
