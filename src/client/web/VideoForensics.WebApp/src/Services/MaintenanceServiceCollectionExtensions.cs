using Microsoft.Extensions.DependencyInjection.Extensions;

using VideoForensics.Client.Hosting.Maintenance;

namespace VideoForensics.Client.WebApp.Services
{
    /// <summary>Registers the server maintenance services in one place, so the host and its tests compose them the same way.</summary>
    public static class MaintenanceServiceCollectionExtensions
    {
        /// <summary>
        /// Adds the process-wide maintenance gate and the coordinator that the Database endpoints take as a parameter.
        /// The coordinator is scoped because it uses the scoped database maintenance service; the gate it drives is a singleton.
        /// </summary>
        public static IServiceCollection AddServerMaintenance(this IServiceCollection services)
        {
            services.TryAddSingleton<MaintenanceGate>();
            services.TryAddSingleton<IMaintenanceGate>(sp => sp.GetRequiredService<MaintenanceGate>());
            services.TryAddScoped<MaintenanceCoordinator>();
            return services;
        }
    }
}
