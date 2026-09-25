using Microsoft.AspNetCore.Components;

namespace VideoForensics.Ui.Shared.Services
{
    /// <summary>
    /// Default <see cref="ISelfApiHttpClientFactory"/> for hosts (e.g. MAUI) with no self-call
    /// network-tier concern: reproduces exactly what every <c>Pages/Security*.razor</c> page's own
    /// private <c>CreateClient()</c> did before this abstraction existed - an <see cref="HttpClient"/>
    /// whose <see cref="HttpClient.BaseAddress"/> comes from the current circuit's
    /// <see cref="NavigationManager"/> and which carries the current <see cref="PairedSessionState"/>'s
    /// session token as a bearer credential, when one is present.
    ///
    /// The WebApp registers its own implementation instead (see WebApp's
    /// <c>SelfHttpServiceExtensions</c>/<c>WebAppSelfApiHttpClientFactory</c>), since only it needs to
    /// attach the calling circuit's real network tier for <c>SuperAdminLocal</c>-gated endpoints.
    /// </summary>
    public class DefaultSelfApiHttpClientFactory : ISelfApiHttpClientFactory
    {
        private readonly NavigationManager _navigationManager;
        private readonly PairedSessionState _sessionState;

        public DefaultSelfApiHttpClientFactory(NavigationManager navigationManager, PairedSessionState sessionState)
        {
            _navigationManager = navigationManager;
            _sessionState = sessionState;
        }

        /// <inheritdoc/>
        public HttpClient CreateClient()
        {
            var client = new HttpClient { BaseAddress = new Uri(_navigationManager.BaseUri) };
            if (_sessionState.SessionToken is not null)
            {
                client.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _sessionState.SessionToken);
            }

            return client;
        }
    }
}
