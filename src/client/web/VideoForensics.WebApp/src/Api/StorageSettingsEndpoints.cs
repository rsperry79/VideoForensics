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

        private static async Task<IResult> RelocateStorageCategory(
            RelocateStorageCategoryRequestDto requestDto,
            IStorageSettingsService storageSettings,
            MaintenanceCoordinator coordinator,
            CancellationToken cancellationToken)
        {
            RelocateStorageCategoryRequest request = requestDto.ToDomain();
            if (request.Category != StorageCategory.Database)
            {
                RelocateStorageCategoryResult plain = await storageSettings.RelocateAsync(request, cancellationToken);
                return Results.Ok(plain.ToDto());
            }

            // The database must be closed before its file moves. The coordinator blocks new work, waits for in-flight work,
            // and releases pooled connections before the relocation runs.
            try
            {
                RelocateStorageCategoryResult result = await coordinator.RunAsync(
                    "Moving the database",
                    (sp, token) => sp.GetRequiredService<IStorageSettingsService>().RelocateAsync(request, token),
                    cancellationToken);
                return Results.Ok(result.ToDto());
            }
            catch (MaintenanceInProgressException ex)
            {
                return Results.Conflict(ex.Message);
            }
        }
    }
}
