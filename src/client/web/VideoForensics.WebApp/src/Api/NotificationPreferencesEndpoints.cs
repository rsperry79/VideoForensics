using System.Reflection;
using VideoForensics.Client.WebApp.Auth;
using VideoForensics.Core.Api.Contracts;
using VideoForensics.Core.Providers.Common.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Client.WebApp.Api
{
    /// <summary>
    /// Per-operator notification preferences (push severity floor, push on/off, email on/off and the email event types)
    /// for the signed-in operator. A read with no stored row returns the entity defaults. A write is validated at the
    /// boundary: the severity must be a defined <see cref="NoticeSeverity"/> and every email event type must be an exact
    /// <see cref="SecurityAuditEventTypes"/> constant name.
    /// </summary>
    public static class NotificationPreferencesEndpoints
    {
        private static readonly IReadOnlySet<string> KnownEmailEventTypes = ReadKnownEmailEventTypes();

        public static void MapNotificationPreferencesEndpoints(this WebApplication app)
        {
            // GET /api/v1/notifications/preferences - the operator's preferences, or the defaults when none are stored
            _ = app.MapGet("/api/v1/notifications/preferences", async (
                IOperatorNotificationPreferenceRepository preferenceRepository,
                HttpContext context,
                CancellationToken ct) =>
            {
                if (!TryGetOperatorId(context, out Guid operatorId))
                {
                    return Results.Unauthorized();
                }

                OperatorNotificationPreference? stored = await preferenceRepository.GetAsync(operatorId, ct);
                var preferences = stored ?? new OperatorNotificationPreference { OperatorId = operatorId };
                return Results.Ok(preferences.ToDto());
            })
            .RequireAuthorization();

            // POST /api/v1/notifications/preferences - replace the operator's preferences (upsert)
            _ = app.MapPost("/api/v1/notifications/preferences", async (
                NotificationPreferencesDto request,
                IOperatorNotificationPreferenceRepository preferenceRepository,
                HttpContext context,
                CancellationToken ct) =>
            {
                if (!TryGetOperatorId(context, out Guid operatorId))
                {
                    return Results.Unauthorized();
                }

                if (!Enum.IsDefined(typeof(NoticeSeverity), request.MinimumSeverity))
                {
                    return Results.BadRequest($"MinimumSeverity {request.MinimumSeverity} is not a defined severity.");
                }

                if (request.EmailEventTypes is null)
                {
                    return Results.BadRequest("EmailEventTypes is required; send an empty list to disable event types.");
                }

                foreach (string name in request.EmailEventTypes)
                {
                    string trimmed = (name ?? string.Empty).Trim();
                    if (!KnownEmailEventTypes.Contains(trimmed))
                    {
                        return Results.BadRequest($"Unknown email event type '{name}'.");
                    }
                }

                OperatorNotificationPreference entity = request.ToDomain();
                entity.OperatorId = operatorId;
                await preferenceRepository.UpsertAsync(entity, ct);
                return Results.Ok(entity.ToDto());
            })
            .RequireAuthorization();
        }

        /// <summary>Reads the operator id from the authenticated principal, the same way the push endpoints do.</summary>
        private static bool TryGetOperatorId(HttpContext context, out Guid operatorId)
        {
            string? operatorIdClaim = context.User.FindFirst(VideoForensicsClaimTypes.OperatorId)?.Value;
            return Guid.TryParse(operatorIdClaim, out operatorId);
        }

        /// <summary>
        /// Collects every public string constant of <see cref="SecurityAuditEventTypes"/>. Reflection keeps the list in step
        /// with the class: a new constant is accepted with no second list to update.
        /// </summary>
        private static IReadOnlySet<string> ReadKnownEmailEventTypes()
        {
            return typeof(SecurityAuditEventTypes)
                .GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(field => field.IsLiteral && field.FieldType == typeof(string))
                .Select(field => (string)field.GetRawConstantValue()!)
                .ToHashSet(StringComparer.Ordinal);
        }
    }
}
