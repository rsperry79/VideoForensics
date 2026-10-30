using VideoForensics.Client.WebApp.Auth;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Client.WebApp.Api
{
    /// <summary>One device on an account's schedule page, with its capture choice.</summary>
    public sealed record DeviceCaptureModeDto(Guid DeviceId, string Name, string ProviderDeviceId, string Mode);

    /// <summary>Sets a device's capture choice. Mode is "Off", "HealthOnly", or "Capture" (case-insensitive).</summary>
    public sealed record SetDeviceCaptureModeRequest(string Mode);

    /// <summary>
    /// Per-device capture choice for the schedule page: list an account's devices with their mode, and change one device's mode.
    /// Changes are local settings, not Ring settings.
    /// </summary>
    public static class DeviceCaptureModeEndpoints
    {
        public static void MapDeviceCaptureModeEndpoints(this WebApplication app)
        {
            RouteGroupBuilder group = app.MapGroup("/api/v1/device-capture-modes")
                .RequireAuthorization(VideoForensicsPolicies.SuperAdminLocalOrPasskey);

            _ = group.MapGet("/", ListAsync)
                .WithName("ListDeviceCaptureModes");

            _ = group.MapPut("/{deviceId:guid}", SetModeAsync)
                .WithName("SetDeviceCaptureMode");
        }

        /// <summary>The account's devices in name order, with their current mode.</summary>
        internal static async Task<IResult> ListAsync(Guid providerAccountId, ILocationRepository locations, IDeviceRepository devices, CancellationToken ct)
        {
            IReadOnlyList<DeviceCaptureModeDto> rows = await ListForAccountAsync(providerAccountId, locations, devices, ct);
            return Results.Ok(rows);
        }

        internal static async Task<IReadOnlyList<DeviceCaptureModeDto>> ListForAccountAsync(Guid providerAccountId, ILocationRepository locations, IDeviceRepository devices, CancellationToken ct)
        {
            var rows = new List<DeviceCaptureModeDto>();
            foreach (Location location in await locations.ListByProviderAccountIdAsync(providerAccountId, ct))
            {
                foreach (Device device in await devices.GetByLocationIdAsync(location.Id, ct))
                {
                    rows.Add(new DeviceCaptureModeDto(device.Id, device.Name, device.ProviderDeviceId, device.CaptureMode.ToString()));
                }
            }

            return rows.OrderBy(row => row.Name, StringComparer.CurrentCultureIgnoreCase).ThenBy(row => row.ProviderDeviceId, StringComparer.Ordinal).ToList();
        }

        internal static async Task<IResult> SetModeAsync(Guid deviceId, SetDeviceCaptureModeRequest request, IDeviceRepository devices, CancellationToken ct)
        {
            if (!Enum.TryParse(request.Mode, ignoreCase: true, out DeviceCaptureMode mode) || !Enum.IsDefined(mode))
            {
                return Results.BadRequest(new { error = "Mode must be Off, HealthOnly, or Capture." });
            }

            Device? device = await devices.GetAsync(deviceId, ct);
            if (device is null)
            {
                return Results.NotFound();
            }

            device.CaptureMode = mode;
            await devices.UpdateAsync(device, ct);
            return Results.Ok(new DeviceCaptureModeDto(device.Id, device.Name, device.ProviderDeviceId, device.CaptureMode.ToString()));
        }
    }
}
