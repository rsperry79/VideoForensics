namespace VideoForensics.Client.Common.Contracts
{
    /// <summary>
    /// Resolves browser-loadable URLs (for img/video src) for stored media items. URLs embed a short-lived
    /// access ticket, so callers must not cache them beyond ExpiresAtUtc. Use this to generate src attributes
    /// for img and video tags; the ticket allows the browser to fetch the media without explicitly carrying
    /// an Authorization header (which HTML tags cannot do).
    /// </summary>
    public interface IMediaContentUrlProvider
    {
        /// <summary>
        /// Returns a dictionary mapping media item IDs to their full, browser-loadable content URLs.
        /// Each URL includes an embedded access ticket valid only for that media item and the requesting operator.
        /// </summary>
        /// <param name="mediaItemIds">Collection of media item IDs to request URLs for.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Dictionary keyed by media item ID; missing IDs are omitted from the result.</returns>
        Task<IReadOnlyDictionary<Guid, string>> GetContentUrlsAsync(IReadOnlyCollection<Guid> mediaItemIds, CancellationToken ct);
    }
}
