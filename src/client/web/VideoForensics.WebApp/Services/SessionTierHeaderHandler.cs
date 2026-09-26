using VideoForensics.Hosting;

namespace VideoForensics.WebApp.Services
{
    /// <summary>
    /// <see cref="DelegatingHandler"/>, used only by <see cref="SelfHttpServiceExtensions"/>, that
    /// attaches the calling Blazor circuit's REAL network tier (<see cref="SessionNetworkContext"/>)
    /// to a self-HTTP request via the <c>X-VF-Session-Tier</c> header, so
    /// <c>PairedDeviceAuthenticationHandler</c>/<c>RequestTierResolver</c> can recover it server-side
    /// instead of resolving the self-call's own connection - which is always loopback, since the
    /// WebApp is calling itself, regardless of where the real browser physically is (see
    /// SessionNetworkContext's doc comment).
    ///
    /// Two flavors of header, both bound to the current request's <see cref="_bearerToken"/> (never
    /// to the client-cached <see cref="Ui.Shared.Services.PairedSessionState.OperatorId"/> directly, so it
    /// cannot be replayed against a different operator's request):
    /// - When <see cref="_bearerToken"/> re-validates to a real session, an OPERATOR-BOUND header (see
    ///   <see cref="ISessionTierHeaderProtector.Protect"/>) - the existing behavior, used by
    ///   already-authenticated self-calls (RemoteSecurityEventsService, Security*.razor's own client,
    ///   WebAuthnClient's step-up-style calls).
    /// - When there is no bearer token at all AND <see cref="_attachPreAuthHeaderWhenNoToken"/> is set,
    ///   a PRE-AUTH header (see <see cref="ISessionTierHeaderProtector.ProtectPreAuth"/>) - for a call
    ///   made before any session/operator exists yet (e.g. WebAuthnClient's login/setup/pairing
    ///   calls), so a PRE-AUTH endpoint like <c>OperatorAuthEndpoints.LoginPasswordAsync</c> can still
    ///   recover the real tier for its own tier-gated decision. <see cref="_attachPreAuthHeaderWhenNoToken"/>
    ///   defaults to off for the original, already-authenticated self-call use (no session simply
    ///   means "not signed in" there, not "pre-auth"), and is turned on only for the explicit-bearer-
    ///   token self-HTTP client (see <c>SelfHttpServiceExtensions.CreateSelfHttpClientWithBearerToken</c>).
    /// </summary>
    public class SessionTierHeaderHandler : DelegatingHandler
    {
        private readonly SessionNetworkContext _networkContext;
        private readonly string? _bearerToken;
        private readonly bool _attachPreAuthHeaderWhenNoToken;
        private readonly ISessionTokenService _tokenService;
        private readonly ISessionTierHeaderProtector _headerProtector;

        public SessionTierHeaderHandler(
            SessionNetworkContext networkContext,
            string? bearerToken,
            bool attachPreAuthHeaderWhenNoToken,
            ISessionTokenService tokenService,
            ISessionTierHeaderProtector headerProtector)
        {
            _networkContext = networkContext;
            _bearerToken = bearerToken;
            _attachPreAuthHeaderWhenNoToken = attachPreAuthHeaderWhenNoToken;
            _tokenService = tokenService;
            _headerProtector = headerProtector;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (!string.IsNullOrEmpty(_bearerToken))
            {
                SessionPrincipal? principal = _tokenService.Validate(_bearerToken);
                if (principal != null)
                {
                    string headerValue = _headerProtector.Protect(_networkContext.Tier, principal.OperatorId);
                    request.Headers.TryAddWithoutValidation(SessionTierHeaderNames.HeaderName, headerValue);
                }

                // A provided-but-invalid token (expired/revoked) is left with no header at all - the
                // request will fail authentication downstream regardless of tier, same as before this
                // feature existed.
            }
            else if (_attachPreAuthHeaderWhenNoToken)
            {
                string preAuthHeaderValue = _headerProtector.ProtectPreAuth(_networkContext.Tier);
                request.Headers.TryAddWithoutValidation(SessionTierHeaderNames.HeaderName, preAuthHeaderValue);
            }

            return base.SendAsync(request, cancellationToken);
        }
    }
}
