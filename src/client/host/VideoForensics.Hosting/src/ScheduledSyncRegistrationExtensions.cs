using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Hosting.BackgroundServices;
using VideoForensics.Hosting.Services;

namespace VideoForensics.Hosting
{
    /// <summary>DI registration for the server-side scheduled per-account sync.</summary>
    public static class ScheduledSyncRegistrationExtensions
    {
        /// <summary>
        /// Registers <see cref="ScheduledSyncService"/> plus the keyed provider services it needs, but only when
        /// ScheduledTasks:EnabledProviders is non-empty; otherwise registers nothing. Server tier only
        /// (VideoForensics.WebApp) - never call this from MAUI or any client host. An unknown provider name throws
        /// (fail fast, via AddVideoForensicsMultiProviderServices).
        /// </summary>
        public static IServiceCollection AddVideoForensicsScheduledSync(this IServiceCollection services, IConfiguration configuration)
        {
            string[] providers = (configuration.GetSection($"{ScheduledTasksOptions.SectionName}:{nameof(ScheduledTasksOptions.EnabledProviders)}").Get<string[]>() ?? [])
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .ToArray();

            if (providers.Length == 0)
            {
                return services;
            }

            _ = services.AddVideoForensicsMultiProviderServices(providers);
            _ = services.Configure<ScheduledTasksOptions>(configuration.GetSection(ScheduledTasksOptions.SectionName));
            services.TryAddSingleton(TimeProvider.System);
            _ = services.AddHostedService<ScheduledSyncService>();
            return services;
        }

        /// <summary>
        /// Registers the background account export (<see cref="IAccountExportService"/> plus its queue-draining hosted
        /// service). Server tier only (VideoForensics.WebApp) - never call this from MAUI or any client host.
        /// Independent of ScheduledTasks:EnabledProviders: exporting needs no provider access.
        /// </summary>
        public static IServiceCollection AddVideoForensicsAccountExport(this IServiceCollection services)
        {
            services.TryAddSingleton(TimeProvider.System);
            services.TryAddSingleton<AccountExportService>();
            services.TryAddSingleton<IAccountExportService>(sp => sp.GetRequiredService<AccountExportService>());
            _ = services.AddHostedService<AccountExportBackgroundService>();
            return services;
        }
    }
}