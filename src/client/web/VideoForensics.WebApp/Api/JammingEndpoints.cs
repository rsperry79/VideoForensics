using VideoForensics.Api.Contracts;
using VideoForensics.Client.Common.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Hosting;
using VideoForensics.WebApp.Auth;

namespace VideoForensics.WebApp.Api
{
    /// <summary>
    /// Jamming / signal-interference API: lets client hosts (MAUI) use <see cref="IJammingRepository"/>
    /// through <c>RemoteJammingRepository</c>, which only the server data layer registers locally.
    ///
    /// Base policy is the default authenticated-user policy (any signed-in role may read); the write
    /// routes stack <see cref="VideoForensicsPolicies.Admin"/> on top, exactly like <c>CaseEndpoints</c>.
    /// <c>CaseId</c> and <c>DetectedAtUtc</c> are server-controlled and never accepted from the client:
    /// the repository creates the case + alert itself when a NEW incident is inserted. <c>Source</c> is
    /// client-claimed by an Admin and honoured only for NEW incidents (so a client host that ran the
    /// RSSI analysis can keep its AutoDetected provenance); an existing incident keeps its stored Source.
    /// </summary>
    public static class JammingEndpoints
    {
        private const int MaxNotesLength = 2000;

        public static void MapJammingEndpoints(this WebApplication app)
        {
            RouteGroupBuilder group = app.MapGroup("/api/v1/jamming").RequireAuthorization();

            _ = group.MapGet("/incidents", ListIncidentsAsync)
                .RequireRateLimiting("default")
                .WithSummary("List jamming incidents")
                .WithDescription("Lists jamming incidents, optionally filtered by deviceId and a fromUtc/toUtc range on the incident start.");

            _ = group.MapGet("/incidents/{id:guid}", GetIncidentAsync)
                .RequireRateLimiting("default")
                .WithSummary("Get jamming incident")
                .WithDescription("Retrieves a single jamming incident by its identifier.");

            _ = group.MapGet("/stats/{deviceId:guid}", GetStatsAsync)
                .RequireRateLimiting("default")
                .WithSummary("Get device jamming stats")
                .WithDescription("Retrieves the jamming statistics summary for a device; 404 when none has been computed.");

            _ = group.MapGet("/stats", ListStatsAsync)
                .RequireRateLimiting("default")
                .WithSummary("List jamming stats")
                .WithDescription("Lists the jamming statistics summaries of all devices.");

            _ = group.MapPut("/incidents", UpsertIncidentAsync)
                .RequireAuthorization(VideoForensicsPolicies.Admin)
                .RequireRateLimiting("default")
                .WithSummary("Record jamming incident")
                .WithDescription("Inserts or updates a jamming incident. DetectedAtUtc and CaseId are server-controlled; Source (optional, new incidents only) defaults to ManuallyRecorded.");

            _ = group.MapPost("/stats/{deviceId:guid}/recompute", RecomputeStatsAsync)
                .RequireAuthorization(VideoForensicsPolicies.Admin)
                .RequireRateLimiting("default")
                .WithSummary("Recompute device jamming stats")
                .WithDescription("Recomputes and returns the jamming statistics summary of a device from its incidents.");
        }

        /// <summary>Handler for GET /incidents</summary>
        public static async Task<IResult> ListIncidentsAsync(
            Guid? deviceId,
            DateTime? fromUtc,
            DateTime? toUtc,
            IJammingRepository jamming,
            CancellationToken ct)
        {
            IReadOnlyList<JammingIncidentRecord> incidents = await jamming.ListIncidentsAsync(deviceId, ToUtc(fromUtc), ToUtc(toUtc), ct);
            return Results.Ok(incidents.Select(i => i.ToDto()).ToList());
        }

        /// <summary>Handler for GET /incidents/{id:guid}</summary>
        public static async Task<IResult> GetIncidentAsync(Guid id, IJammingRepository jamming, CancellationToken ct)
        {
            JammingIncidentRecord? incident = await jamming.GetIncidentAsync(id, ct);
            return incident == null ? Results.NotFound() : Results.Ok(incident.ToDto());
        }

        /// <summary>Handler for GET /stats/{deviceId:guid}</summary>
        public static async Task<IResult> GetStatsAsync(Guid deviceId, IJammingRepository jamming, CancellationToken ct)
        {
            JammingStatsSummary? stats = await jamming.GetStatsAsync(deviceId, ct);
            return stats == null ? Results.NotFound() : Results.Ok(stats.ToDto());
        }

        /// <summary>Handler for GET /stats</summary>
        public static async Task<IResult> ListStatsAsync(IJammingRepository jamming, CancellationToken ct)
        {
            IReadOnlyList<JammingStatsSummary> stats = await jamming.ListStatsAsync(ct);
            return Results.Ok(stats.Select(s => s.ToDto()).ToList());
        }

