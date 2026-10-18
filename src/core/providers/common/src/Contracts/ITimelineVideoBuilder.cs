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

    /// <summary>
    /// A video file that covers a real time window on the timeline. The clip covers the half-open interval
    /// [StartedAt, StartedAt + Duration) and replaces any still frames in that window.
    /// </summary>
    /// <param name="VideoPath">Path to an existing video file that ffprobe can read and that has a video stream.</param>
    /// <param name="StartedAt">Wall-clock time of the clip's first frame.</param>
    /// <param name="Duration">How much of the clip is used, from its first frame. Must be positive.</param>
    public sealed record TimelineClip(string VideoPath, DateTimeOffset StartedAt, TimeSpan Duration);

    /// <summary>Input to <see cref="ITimelineVideoBuilder.BuildAsync"/>.</summary>
    /// <param name="Stills">Stills to place on the timeline. Must not be empty.</param>
    /// <param name="OutputPath">Destination MP4 path. An existing file is overwritten.</param>
    /// <param name="Clips">
    /// Optional event clips. Each clip replaces the stills in its window. The timeline spans from the earliest
    /// start to the latest end across stills and clips. Stills are cut around clip windows: a still's effective
    /// interval is its [CapturedAt, CapturedAt + Hold) minus the clip windows, and a remaining piece shorter than
    /// one whole second is dropped. Clips that overlap are resolved in favour of the one that starts first; the
    /// later clip is trimmed to start where the earlier one ends. Each segment is rounded to whole seconds
    /// (away from zero, minimum 1 s), and a clip whose window rounds to 0 s is dropped.
    /// </param>
    public sealed record TimelineVideoRequest(IReadOnlyList<TimelineStill> Stills, string OutputPath, IReadOnlyList<TimelineClip>? Clips = null);

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
