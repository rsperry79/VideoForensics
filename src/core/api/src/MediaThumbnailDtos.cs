namespace VideoForensics.Core.Api.Contracts
{
    /// <summary>
    /// Pushed over the live hub when a video's thumbnail has just been produced. Carries only the media item ID,
    /// because the client fetches the thumbnail through the existing ticket route when it refreshes.
    /// </summary>
    /// <param name="MediaItemId">The media item whose thumbnail is now available.</param>
    public sealed record ThumbnailReadyDto(Guid MediaItemId);
}