        /// <summary>Handler for PUT /incidents</summary>
        public static async Task<IResult> UpsertIncidentAsync(
            UpsertJammingIncidentRequest request,
            IJammingRepository jamming,
            IDeviceRepository devices,
            ISecurityAuditLogger auditLog,
            INetworkTierResolver tierResolver,
            HttpContext context,
            CancellationToken ct)
        {
            string? validationError = Validate(request);
            if (validationError != null)
            {
                return Results.BadRequest(new { error = validationError });
            }

            // Checked up front so a bad DeviceId is a 404, never a foreign-key failure surfacing as a 500.
            Device? device = await devices.GetAsync(request.DeviceId, ct);
            if (device == null)
            {
                return Results.NotFound(new { error = "Device not found" });
            }

            JammingIncidentRecord record = request.ToDomain();
            JammingIncidentRecord? existing = request.Id == Guid.Empty ? null : await jamming.GetIncidentAsync(request.Id, ct);
            if (existing == null)
            {
                // record.Source already holds the validated client claim (ManuallyRecorded when omitted).
                record.DetectedAtUtc = DateTime.UtcNow;
                record.CaseId = null;
            }
            else
            {
                // The repository overwrites these on update, so carry the stored values forward: a client
                // can neither relabel an auto-detected incident nor rewrite when it was detected or unlink its case.
                record.Source = existing.Source;
                record.DetectedAtUtc = existing.DetectedAtUtc;
                record.CaseId = existing.CaseId;
            }

            JammingIncidentRecord saved = await jamming.UpsertIncidentAsync(record, ct);
            if (existing == null)
            {
                // The case is created by the repository after the insert; re-read so the caller sees its CaseId.
                saved = await jamming.GetIncidentAsync(saved.Id, ct) ?? saved;
            }

            string? operatorIdClaim = context.User.FindFirst(VideoForensicsClaimTypes.OperatorId)?.Value;
            await auditLog.LogAsync(SecurityAuditEventTypes.JammingIncidentRecorded,
                Guid.TryParse(operatorIdClaim, out Guid actingOperatorId) ? actingOperatorId : null,
                null, tierResolver.ResolveClientIp(context), $"device={device.Name} confidence={saved.Confidence}", isUrgent: false, ct);

            return Results.Ok(saved.ToDto());
        }

        /// <summary>Handler for POST /stats/{deviceId:guid}/recompute</summary>
        public static async Task<IResult> RecomputeStatsAsync(
            Guid deviceId,
            IJammingRepository jamming,
            IDeviceRepository devices,
            CancellationToken ct)
        {
            if (await devices.GetAsync(deviceId, ct) == null)
            {
                return Results.NotFound(new { error = "Device not found" });
            }

            JammingStatsSummary stats = await jamming.RecomputeStatsAsync(deviceId, ct);
            return Results.Ok(stats.ToDto());
        }

        private static string? Validate(UpsertJammingIncidentRequest request)
        {
            // IsDefined rejects numeric strings like "99" that Enum.TryParse would otherwise accept.
            if (request.Confidence == null
                || !Enum.TryParse(request.Confidence, ignoreCase: true, out JammingConfidenceLevel confidence)
                || !Enum.IsDefined(confidence))
            {
                return "Invalid confidence. Must be one of: " + string.Join(", ", Enum.GetNames<JammingConfidenceLevel>()) + ".";
            }

            if (request.Source != null
                && (!Enum.TryParse(request.Source, ignoreCase: true, out JammingIncidentSource source) || !Enum.IsDefined(source)))
            {
                return "Invalid source. Must be one of: " + string.Join(", ", Enum.GetNames<JammingIncidentSource>()) + ".";
            }

            if (request.StartUtc >= request.EndUtc)
            {
                return "StartUtc must be before EndUtc.";
            }

            if (!double.IsFinite(request.AverageDegradationDb) || request.AverageDegradationDb < 0)
            {
                return "AverageDegradationDb must be a non-negative number.";
            }

            if (request.AffectedEventCount < 0)
            {
                return "AffectedEventCount must be non-negative.";
            }

            if (request.Notes is { Length: > MaxNotesLength })
            {
                return $"Notes must be at most {MaxNotesLength} characters.";
            }

            return null;
        }

        /// <summary>
        /// Minimal API parses a "...Z" query value into a Local-kind DateTime; the repository compares against UTC
        /// values, so normalise to UTC (an Unspecified value is taken to already be UTC).
        /// </summary>
        private static DateTime? ToUtc(DateTime? value)
        {
            return value switch
            {
                null => null,
                { Kind: DateTimeKind.Utc } => value,
                { Kind: DateTimeKind.Local } => value.Value.ToUniversalTime(),
                _ => DateTime.SpecifyKind(value.Value, DateTimeKind.Utc)
            };
        }
    }
}