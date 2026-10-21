namespace VideoForensics.Data.Core.Models
{
    /// <summary>
    /// Links a media item to the provider event it was downloaded for. The provider event id is the key shared with
    /// Event.ProviderEventId, so clients can match media to event rows without a download event repository.
    /// </summary>
    /// <param name="MediaItemId">The media item.</param>
    /// <param name="ProviderEventId">The provider's event id for the download that produced the media item.</param>
    public record MediaProviderEventLink(Guid MediaItemId, string ProviderEventId);
}
