namespace VideoForensics.Core.Providers.Common.Contracts
{
    /// <summary>
    /// Runs a media pull for an account's devices in 30-day batches, advances the watermark only past clean batches, and
    /// counts only media that was actually pulled. Provider-neutral: the provider is reached through <see cref="IMediaDownloadService"/>.
    /// </summary>
    public interface IEventMediaPuller
    {
        /// <summary>Pulls the request's window and returns the honest counts. Cancellation propagates; the watermark keeps the progress made.</summary>
        Task<MediaPullResult> PullAsync(IMediaDownloadService downloads, IMediaPullProgressStore progress, MediaPullRequest request, CancellationToken cancellationToken);
    }
}
