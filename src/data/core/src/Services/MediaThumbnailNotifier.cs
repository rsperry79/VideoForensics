namespace VideoForensics.Data.Core.Services
{
    /// <summary>Announces that a video's thumbnail has just been produced, so open evidence views can show it.</summary>
    public interface IMediaThumbnailNotifier
    {
        /// <summary>Announces one media item's new thumbnail.</summary>
        Task ThumbnailReadyAsync(Guid mediaItemId, CancellationToken ct);
    }

    /// <summary>Default for hosts with no live audience (console, MCP, tests): announcing does nothing.</summary>
    public sealed class NullMediaThumbnailNotifier : IMediaThumbnailNotifier
    {
        /// <inheritdoc />
        public Task ThumbnailReadyAsync(Guid mediaItemId, CancellationToken ct)
        {
            return Task.CompletedTask;
        }
    }
}
