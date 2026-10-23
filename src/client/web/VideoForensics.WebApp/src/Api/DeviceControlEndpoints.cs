using VideoForensics.Core.Api.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.WebApp.Auth;

using IDeviceControlService = VideoForensics.Core.Providers.Common.Contracts.IDeviceControlService;

namespace VideoForensics.WebApp.Api
{
    /// <summary>
    /// Provider-agnostic device controls (light, siren, volume, night mode, motion detection, do-not-disturb).
    /// The route id is the provider's own device id (ProviderDeviceId), because the UI only holds that value.
    /// The handler resolves it to a Device and then calls IDeviceControlService with the provider device id.
    /// Known limitation: ProviderDeviceId is unique per provider account, not globally. Lookup takes the first
    /// match across all accounts, so two accounts with the same provider id would collide. Scope the lookup to
    /// the account in a later step.
    /// Input is validated here, at the API boundary only. Provider failures return 502 with a generic message
    /// and log only the exception type.
    /// </summary>
    public static class DeviceControlEndpoints
    {
        public static void MapDeviceControlEndpoints(this WebApplication app)
        {
            RouteGroupBuilder group = app.MapGroup("/api/v1/device-controls").RequireAuthorization(VideoForensicsPolicies.SuperAdminLocalOrPasskey);

            _ = group.MapPost("/{providerDeviceId}/light", SetLightAsync)
                .WithSummary("Turn a device light on or off")
                .RequireRateLimiting("media");

            _ = group.MapPost("/{providerDeviceId}/siren", SetSirenAsync)
                .WithSummary("Turn a device siren on or off")
                .RequireRateLimiting("media");

            _ = group.MapPost("/{providerDeviceId}/volume", SetVolumeAsync)
                .WithSummary("Set a device volume (0 to 11)")
                .RequireRateLimiting("media");

            _ = group.MapPost("/{providerDeviceId}/night-mode", SetNightModeAsync)
                .WithSummary("Enable or disable night mode on a device")
                .RequireRateLimiting("media");

            _ = group.MapPost("/{providerDeviceId}/motion-detection", SetMotionDetectionAsync)
                .WithSummary("Enable or disable motion detection on a device")
                .RequireRateLimiting("media");

            _ = group.MapPost("/{providerDeviceId}/do-not-disturb", SetDoNotDisturbAsync)
                .WithSummary("Silence a chime for a number of seconds")
                .RequireRateLimiting("media");
        }

        public static Task<IResult> SetLightAsync(
            string providerDeviceId, SetLightRequest body, IDeviceRepository devices, IDeviceControlService control,
            ILogger<DeviceControlLog> logger, CancellationToken ct)
        {
            return ControlAsync(providerDeviceId, devices, logger, ct,
                providerId => control.SetLightAsync(providerId, body.On, ct));
        }

        public static Task<IResult> SetSirenAsync(
            string providerDeviceId, SetSirenRequest body, IDeviceRepository devices, IDeviceControlService control,
            ILogger<DeviceControlLog> logger, CancellationToken ct)
        {
            if (body.DurationSeconds is { } duration && (duration < 1 || duration > 3600))
            {
                return Task.FromResult(Results.BadRequest());
            }

            return ControlAsync(providerDeviceId, devices, logger, ct,
                providerId => control.SetSirenAsync(providerId, body.On, body.DurationSeconds, ct));
        }

        public static Task<IResult> SetVolumeAsync(
            string providerDeviceId, SetVolumeRequest body, IDeviceRepository devices, IDeviceControlService control,
            ILogger<DeviceControlLog> logger, CancellationToken ct)
        {
            if (body.Volume < 0 || body.Volume > 11)
            {
                return Task.FromResult(Results.BadRequest());
            }

            return ControlAsync(providerDeviceId, devices, logger, ct,
                providerId => control.SetVolumeAsync(providerId, body.Volume, ct));
        }

        public static Task<IResult> SetNightModeAsync(
            string providerDeviceId, SetNightModeRequest body, IDeviceRepository devices, IDeviceControlService control,
            ILogger<DeviceControlLog> logger, CancellationToken ct)
        {
            return ControlAsync(providerDeviceId, devices, logger, ct,
                providerId => control.SetNightModeAsync(providerId, body.Enabled, ct));
        }

        public static Task<IResult> SetMotionDetectionAsync(
            string providerDeviceId, SetMotionDetectionRequest body, IDeviceRepository devices, IDeviceControlService control,
            ILogger<DeviceControlLog> logger, CancellationToken ct)
        {
            return ControlAsync(providerDeviceId, devices, logger, ct,
                providerId => control.SetMotionDetectionAsync(providerId, body.Enabled, ct));
        }

        public static Task<IResult> SetDoNotDisturbAsync(
            string providerDeviceId, SetDoNotDisturbRequest body, IDeviceRepository devices, IDeviceControlService control,
            ILogger<DeviceControlLog> logger, CancellationToken ct)
        {
            if (body.Seconds < 1 || body.Seconds > 86400)
            {
                return Task.FromResult(Results.BadRequest());
            }

            return ControlAsync(providerDeviceId, devices, logger, ct,
                providerId => control.SetDoNotDisturbAsync(providerId, body.Seconds, ct));
        }

        private static async Task<IResult> ControlAsync(
            string providerDeviceId, IDeviceRepository devices, ILogger<DeviceControlLog> logger, CancellationToken ct,
            Func<string, Task> applyControl)
        {
            // Lookup is by provider id across all devices; see the class remarks for the account-scoping limitation.
            IReadOnlyList<Device> all = await devices.ListAsync(ct);
            Device? device = all.FirstOrDefault(d => string.Equals(d.ProviderDeviceId, providerDeviceId, StringComparison.Ordinal));
            if (device is null)
            {
                return Results.NotFound();
            }

            try
            {
                await applyControl(device.ProviderDeviceId);
                return Results.NoContent();
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (KeyNotFoundException)
            {
                // The provider could not map the device to a linked account; no control was sent.
                return Results.NotFound();
            }
            catch (InvalidOperationException ex)
            {
                // Ambiguous account links (or an unsigned-in account) refuse the control. Log the type only.
                logger.LogWarning("Device control refused: {ExceptionType}", ex.GetType().Name);
                return Results.Problem(statusCode: StatusCodes.Status409Conflict, detail: "The device control was refused for this device's account.");
            }
            catch (Exception ex)
            {
                // Log the exception type only: provider messages can carry URLs or ids.
                logger.LogError("Device control failed at the provider: {ExceptionType}", ex.GetType().Name);
                return Results.Problem(statusCode: StatusCodes.Status502BadGateway, detail: "The provider could not apply the device control.");
            }
        }
    }
}

/// <summary>Log category for device control endpoints. Endpoints are a static class, so they cannot be a logger type argument.</summary>
public sealed class DeviceControlLog;
