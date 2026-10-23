using Microsoft.Extensions.DependencyInjection;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Core.Logging.DependencyInjection;

namespace VideoForensics.Hosting.Local
{
    /// <summary>Registers the in-process log viewer for hosts that read their own logs without HTTP (MAUI).</summary>
    public static class LocalLogViewerServiceExtensions
    {
        /// <summary>
        /// Registers the in-memory log buffer and <see cref="LocalLogViewerService"/> as <see cref="ILogViewerService"/>.
        /// Register after <c>AddVideoForensicsClientApi</c> so this implementation replaces the HTTP one (last registration wins).
        /// </summary>
        /// <param name="services">The service collection to add to.</param>
        /// <returns>The same service collection, for chaining.</returns>
        public static IServiceCollection AddLocalLogViewerService(this IServiceCollection services)
        {
            _ = services.AddInMemoryLogBuffer();
            _ = services.AddSingleton<ILogViewerService, LocalLogViewerService>();
            return services;
        }
    }
}
