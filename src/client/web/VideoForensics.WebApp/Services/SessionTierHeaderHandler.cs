using VideoForensics.Hosting;
using VideoForensics.Ui.Shared.Services;

namespace VideoForensics.WebApp.Services
{
    /// <summary>
    /// <see cref="DelegatingHandler"/>, used only by <see cref="SelfHttpServiceExtensions.AddSelfHttpService{TService}"/>,
    /// that attaches the calling Blazor circuit's REAL network tier (<see cref="SessionNetworkContext"/>)
    /// to a self-HTTP request via the <c>X-VF-Session-Tier</c> header, so
    /// <c>PairedDeviceAuthenticationHandler</c> can recover it server-side instead of resolving the
    /// self-call's own connection - which is always loopback, since the WebApp is calling itself,
    /// regardless of where the real browser physically is (see SessionNetworkContext's doc comment).
    ///
    /// The header is bound to the current session's OPERATOR ID - taken from re-validating the
    /// session token (the same authoritative source LocalMediaContentUrlProvider uses), never from
    /// the client-cached <see cref="PairedSessionState.OperatorId"/> directly - so it cannot be
    /// replayed against a different operator's request. No signed-in/valid session at all means no
    /// header is attached; the call then authenticates (or not) exactly as before this feature
    /// existed.
    /// </summary>
    public class SessionTierHeaderHandler : DelegatingHandler
    {
        private readonly SessionNetworkContext _networkContext;
        private readonly PairedSessionState _sessionState;
        private readonly ISessionTokenService _tokenService;
        private readonly ISessionTierHeaderProtector _headerProtector;

        public SessionTierHeaderHandler(
            SessionNetworkContext networkContext,
            PairedSessionState sessionState,
            ISessionTokenService tokenService,
            ISessionTierHeaderProtector headerProtector)
        {
            _networkContext = networkContext;
            _sessionState = sessionState;
            _tokenService = tokenService;
            _headerProtector = headerProtector;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (!string.IsNullOrEmpty(_sessionState.SessionToken))
            {
                SessionPrincipal? principal = _tokenService.Validate(_sessionState.SessionToken);
                if (principal != null)
                {
                    string headerValue = _headerProtector.Protect(_networkContext.Tier, principal.OperatorId);
                    request.Headers.TryAddWithoutValidation(SessionTierHeaderNames.HeaderName, headerValue);
                }
            }

            return base.SendAsync(request, cancellationToken);
        }
    }
}
