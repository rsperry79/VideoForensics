using Microsoft.Extensions.Configuration;

namespace VideoForensics.WebApp.Infrastructure
{
    /// <summary>
    /// Resolves the actual listening port from ASPNETCORE_URLS/urls configuration,
    /// defaulting to 5162 (the standard port from Properties/launchSettings.json).
    /// Used by ChatOrchestrator to discover the local server's base address for MCP connections.
    /// </summary>
    internal static class ServerAddressResolver
    {
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
