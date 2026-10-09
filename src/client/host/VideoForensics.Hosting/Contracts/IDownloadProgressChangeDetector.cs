using VideoForensics.Api.Contracts;

namespace VideoForensics.Hosting.Contracts
{
    /// <summary>
    /// Decides whether a sampled download-progress payload needs to be pushed to connected clients.
    /// Kept as a pure function so the send-on-change rule is testable without a hub or timer.
    /// </summary>
    public interface IDownloadProgressChangeDetector
    {
        /// <summary>
        /// Returns true when <paramref name="current"/> differs from <paramref name="previous"/> in any
        /// state field, ignoring <see cref="DownloadProgressDto.Activity"/> (activity lines are
        /// transient and are judged separately by <see cref="ShouldSend"/>).
        /// </summary>
        /// <param name="previous">The last payload sent, or null if nothing has been sent yet.</param>
        /// <param name="current">The freshly sampled payload.</param>
        bool HasChanged(DownloadProgressDto? previous, DownloadProgressDto current);

        /// <summary>
        /// Returns true when the payload should be sent: it changed in state, or it carries activity
        /// lines (which must never be dropped, even when the numbers are identical).
        /// </summary>
        /// <param name="previous">The last payload sent, or null if nothing has been sent yet.</param>
        /// <param name="current">The freshly sampled payload.</param>
        bool ShouldSend(DownloadProgressDto? previous, DownloadProgressDto current);
    }
}
