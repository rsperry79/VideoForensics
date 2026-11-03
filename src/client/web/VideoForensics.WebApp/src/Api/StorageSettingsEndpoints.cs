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
            IStorageSettingsService storageSettings,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(requestDto.NewRootPath))
            {
                return Results.BadRequest("New default root cannot be empty");
            }

            // The service runs the move under maintenance and restarts the process on success.
            try
            {
                RelocateStorageCategoryResult result = await storageSettings.ChangeDefaultRootAsync(requestDto.NewRootPath, cancellationToken);
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

        private static async Task<IResult> RelocateStorageCategory(
            RelocateStorageCategoryRequestDto requestDto,
            IStorageSettingsService storageSettings,
            CancellationToken cancellationToken)
        {
            RelocateStorageCategoryRequest request = requestDto.ToDomain();

            // The service runs every move under maintenance and restarts the process after a Database or Logs move.
            try
            {
                RelocateStorageCategoryResult result = await storageSettings.RelocateAsync(request, cancellationToken);
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
