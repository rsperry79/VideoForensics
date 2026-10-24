using VideoForensics.Data.Core.Contracts;

namespace VideoForensics.Data.Core.Services
{
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
