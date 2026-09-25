namespace VideoForensics.Api.Contracts
{
    /// <summary>
    /// Request to issue media access tickets for a batch of media items.
    /// </summary>
    /// <param name="MediaItemIds">IDs of the media items to request tickets for.</param>
    public record MediaTicketRequestDto(IReadOnlyList<Guid> MediaItemIds);

    /// <summary>
    /// Response containing a media access ticket with its content URL and expiration time.
    /// </summary>
    /// <param name="MediaItemId">The media item this ticket grants access to.</param>
    /// <param name="ContentUrl">Server-relative URL where the media content can be retrieved: /api/v1/media/{id}/content?ticket={ticket}</param>
    /// <param name="ExpiresAtUtc">UTC timestamp when this ticket expires and becomes invalid.</param>
    public record MediaTicketDto(Guid MediaItemId, string ContentUrl, DateTime ExpiresAtUtc);

    /// <summary>
    /// Helper methods and constants for media content routes and URLs.
    /// </summary>
    public static class MediaContentRoutes
    {
        /// <summary>Maximum number of media items that can be requested in a single ticket request.</summary>
        public const int MaxTicketsPerRequest = 200;

        /// <summary>
        /// Builds a server-relative URL for accessing media content with an access ticket.
        /// </summary>
        /// <param name="mediaItemId">The ID of the media item to access.</param>
        /// <param name="ticketToken">The access ticket token.</param>
        /// <returns>Server-relative URL in the format: /api/v1/media/{id}/content?ticket={token}</returns>
        public static string ContentUrl(Guid mediaItemId, string ticketToken)
        {
            return $"/api/v1/media/{mediaItemId}/content?ticket={Uri.EscapeDataString(ticketToken)}";
        }
    }
}
