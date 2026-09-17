using VideoForensics.Api.Contracts;
using VideoForensics.Client.Common.Contracts;
using VideoForensics.WebApp.Auth;

namespace VideoForensics.WebApp.Api
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
            RouteGroupBuilder group = app.MapGroup("/api/v1/storage-settings").RequireAuthorization(VideoForensicsPolicies.SuperAdminLocal);

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
            CancellationToken cancellationToken)
        {
            RelocateStorageCategoryRequest request = requestDto.ToDomain();
            RelocateStorageCategoryResult result = await storageSettings.RelocateAsync(request, cancellationToken);
            RelocateStorageCategoryResultDto resultDto = result.ToDto();
            return Results.Ok(resultDto);
        }
    }
}
