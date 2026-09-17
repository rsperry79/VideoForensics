using System.Net.Http.Headers;

using VideoForensics.Ui.Shared.Services;

namespace VideoForensics.Hosting
{
    /// <summary>
    /// <see cref="DelegatingHandler"/> that attaches the paired-device bearer token to every outgoing
    /// HTTP request. This is the single, centralized place where auth credentials flow into all 17
    /// <c>Remote*</c> HTTP client registrations in <see cref="AddVideoForensicsClientApi"/>, so no
    /// individual <c>Remote*</c> class needs its own auth logic or token-sourcing complexity.
    ///
    /// The token is obtained from the current circuit's <see cref="PairedSessionState"/> - if none is
    /// available or the token is null, the request proceeds without an Authorization header, and the
    /// server will respond with HTTP 401 (expected for an unauthenticated client).
    ///
    /// If a token is attached and the server responds with HTTP 401, the token is cleared and
    /// <see cref="PairedSessionState.NotifyAuthenticationExpiredAsync"/> is invoked to drive re-authentication.
    /// </summary>
    public class PairedDeviceAuthHandler : DelegatingHandler
    {
        private readonly PairedSessionState _sessionState;

        public PairedDeviceAuthHandler(PairedSessionState sessionState)
        {
            _sessionState = sessionState;
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            // Track whether we attached a token so we can detect 401 failures on an authenticated request.
            bool tokenWasAttached = false;

            // Attach the paired-device session token as a Bearer credential if available.
            if (!string.IsNullOrEmpty(_sessionState.SessionToken))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _sessionState.SessionToken);
                tokenWasAttached = true;
            }

            HttpResponseMessage response = await base.SendAsync(request, cancellationToken);

            // If we sent a token and the server rejected it, clear the stale credential and notify listeners.
            if (tokenWasAttached && response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                await _sessionState.NotifyAuthenticationExpiredAsync();
            }

            return response;
        }
    }
}
