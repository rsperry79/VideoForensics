using Microsoft.AspNetCore.DataProtection;

using System.Text.Json;

namespace VideoForensics.Hosting
{
    /// <summary>
    /// Issues and validates short-lived, media-specific access tickets for use in URL query strings.
    /// Img/video HTML tags cannot carry HTTP Authorization headers, so authenticated access to
    /// media content must use an alternative: a short-lived signed ticket embedded in the URL as
    /// a query parameter (e.g., /api/v1/media/{id}/content?ticket=...). The server hands the ticket
    /// to the client in a response; the client embeds it in the src attribute; the browser loads
    /// the media without needing to understand auth. Tickets are scoped to a single media item
    /// (a ticket for media A cannot be used to access media B) and attributed to the operator who
    /// requested it (audit trail), and they expire quickly (10 minutes) so tokens leaked or
    /// reused remain risk-bounded.
    /// </summary>
    public interface IMediaAccessTicketService
    {
        /// <summary>Lifetime of issued tickets (10 minutes).</summary>
        TimeSpan TicketLifetime { get; }

        /// <summary>Issues a ticket valid only for this media item, attributed to this operator.</summary>
        MediaAccessTicket Issue(Guid mediaItemId, Guid operatorId);

        /// <summary>Returns the operator id the ticket was issued to if it is valid, unexpired, and for this exact media item; otherwise null.</summary>
        Guid? Validate(string? ticket, Guid mediaItemId);
    }

    public sealed record MediaAccessTicket(string Token, DateTime ExpiresAtUtc);

    public class MediaAccessTicketService : IMediaAccessTicketService
    {
        private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);
        private readonly IDataProtector _protector;
        private readonly TimeProvider _timeProvider;

        private record MediaAccessPayload(Guid MediaItemId, Guid OperatorId, DateTime ExpiresAtUtc);

        public TimeSpan TicketLifetime => Lifetime;

        public MediaAccessTicketService(IDataProtectionProvider provider, TimeProvider? timeProvider = null)
        {
            _protector = provider.CreateProtector("VideoForensics.MediaAccessTickets.v1");
            _timeProvider = timeProvider ?? TimeProvider.System;
        }

        public MediaAccessTicket Issue(Guid mediaItemId, Guid operatorId)
        {
            var expiresAt = _timeProvider.GetUtcNow().UtcDateTime.Add(Lifetime);
            var payload = new MediaAccessPayload(mediaItemId, operatorId, expiresAt);
            string token = _protector.Protect(JsonSerializer.Serialize(payload));
            return new MediaAccessTicket(token, expiresAt);
        }

        public Guid? Validate(string? ticket, Guid mediaItemId)
        {
            if (string.IsNullOrEmpty(ticket))
            {
                return null;
            }

            try
            {
                MediaAccessPayload? payload = JsonSerializer.Deserialize<MediaAccessPayload>(_protector.Unprotect(ticket));
                if (payload == null)
                {
                    return null;
                }

                if (payload.MediaItemId != mediaItemId)
                {
                    return null;
                }

                if (payload.ExpiresAtUtc <= _timeProvider.GetUtcNow().UtcDateTime)
                {
                    return null;
                }

                return payload.OperatorId;
            }
            catch
            {
                return null;
            }
        }
    }
}
