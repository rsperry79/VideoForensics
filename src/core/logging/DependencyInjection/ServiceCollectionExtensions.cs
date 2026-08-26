using Microsoft.Extensions.DependencyInjection;
using VideoForensics.Core.Logging.Contracts;
using VideoForensics.Core.Logging.Services;

namespace VideoForensics.Core.Logging.DependencyInjection
{
    public static class ServiceCollectionExtensions
    {
        /// <summary>Adds the action logger service to the dependency injection container.</summary>
        public static IServiceCollection AddActionLogger(this IServiceCollection services)
        {
            services.AddScoped<IActionLogger, ActionLogger>();
            return services;
        }
    }
}
