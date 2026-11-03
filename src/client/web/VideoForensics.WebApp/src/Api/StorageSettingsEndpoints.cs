using VideoForensics.Client.Common.Contracts;
using VideoForensics.Client.WebApp.Api;
using VideoForensics.Client.WebApp.Auth;
using VideoForensics.Client.WebApp.Services;
using VideoForensics.Core.Api.Contracts;
using VideoForensics.Core.Helpers.Platform;

namespace VideoForensics.Client.WebApp.Api
{
    /// <summary>
    /// Storage settings management endpoints (SuperAdmin-only via Local network tier):
    /// retrieve current storage configuration and state for all categories, and relocate
    /// storage directories to new paths. Database and Logs categories require app restart
    /// after relocation to take effect.
    /// </summary>
    public static class StorageSettingsEndpoints
    {
        public static void MapStorageSettingsEndpoints(this WebApplication app)
        {
            RouteGroupBuilder group = app.MapGroup("/api/v1/storage-settings").RequireAuthorization(VideoForensicsPolicies.SuperAdminLocalOrPasskey);

            _ = group.MapGet("/", GetStorageSettings)
                .WithSummary("Get storage settings status")
                .WithDescription("Retrieves the current state of all storage categories including used space, free space, and whether they are using default or custom paths.")
                .RequireRateLimiting("media");

            _ = group.MapPost("/default-root", ChangeDefaultRoot)
                .WithSummary("Change the default storage root")
                .WithDescription("Moves every category on the default location under the new root and records it. The app restarts after a successful change.")
                .RequireRateLimiting("media");

            _ = group.MapPost("/relocate", RelocateStorageCategory)
                .WithSummary("Relocate a storage category")
                .WithDescription("Relocates a storage category to a new root path. Database and Logs categories require an app restart. Keys category cannot be relocated.")
                .RequireRateLimiting("media");
        }

        private static async Task<IResult> GetStorageSettings(IStorageSettingsService storageSettings, CancellationToken cancellationToken)
        {
            StorageSettings settings = await storageSettings.GetStatusAsync(cancellationToken);
            StorageSettingsDto dto = settings.ToDto();
            return Results.Ok(dto);
        }

        private static async Task<IResult> ChangeDefaultRoot(
            ChangeDefaultRootRequestDto requestDto,
            MaintenanceCoordinator coordinator,
            IHostApplicationLifetime lifetime,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(requestDto.NewRootPath))
            {
                return Results.BadRequest("New default root cannot be empty");
            }

            // Runs under the maintenance gate: the database file may move, so new work waits and pooled connections are released.
            // The hold stays after success, because this process still points at the old paths; it restarts to pick up the new ones.
            try
            {
                string newRoot = requestDto.NewRootPath;
                RelocateStorageCategoryResult result = await coordinator.RunAsync(
                    "Changing the default storage path",
                    (sp, token) => sp.GetRequiredService<IStorageSettingsService>().ChangeDefaultRootAsync(newRoot, token),
                    cancellationToken,
                    holdOnSuccess: true);

                if (result.Succeeded)
                {
                    Environment.ExitCode = 1;
                    lifetime.StopApplication();
                }

                return Results.Ok(result.ToDto());
            }
            catch (MaintenanceInProgressException ex)
            {
                return Results.Conflict(ex.Message);
            }
        }

        private static async Task<IResult> RelocateStorageCategory(
            RelocateStorageCategoryRequestDto requestDto,
            IStorageSettingsService storageSettings,
            MaintenanceCoordinator coordinator,
            IHostApplicationLifetime lifetime,
            CancellationToken cancellationToken)
        {
            RelocateStorageCategoryRequest request = requestDto.ToDomain();

            // Every move runs under maintenance: the coordinator blocks new work, waits for in-flight work, and releases
            // pooled connections before the files move. Database and Logs restart afterwards, because this process still
            // points at the old files, so their hold stays on success. Other categories end maintenance when the move ends.
            bool restarts = request.Category is StorageCategory.Database or StorageCategory.Logs;
            try
            {
                RelocateStorageCategoryResult result = await coordinator.RunAsync(
                    $"Moving {request.Category}",
                    (sp, token) => sp.GetRequiredService<IStorageSettingsService>().RelocateAsync(request, token),
                    cancellationToken,
                    holdOnSuccess: restarts);

                if (restarts && result.Succeeded)
                {
                    Environment.ExitCode = 1;
                    lifetime.StopApplication();
                }

                return Results.Ok(result.ToDto());
            }
            catch (MaintenanceInProgressException ex)
            {
                return Results.Conflict(ex.Message);
            }
            catch (MaintenanceDrainTimeoutException ex)
            {
                return Results.Text(ex.Message, statusCode: StatusCodes.Status503ServiceUnavailable);
            }
        }
    }
}
