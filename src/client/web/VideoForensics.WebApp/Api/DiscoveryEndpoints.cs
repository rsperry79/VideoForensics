using VideoForensics.Api.Contracts;
using VideoForensics.Providers.Common.Contracts;

namespace VideoForensics.WebApp.Api
{
    /// <summary>
    /// Minimal API surface wrapping IDeviceDiscoveryService and IEventAndConfigService contracts.
    /// These are provider-facing discovery and configuration operations that trigger real provider API calls
    /// to enumerate locations, devices, retrieve events, and query/update device settings.
    ///
    /// Authorization is required (RequireAuthorization) because these operations need the paired-device
    /// credential to authenticate with the provider. Step-up authentication is not required since these
    /// are primarily read operations (discovery/config queries) that do not destructively modify provider state,
    /// with the exception of UpdateDeviceConfig, which updates configuration but not data itself.
    /// </summary>
    public static class DiscoveryEndpoints
    {
        public static void MapDiscoveryEndpoints(this WebApplication app)
        {
            RouteGroupBuilder group = app.MapGroup("/api/v1/discovery").RequireAuthorization();

            // IDeviceDiscoveryService endpoints

            _ = group.MapGet("/locations", GetLocations)
                .WithSummary("Get all locations")
                .WithDescription("Retrieves all locations/sites for the authenticated user from the provider.")
                .RequireRateLimiting("media");

            _ = group.MapGet("/locations/{locationId}/devices", GetDevicesByLocation)
                .WithSummary("Get devices at a location")
                .WithDescription("Retrieves all devices at a specific location from the provider.")
                .RequireRateLimiting("media");

            _ = group.MapGet("/devices/{deviceId}", GetDevice)
                .WithSummary("Get device details")
                .WithDescription("Retrieves details for a specific device from the provider.")
                .RequireRateLimiting("media");

            // IEventAndConfigService endpoints

            _ = group.MapGet("/devices/{deviceId}/events", GetDeviceEvents)
                .WithSummary("Get device events")
                .WithDescription("Retrieves events (motion, person detection, etc.) for a device within a date range from the provider.")
                .RequireRateLimiting("media");

            _ = group.MapGet("/devices/{deviceId}/config", GetDeviceConfig)
                .WithSummary("Get device configuration")
                .WithDescription("Retrieves device configuration settings from the provider.")
                .RequireRateLimiting("media");

            _ = group.MapPut("/devices/{deviceId}/config", UpdateDeviceConfig)
                .WithSummary("Update device configuration")
                .WithDescription("Updates device configuration settings on the provider.")
                .RequireRateLimiting("media");
        }

        private static async Task<IResult> GetLocations(
            IDeviceDiscoveryService discoveryService,
            CancellationToken ct)
        {
            IReadOnlyList<Location> locations = await discoveryService.GetLocationsAsync(ct);
            return Results.Ok(locations.Select(x => x.ToDto()));
        }

        private static async Task<IResult> GetDevicesByLocation(
            string locationId,
            IDeviceDiscoveryService discoveryService,
            CancellationToken ct)
        {
            IReadOnlyList<Device> devices = await discoveryService.GetDevicesAsync(locationId, ct);
            return Results.Ok(devices.Select(x => x.ToDto()));
        }

        private static async Task<IResult> GetDevice(
            string deviceId,
            IDeviceDiscoveryService discoveryService,
            CancellationToken ct)
        {
            Device? device = await discoveryService.GetDeviceAsync(deviceId, ct);
            return device == null ? Results.NotFound() : Results.Ok(device.ToDto());
        }

        private static async Task<IResult> GetDeviceEvents(
            string deviceId,
            DateTime startDate,
            DateTime endDate,
            string? eventType,
            IEventAndConfigService configService,
            CancellationToken ct)
        {
            IReadOnlyList<DeviceEvent> events = await configService.GetEventsAsync(
                deviceId,
                startDate,
                endDate,
                eventType,
                ct
            );
            return Results.Ok(events.Select(x => x.ToDto()));
        }

        private static async Task<IResult> GetDeviceConfig(
            string deviceId,
            IEventAndConfigService configService,
            CancellationToken ct)
        {
            DeviceConfig? config = await configService.GetDeviceConfigAsync(deviceId, ct);
            return config == null ? Results.NotFound() : Results.Ok(config.ToDto());
        }

        private static async Task<IResult> UpdateDeviceConfig(
            string deviceId,
            DeviceConfigDto configDto,
            IEventAndConfigService configService,
            CancellationToken ct)
        {
            DeviceConfig config = configDto.ToDomain();
            bool success = await configService.UpdateDeviceConfigAsync(deviceId, config, ct);

            return !success ? Results.BadRequest("Failed to update device configuration.") : Results.NoContent();
        }
    }
}
