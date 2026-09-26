using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

using VideoForensics.Hosting;
using VideoForensics.Ui.Shared.Services;

namespace VideoForensics.WebApp.Services
{
    /// <summary>
    /// Registers a <c>Remote*</c>-style service (one written to talk to
    /// <c>VideoForensics.WebApp/Api/*Endpoints.cs</c> over HTTP, normally used by a thin client such as
    /// MAUI - see <see cref="Hosting.VideoForensicsHostingExtensions.AddVideoForensicsClientApi"/>) for
    /// use by the WebApp's OWN Blazor UI as well, calling back into its own Minimal API instead of a
    /// remote server address.
    ///
    /// This matters for any endpoint gated on the caller's real network tier (SuperAdmin+Local, plan
    /// §5.10/§5.12): a Blazor Server circuit's own <c>HttpContext</c> - and therefore the CONNECTION
    /// this self-HTTP call itself travels over - is a loopback call from this process back into
    /// itself, regardless of where the real browser physically is. Resolving tier from that
    /// connection would always (incorrectly) yield Local. <see cref="SessionTierHeaderHandler"/>
    /// fixes this: it attaches the circuit's REAL tier - captured once from the browser's actual
    /// initial connection into <see cref="SessionNetworkContext"/> (see
    /// Components/NetworkTierCapture.razor) - as a protected header, which
    /// <c>PairedDeviceAuthenticationHandler</c> then recovers server-side instead of trusting the
    /// loopback connection. The endpoint's own tier check (reading the resulting NetworkTier claim)
    /// stays the single, uniform source of truth for both MAUI and the WebApp's own UI.
    ///
    /// The HttpClient's base address is resolved from the current circuit's <see cref="NavigationManager"/>
    /// (the same source the existing self-call pages already use) rather than a fixed configuration
    /// value, since the WebApp's own externally-visible address can vary (LAN address, Cloudflare Tunnel
    /// hostname, etc.). Auth is attached via the same <see cref="PairedDeviceAuthHandler"/> used by every
    /// other Remote* registration, reading the current circuit's <see cref="PairedSessionState"/> - so a
    /// signed-in WebApp user's own browser session authenticates the call exactly like a paired MAUI
    /// device would.
    /// </summary>
    public static class SelfHttpServiceExtensions
    {
        /// <summary>
        /// Registers <typeparamref name="TService"/> as a scoped service built by <paramref name="factory"/>
        /// from an <see cref="HttpClient"/> that targets this same WebApp instance and carries the current
        /// circuit's paired-session bearer token.
        /// </summary>
        public static IServiceCollection AddSelfHttpService<TService>(
            this IServiceCollection services,
            Func<HttpClient, TService> factory)
            where TService : class
        {
            return services.AddScoped(sp => factory(CreateSelfHttpClient(sp)));
        }

        /// <summary>
        /// Builds the shared self-HTTP handler chain and <see cref="HttpClient"/> using the current
        /// circuit's <see cref="PairedSessionState.SessionToken"/> for both the tier header (operator-
        /// bound only; no session at all means no header, same as before this feature existed - see
        /// <see cref="SessionTierHeaderHandler"/>) and the bearer credential. Used by
        /// <see cref="AddSelfHttpService{TService}"/> and by <see cref="WebAppSelfApiHttpClientFactory.CreateClient()"/>.
        /// See <see cref="CreateSelfHttpClientWithBearerToken"/> for the explicit-bearer-token variant.
        /// </summary>
        public static HttpClient CreateSelfHttpClient(IServiceProvider sp, HttpMessageHandler? innermostHandler = null)
        {
            PairedSessionState sessionState = sp.GetRequiredService<PairedSessionState>();
            return CreateSelfHttpClientCore(sp, sessionState.SessionToken, attachPreAuthHeaderWhenNoToken: false, innermostHandler);
        }

        /// <summary>
        /// Builds the shared self-HTTP handler chain and <see cref="HttpClient"/> for a caller (e.g.
        /// <c>WebAuthnClient</c>) that already manages its own bearer token as an explicit value rather
        /// than reading it from <see cref="PairedSessionState"/> directly - some of its calls happen
        /// before any session/circuit-level token exists at all (login, first-run setup, device
        /// pairing/registration). Unlike <see cref="CreateSelfHttpClient"/>, a null
        /// <paramref name="bearerToken"/> here attaches a PRE-AUTH tier header (see
        /// <see cref="ISessionTierHeaderProtector.ProtectPreAuth"/>) instead of no header at all, so a
        /// PRE-AUTH endpoint like <c>OperatorAuthEndpoints.LoginPasswordAsync</c>'s primary-SuperAdmin
        /// Local-only check can still recover the real tier (see <c>RequestTierResolver.ResolvePreAuth</c>).
        /// Used by <see cref="WebAppSelfApiHttpClientFactory.CreateClient(string?)"/>.
        /// </summary>
        public static HttpClient CreateSelfHttpClientWithBearerToken(IServiceProvider sp, string? bearerToken, HttpMessageHandler? innermostHandler = null)
        {
            return CreateSelfHttpClientCore(sp, bearerToken, attachPreAuthHeaderWhenNoToken: true, innermostHandler);
        }

        /// <summary>
        /// Builds the shared self-HTTP handler chain - the outermost <see cref="SessionTierHeaderHandler"/>
        /// (real network tier), then <see cref="PairedDeviceAuthHandler"/> (bearer token, always read
        /// from the circuit's <see cref="PairedSessionState"/> - which equals <paramref name="tierHeaderBearerToken"/>
        /// in every real caller), then <paramref name="innermostHandler"/> - shared by both public
        /// entry points above so they never drift apart. <paramref name="innermostHandler"/> defaults
        /// to a real <see cref="HttpClientHandler"/>; tests substitute a recording/fake handler to
        /// observe what the chain sends without a live network call.
        /// </summary>
        private static HttpClient CreateSelfHttpClientCore(
            IServiceProvider sp,
            string? tierHeaderBearerToken,
            bool attachPreAuthHeaderWhenNoToken,
            HttpMessageHandler? innermostHandler)
        {
            PairedSessionState sessionState = sp.GetRequiredService<PairedSessionState>();
            NavigationManager navigationManager = sp.GetRequiredService<NavigationManager>();
            SessionNetworkContext networkContext = sp.GetRequiredService<SessionNetworkContext>();
            ISessionTokenService tokenService = sp.GetRequiredService<ISessionTokenService>();
            ISessionTierHeaderProtector headerProtector = sp.GetRequiredService<ISessionTierHeaderProtector>();

            var authHandler = new PairedDeviceAuthHandler(sessionState)
            {
                InnerHandler = innermostHandler ?? new HttpClientHandler()
            };
            // Outermost: attaches the circuit's real network tier (see
            // SessionTierHeaderHandler's doc comment for why this can't just be resolved from
            // the self-call's own connection) before the auth handler attaches the bearer token.
            var tierHandler = new SessionTierHeaderHandler(networkContext, tierHeaderBearerToken, attachPreAuthHeaderWhenNoToken, tokenService, headerProtector)
            {
                InnerHandler = authHandler
            };
            return new HttpClient(tierHandler) { BaseAddress = new Uri(navigationManager.BaseUri) };
        }
    }
}
