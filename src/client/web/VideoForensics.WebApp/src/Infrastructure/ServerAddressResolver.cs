namespace VideoForensics.Client.WebApp.Infrastructure
{
    /// <summary>
    /// Resolves the actual listening port from ASPNETCORE_URLS/urls configuration,
    /// defaulting to 5162 (the standard port from Properties/launchSettings.json).
    /// Used by ChatOrchestrator to discover the local server's base address for MCP connections.
    /// </summary>
    /// <summary>A single Kestrel listen endpoint parsed from the configured URL list.</summary>
    /// <param name="Scheme">Lower-case URL scheme: "http" or "https".</param>
    /// <param name="Port">The TCP port to bind.</param>
    internal sealed record ListenEndpoint(string Scheme, int Port);

    internal static class ServerAddressResolver
    {
        private const int DefaultPort = 5162;

        private static IEnumerable<Uri> ParseUrls(IConfiguration configuration)
        {
            string? urls = configuration["ASPNETCORE_URLS"] ?? configuration["urls"];
            foreach (string entry in (urls ?? string.Empty).Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                if (Uri.TryCreate(entry.Trim(), UriKind.Absolute, out Uri? uri))
                {
                    yield return uri;
                }
            }
        }

        /// <summary>
        /// Resolves every configured listen endpoint with its own scheme, in configured order.
        /// https entries are bound with TLS (ASP.NET development certificate); http entries stay plain HTTP.
        /// Defaults to a single http endpoint on 5162 when nothing usable is configured.
        /// </summary>
        /// <param name="configuration">The application configuration.</param>
        /// <returns>The endpoints to bind.</returns>
        public static IReadOnlyList<ListenEndpoint> ResolveListenEndpoints(IConfiguration configuration)
        {
            var endpoints = ParseUrls(configuration)
                .Where(u => string.Compare(u.Scheme, Uri.UriSchemeHttp, StringComparison.Ordinal) == 0 || string.Compare(u.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal) == 0)
                .Select(u => new ListenEndpoint(u.Scheme, u.Port))
                .ToList();
            return endpoints.Count > 0 ? endpoints : new[] { new ListenEndpoint("http", DefaultPort) };
        }

        /// <summary>
        /// Resolves the WebAuthn/passkey allowed origins by mirroring the Kestrel listen endpoints
        /// (scheme + port, host fixed to localhost), so the origin check cannot drift from what the server binds.
        /// </summary>
        /// <param name="configuration">The application configuration.</param>
        /// <returns>A case-sensitive set of origins such as "https://localhost:7004".</returns>
        public static IReadOnlySet<string> ResolveAllowedOrigins(IConfiguration configuration)
        {
            var origins = new HashSet<string>(
                ResolveListenEndpoints(configuration).Select(e => $"{e.Scheme}://localhost:{e.Port}"),
                StringComparer.Ordinal);

            // A reverse proxy/tunnel has a public origin that is not a Kestrel listen
            // address. Keep it explicit rather than trusting Host headers blindly.
            string? configuredOrigins = configuration["WebAuthn:Origins"];
            if (!string.IsNullOrWhiteSpace(configuredOrigins))
            {
                foreach (string origin in configuredOrigins.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    if (Uri.TryCreate(origin, UriKind.Absolute, out Uri? uri)
                        && (string.Compare(uri.Scheme, Uri.UriSchemeHttp, StringComparison.Ordinal) == 0 || string.Compare(uri.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal) == 0)
                        && string.IsNullOrEmpty(uri.AbsolutePath.Trim('/')))
                    {
                        _ = origins.Add(uri.GetLeftPart(UriPartial.Authority));
                    }
                }
            }

            return origins;
        }

        /// <summary>
        /// Resolves the WebAuthn relying-party id. An explicit <c>WebAuthn:RpId</c> wins; otherwise the host of the
        /// configured Internet address is used, so passkeys match the tunnel host; otherwise <c>localhost</c>.
        /// </summary>
        /// <param name="configuration">The application configuration.</param>
        /// <param name="configuredInternetUrl">The saved Internet address (e.g. https://video.example.com), if any.</param>
        /// <returns>The RP id, a bare host name with no scheme, port or trailing dot.</returns>
        public static string ResolveRpId(IConfiguration configuration, string? configuredInternetUrl = null)
        {
            return configuration["WebAuthn:RpId"]?.Trim().TrimEnd('.')
                        is { Length: > 0 } configured
                ? configured
                : Uri.TryCreate(configuredInternetUrl, UriKind.Absolute, out Uri? internetUrl)
                && internetUrl.Scheme == Uri.UriSchemeHttps
                && internetUrl.Host.Length > 0
                ? internetUrl.Host
                : "localhost";
        }

        /// <summary>
        /// Resolves the port of the first configured http endpoint, defaulting to 5162. The chat self-call
        /// uses this over http://localhost because an HTTP listener is always present, avoiding certificate trust issues.
        /// </summary>
        /// <param name="configuration">The application configuration.</param>
        /// <returns>The HTTP port.</returns>
        public static int ResolveHttpPort(IConfiguration configuration)
        {
            return ResolveListenEndpoints(configuration).FirstOrDefault(e => string.Compare(e.Scheme, "http", StringComparison.Ordinal) == 0)?.Port ?? DefaultPort;
        }

        /// <summary>
        /// Resolves the actual listening port from ASPNETCORE_URLS/urls config, defaulting to 5162.
        /// </summary>
        /// <param name="configuration">The application configuration.</param>
        /// <returns>The configured port, or 5162 if not found.</returns>
        public static int ResolveConfiguredPort(IConfiguration configuration)
        {
            string? urls = configuration["ASPNETCORE_URLS"] ?? configuration["urls"];
            string? first = urls?.Split(';', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            if (first is not null && Uri.TryCreate(first, UriKind.Absolute, out Uri? uri))
            {
                return uri.Port;
            }

            return 5162; // Matches Properties/launchSettings.json's applicationUrl.
        }
    }
}
