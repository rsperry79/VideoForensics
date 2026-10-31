namespace VideoForensics.Data.Core.Contracts
{
    /// <summary>Announces that a video's thumbnail has just been produced, so open evidence views can show it.</summary>
    public interface IMediaThumbnailNotifier
    {
        /// <summary>Announces one media item's new thumbnail.</summary>
        Task ThumbnailReadyAsync(Guid mediaItemId, CancellationToken ct);
    }
}
