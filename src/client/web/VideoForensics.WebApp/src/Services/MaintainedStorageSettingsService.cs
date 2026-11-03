using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Client.WebApp.Services.CircuitGuards;
using VideoForensics.Core.Helpers.Platform;

namespace VideoForensics.Client.WebApp.Services
{
    /// <summary>
    /// Runs every storage move under the maintenance coordinator, so the in-app pages and the HTTP endpoints share one path.
    /// A move of the Database or the Logs leaves maintenance held and stops the process, because this process still points at
    /// the old files. The restart then opens the new location.
    /// </summary>
    public sealed class MaintainedStorageSettingsService(IStorageSettingsService inner, MaintenanceCoordinator coordinator, IHostApplicationLifetime lifetime) : IStorageSettingsService
    {
        public Task<StorageSettings> GetStatusAsync(CancellationToken cancellationToken) => inner.GetStatusAsync(cancellationToken);

        public Task<RelocateStorageCategoryResult> RelocateAsync(RelocateStorageCategoryRequest request, CancellationToken cancellationToken)
        {
            bool restarts = request.Category is StorageCategory.Database or StorageCategory.Logs;
            return RunMaintainedAsync($"Moving {request.Category}", restarts, token => inner.RelocateAsync(request, token), cancellationToken);
        }

        public Task<RelocateStorageCategoryResult> ChangeDefaultRootAsync(string newDefaultRoot, CancellationToken cancellationToken)
        {
            // A default-root change can move the Database and the Logs, so it always restarts.
            return RunMaintainedAsync("Changing the default storage path", restarts: true, token => inner.ChangeDefaultRootAsync(newDefaultRoot, token), cancellationToken);
        }

        private async Task<RelocateStorageCategoryResult> RunMaintainedAsync(
            string description,
            bool restarts,
            Func<CancellationToken, Task<RelocateStorageCategoryResult>> operation,
            CancellationToken cancellationToken)
        {
            RelocateStorageCategoryResult result = await coordinator.RunAsync(
                description,
                (_, token) => operation(token),
                cancellationToken,
                holdOnSuccess: restarts);

            if (restarts && result.Succeeded)
            {
                Environment.ExitCode = 1;
                lifetime.StopApplication();
            }

            return result;
        }
    }

    /// <summary>Registers <see cref="MaintainedStorageSettingsService"/> around the storage service the pages and endpoints resolve.</summary>
    public static class MaintainedStorageSettingsRegistration
    {
        public static IServiceCollection AddMaintainedStorageSettings(this IServiceCollection services)
        {
            return services.DecorateScoped<IStorageSettingsService>((inner, sp) => new MaintainedStorageSettingsService(
                inner,
                sp.GetRequiredService<MaintenanceCoordinator>(),
                sp.GetRequiredService<IHostApplicationLifetime>()));
        }
    }
}
