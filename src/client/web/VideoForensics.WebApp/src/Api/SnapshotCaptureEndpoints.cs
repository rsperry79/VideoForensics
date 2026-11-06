using VideoForensics.Core.Api.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Client.WebApp.Api
{
    /// <summary>
    /// Snapshot captures over <c>/api/v1/snapshot-captures</c>: reads only, for any signed-in role, the same policy as the media-flag reads.
    /// The review panel lists a flag window's snapshots through the list route; the by-id route serves the evidence export.
    /// </summary>
    public static class SnapshotCaptureEndpoints
    {
        public static void MapSnapshotCaptureEndpoints(this WebApplication app)
        {
            RouteGroupBuilder group = app.MapGroup("/api/v1/snapshot-captures");

            _ = group.MapGet("", ListAsync)
                .RequireAuthorization()
                .RequireRateLimiting("media");

            _ = group.MapGet("{id:guid}", GetAsync)
                .RequireAuthorization()
                .RequireRateLimiting("media");
        }

        /// <summary>
        /// Lists one device's captures within the inclusive window, oldest first. <c>attemptsOnly=false</c> (the default) returns only
        /// current 200 captures; <c>attemptsOnly=true</c> returns every attempt, failed ones included. 400 when <c>to</c> is not after <c>from</c>.
        /// </summary>
        public static async Task<IResult> ListAsync(
            Guid deviceId,
            DateTimeOffset from,
            DateTimeOffset to,
            ISnapshotCaptureRepository captures,
            CancellationToken ct,
            bool attemptsOnly = false)
        {
            if (to <= from)
            {
                return Results.BadRequest();
            }

            IReadOnlyList<SnapshotCapture> rows = attemptsOnly
                ? await captures.GetAttemptsForDeviceAsync(deviceId, from, to, ct)
                : await captures.GetCurrentForDeviceAsync(deviceId, from, to, ct);
            return Results.Ok(rows.Select(ToDto).ToList());
        }

        /// <summary>Gets one capture attempt, or 404.</summary>
        public static async Task<IResult> GetAsync(Guid id, ISnapshotCaptureRepository captures, CancellationToken ct)
        {
            SnapshotCapture? capture = await captures.GetAsync(id, ct);
            return capture == null ? Results.NotFound() : Results.Ok(ToDto(capture));
        }

        private static SnapshotCaptureDto ToDto(SnapshotCapture capture)
        {
            return new SnapshotCaptureDto(
                capture.Id,
                capture.DeviceId,
                capture.CapturedAtUtc,
                capture.Trigger,
                capture.HttpStatus,
                capture.IsCurrent,
                capture.MediaFilePath,
                capture.DeviceHealthId);
        }
    }
}
