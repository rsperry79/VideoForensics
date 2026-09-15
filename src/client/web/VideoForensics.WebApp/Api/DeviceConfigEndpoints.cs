using VideoForensics.Api.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.WebApp.Auth;

namespace VideoForensics.WebApp.Api
{
    /// <summary>
    /// Device configuration snapshot management (plan §4/M5): read-only endpoints for listing
    /// and retrieving device configuration snapshots, plus an append-only write endpoint to record
    /// new configuration states. Configuration snapshots are immutable once recorded - updates are
    /// new snapshots, not in-place modifications.
    ///
    /// Device configuration changes (appending snapshots) are ordinary operator actions that do not
    /// widen network exposure or breach privileged boundaries the way pairing revocation or network
    /// tier changes do - they record settings that have already been fetched from or applied to
    /// devices by provider services running server-side. No step-up re-authentication is required
    /// for these operations: they are audited and the same authorization (SuperAdmin+Local)
    /// applies equally to all endpoints in this group.
    /// </summary>
    public static class DeviceConfigEndpoints
    {
        public static void MapDeviceConfigEndpoints(this WebApplication app)
        {
            RouteGroupBuilder group = app.MapGroup("/api/v1/device-config").RequireAuthorization(VideoForensicsPolicies.SuperAdminLocal);

            _ = group.MapGet("/", async (IDeviceConfigRepository configs, CancellationToken ct) =>
                Results.Ok((await configs.ListAsync(ct)).Select(x => x.ToDto())))
                .WithSummary("List all device configuration snapshots")
                .WithDescription("Returns all device configuration snapshots in the system, ordered by capture time.")
                .RequireRateLimiting("media");

            _ = group.MapGet("/{id:guid}", async (Guid id, IDeviceConfigRepository configs, CancellationToken ct) =>
            {
                DeviceConfigSnapshot? snapshot = await configs.GetAsync(id, ct);
                return snapshot == null ? Results.NotFound() : Results.Ok(snapshot.ToDto());
            })
            .WithSummary("Get a device configuration snapshot by ID")
            .WithDescription("Returns the specific device configuration snapshot identified by its snapshot ID.")
            .RequireRateLimiting("media");

            _ = group.MapGet("/by-device/{deviceId:guid}", async (Guid deviceId, IDeviceConfigRepository configs, CancellationToken ct) =>
            {
                DeviceConfigSnapshot? snapshot = await configs.GetLatestAsync(deviceId, ct);
                return snapshot == null ? Results.NotFound() : Results.Ok(snapshot.ToDto());
            })
            .WithSummary("Get the latest configuration snapshot for a device")
            .WithDescription("Returns the most recent configuration snapshot for the specified device.")
            .RequireRateLimiting("media");

            _ = group.MapGet("/history/{deviceId:guid}", async (Guid deviceId, IDeviceConfigRepository configs, CancellationToken ct) =>
                Results.Ok((await configs.GetHistoryAsync(deviceId, ct)).Select(x => x.ToDto())))
            .WithSummary("Get the configuration history for a device")
            .WithDescription("Returns all configuration snapshots for the specified device, ordered by capture time.")
            .RequireRateLimiting("media");

            _ = group.MapPost("/", async (
                CreateDeviceConfigSnapshotRequest request,
                IDeviceConfigRepository configs,
                CancellationToken ct) =>
            {
                var snapshot = new DeviceConfigSnapshot
                {
                    Id = Guid.NewGuid(),
                    DeviceId = request.DeviceId,
                    MotionDetectionEnabled = request.MotionDetectionEnabled,
                    MotionSensitivity = request.MotionSensitivity,
                    RecordingMode = request.RecordingMode,
                    CustomSettingsJson = request.CustomSettingsJson,
                    CapturedAtUtc = request.CapturedAtUtc ?? DateTime.UtcNow,
                    Source = (DeviceConfigSource)Enum.Parse(typeof(DeviceConfigSource), request.Source)
                };

                DeviceConfigSnapshot created = await configs.AppendSnapshotAsync(snapshot, ct);
                return Results.Created($"/api/v1/device-config/{created.Id}", created.ToDto());
            })
            .WithSummary("Append a new device configuration snapshot")
            .WithDescription("Records a new configuration snapshot for a device. Configuration snapshots are immutable once created.")
            .RequireRateLimiting("media");
        }
    }

    /// <summary>Request payload for creating a new device configuration snapshot.</summary>
    /// <param name="DeviceId">The ID of the device this configuration applies to.</param>
    /// <param name="MotionDetectionEnabled">Whether motion detection is enabled.</param>
    /// <param name="MotionSensitivity">Motion detection sensitivity level.</param>
    /// <param name="RecordingMode">Recording mode setting.</param>
    /// <param name="CustomSettingsJson">Additional vendor-specific settings in JSON format.</param>
    /// <param name="CapturedAtUtc">When this configuration was captured (optional; defaults to current UTC time).</param>
    /// <param name="Source">Source of this configuration: "Fetched" (from device/provider) or "Applied" (settings applied to device).</param>
    public record CreateDeviceConfigSnapshotRequest(
        Guid DeviceId,
        bool? MotionDetectionEnabled,
        string? MotionSensitivity,
        string? RecordingMode,
        string? CustomSettingsJson,
        DateTime? CapturedAtUtc,
        string Source
    );
}
