namespace VideoForensics.Ui.Shared.Services
{
    /// <summary>
    /// Builds an <see cref="HttpClient"/> for a Blazor page in <c>Pages/Security*.razor</c> (and any
    /// similar page) to call this same app's own <c>/api/v1/...</c> Minimal API - a "self-call" back
    /// into the host process rather than a remote server.
    ///
    /// <c>VideoForensics.Ui.Shared</c> cannot reference <c>VideoForensics.Hosting</c> or
    /// <c>VideoForensics.WebApp</c> (doing so would be circular - both reference Ui.Shared), so it
    /// cannot itself build the WebApp's network-tier-aware handler chain
    /// (see WebApp's <c>SelfHttpServiceExtensions</c>). Instead, each host registers the
    /// implementation appropriate to it:
    /// - <see cref="DefaultSelfApiHttpClientFactory"/> (registered here via <c>TryAddScoped</c>) for
    ///   hosts, such as MAUI, that have no self-tier concern - it reproduces the plain
    ///   <c>BaseAddress</c> + bearer-token behavior every page used before this abstraction existed.
    /// - The WebApp registers its own implementation instead, which attaches the calling circuit's
    ///   real network tier so <c>PairedDeviceAuthenticationHandler</c> can resolve it, since a
    ///   self-call's own connection is always loopback regardless of where the real browser is.
    ///
    /// A new call to <see cref="CreateClient"/> returns a fresh <see cref="HttpClient"/> (and,
    /// depending on the implementation, fresh underlying handlers) every time - callers must not
    /// assume the same instance is reused across calls, and disposing one client must never affect a
    /// client obtained from a later call.
    /// </summary>
    public interface ISelfApiHttpClientFactory
    {
        /// <summary>
        /// Builds an <see cref="HttpClient"/> targeting this host's own API, with its
        /// <see cref="HttpClient.BaseAddress"/> set and the current session's bearer credential (and,
        /// where applicable, any other required headers) already attached.
        /// </summary>
        HttpClient CreateClient();
    }
}
