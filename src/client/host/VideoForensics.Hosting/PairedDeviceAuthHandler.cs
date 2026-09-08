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
            // Attach the paired-device session token as a Bearer credential if available.
            if (!string.IsNullOrEmpty(_sessionState.SessionToken))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _sessionState.SessionToken);
            }

            return await base.SendAsync(request, cancellationToken);
        }
    }
}
