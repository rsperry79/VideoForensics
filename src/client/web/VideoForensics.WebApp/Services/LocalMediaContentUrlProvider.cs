using Microsoft.Extensions.Logging;

using VideoForensics.Api.Contracts;
using VideoForensics.Client.Common.Contracts;
using VideoForensics.Hosting;
using VideoForensics.Ui.Shared.Services;

namespace VideoForensics.WebApp.Services
{
    /// <summary>
    /// Provides media content URLs (for img/video src attributes) for the local WebApp by issuing
    /// short-lived access tickets tied to the paired-device session. Unlike RemoteMediaContentUrlProvider
    /// which would fetch tickets from a remote server, this implementation uses the in-process
    /// ticket service and validates the session token locally.
    ///
    /// The session token's embedded OperatorId is used for ticket issuance (not localStorage values),
    /// ensuring audit correctness and preventing privilege escalation.
    /// </summary>
    public class LocalMediaContentUrlProvider : IMediaContentUrlProvider
    {
        private readonly PairedSessionState _sessionState;
        private readonly ISessionTokenService _sessionTokenService;
        private readonly IMediaAccessTicketService _ticketService;
        private readonly ILogger<LocalMediaContentUrlProvider> _logger;

        public LocalMediaContentUrlProvider(
            PairedSessionState sessionState,
            ISessionTokenService sessionTokenService,
            IMediaAccessTicketService ticketService,
            ILogger<LocalMediaContentUrlProvider> logger)
        {
            _sessionState = sessionState;
            _sessionTokenService = sessionTokenService;
            _ticketService = ticketService;
            _logger = logger;
        }

        public async Task<IReadOnlyDictionary<Guid, string>> GetContentUrlsAsync(
            IReadOnlyCollection<Guid> mediaItemIds,
            CancellationToken ct)
        {
            // Throw if cancellation was requested before we started
            ct.ThrowIfCancellationRequested();

            // Ensure the paired session is loaded from storage
            await _sessionState.EnsureLoadedAsync();

            // If no session is signed in, return empty
            if (!_sessionState.IsSignedIn)
            {
                return new Dictionary<Guid, string>();
            }

            // Validate the session token to get the principal
            SessionPrincipal? principal = _sessionTokenService.Validate(_sessionState.SessionToken);
            if (principal == null)
            {
                return new Dictionary<Guid, string>();
            }

            // Check cancellation again after async work
            ct.ThrowIfCancellationRequested();

            // Deduplicate the requested media IDs
            var distinctIds = mediaItemIds.Distinct().ToList();

            // Issue tickets for each distinct ID and build the result dictionary
            var result = new Dictionary<Guid, string>(distinctIds.Count);
            foreach (var mediaId in distinctIds)
            {
                var ticket = _ticketService.Issue(mediaId, principal.OperatorId);
                string url = MediaContentRoutes.ContentUrl(mediaId, ticket.Token);
                result[mediaId] = url;
            }

            return result;
        }
    }
}
