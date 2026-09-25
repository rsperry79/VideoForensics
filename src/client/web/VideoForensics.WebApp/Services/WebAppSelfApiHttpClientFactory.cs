using VideoForensics.Ui.Shared.Services;

namespace VideoForensics.WebApp.Services
{
    /// <summary>
    /// The WebApp's own <see cref="ISelfApiHttpClientFactory"/> for <c>Pages/Security*.razor</c>: it
    /// builds the exact same self-HTTP handler chain <see cref="SelfHttpServiceExtensions.AddSelfHttpService{TService}"/>
    /// uses for RemoteSecurityEventsService/RemoteAdminOperatorService (via the shared
    /// <see cref="SelfHttpServiceExtensions.CreateSelfHttpClient"/> helper, so the two never drift
    /// apart), so those pages' self-calls carry the calling circuit's real network tier the same way -
    /// see <see cref="SelfHttpServiceExtensions"/>'s doc comment for why a self-call's own loopback
    /// connection can't be trusted to resolve that tier on its own.
    ///
    /// Registered ahead of (and so it wins over) Ui.Shared's <see cref="DefaultSelfApiHttpClientFactory"/>
    /// <c>TryAddScoped</c> registration.
    /// </summary>
    public class WebAppSelfApiHttpClientFactory : ISelfApiHttpClientFactory
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly HttpMessageHandler? _innermostHandlerOverride;

        public WebAppSelfApiHttpClientFactory(IServiceProvider serviceProvider)
            : this(serviceProvider, innermostHandlerOverride: null)
        {
        }

        /// <summary>Test-only entry point: lets a test substitute a recording/fake innermost handler
        /// instead of a real <see cref="HttpClientHandler"/>, to observe the chain's outgoing request
        /// without a live network call.</summary>
        internal WebAppSelfApiHttpClientFactory(IServiceProvider serviceProvider, HttpMessageHandler? innermostHandlerOverride)
        {
            _serviceProvider = serviceProvider;
            _innermostHandlerOverride = innermostHandlerOverride;
        }

        /// <inheritdoc/>
        public HttpClient CreateClient() => SelfHttpServiceExtensions.CreateSelfHttpClient(_serviceProvider, _innermostHandlerOverride);
    }
}
