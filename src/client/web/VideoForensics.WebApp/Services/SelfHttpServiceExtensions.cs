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
    /// §5.10/§5.12): a Blazor Server circuit's <c>HttpContext</c> is only reliably available during the
    /// request that first established the circuit, not for later UI-event-driven code (button clicks,
    /// etc.) - reusing it directly from a page for a later action would resolve a stale or missing
    /// network tier. Issuing a genuine new HTTP request back into the same Minimal API pipeline (the
    /// same pattern the existing SecurityLockoutPolicy/SecurityOperators pages use with an inline
    /// HttpClient) gives every call a fresh, real <c>HttpContext</c> - so the endpoint's own tier check
    /// stays the single, uniform source of truth for both MAUI and the WebApp's own UI, with zero
    /// duplicated authorization logic.
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
            return services.AddScoped(sp =>
            {
                PairedSessionState sessionState = sp.GetRequiredService<PairedSessionState>();
                NavigationManager navigationManager = sp.GetRequiredService<NavigationManager>();

                var authHandler = new PairedDeviceAuthHandler(sessionState)
                {
                    InnerHandler = new HttpClientHandler()
                };
                var httpClient = new HttpClient(authHandler) { BaseAddress = new Uri(navigationManager.BaseUri) };

                return factory(httpClient);
            });
        }
    }
}
