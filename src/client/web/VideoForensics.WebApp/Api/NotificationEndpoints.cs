using VideoForensics.Client.Common;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Hosting;
using VideoForensics.WebApp.Auth;

namespace VideoForensics.WebApp.Api
{
    /// <summary>
    /// Notification settings for the /settings/notifications screen (plan §5.6). Gated Admin, per
    /// the plan's own route table - viewing/editing which events page the owner isn't as sensitive
    /// as the SuperAdmin+Local infrastructure screens.
    /// </summary>
    public static class NotificationEndpoints
    {
        public static void MapNotificationEndpoints(this WebApplication app)
        {
            var group = app.MapGroup("/api/notifications").RequireAuthorization(VideoForensicsPolicies.Admin);

            group.MapGet("/settings", (IForensicsConfiguration config) => Results.Ok(new
            {
                enableEmailNotifications = config.EnableEmailNotifications,
                smtpHost = config.SmtpHost,
                smtpPort = config.SmtpPort,
                smtpUseTls = config.SmtpUseTls,
                smtpUsername = config.SmtpUsername,
                smtpFromAddress = config.SmtpFromAddress,
                notificationRecipientEmail = config.NotificationRecipientEmail
                // The SMTP password is deliberately never returned - write-only from the UI's
                // point of view, same as every other credential field in this app.
            }));

            group.MapPost("/settings", async (
                SaveNotificationSettingsRequest request,
                IForensicsConfiguration config,
                IForensicsConfigurationService configService,
                ISmtpPasswordStore passwordStore,
                CancellationToken ct) =>
            {
                config.EnableEmailNotifications = request.EnableEmailNotifications;
                config.SmtpHost = request.SmtpHost;
                config.SmtpPort = request.SmtpPort;
                config.SmtpUseTls = request.SmtpUseTls;
                config.SmtpUsername = request.SmtpUsername;
                config.SmtpFromAddress = request.SmtpFromAddress;
                config.NotificationRecipientEmail = request.NotificationRecipientEmail;
                await configService.SaveConfigurationAsync(config, ct);

                // Only overwrite the stored password if the caller actually sent a new one - the
                // settings form never receives the existing password back (see GET /settings above),
                // so a blank field on save must mean "leave it alone," not "clear it."
                if (!string.IsNullOrEmpty(request.SmtpPassword))
                {
                    await passwordStore.SetAsync(request.SmtpPassword, ct);
                }

                return Results.Ok();
            });

            group.MapPost("/test-email", async (EmailNotificationProvider emailProvider, CancellationToken ct) =>
            {
                try
                {
                    await emailProvider.SendTestEmailAsync(ct);
                    return Results.Ok();
                }
                catch (Exception ex)
                {
                    return Results.Json(new { error = $"Test email failed: {ex.Message}" }, statusCode: StatusCodes.Status502BadGateway);
                }
            });

            group.MapGet("/urgency-overrides", async (IUrgencyOverrideStore overrides, CancellationToken ct) =>
            {
                var current = await overrides.GetAllOverridesAsync(ct);
                var rows = SecurityAuditEventTypes.DefaultUrgency.Select(kvp => new
                {
                    eventType = kvp.Key,
                    defaultUrgent = kvp.Value,
                    overrideUrgent = current.TryGetValue(kvp.Key, out var o) ? (bool?)o : null,
                    effectiveUrgent = current.TryGetValue(kvp.Key, out var o2) ? o2 : kvp.Value
                });
                return Results.Ok(rows);
            });

            group.MapPost("/urgency-overrides/set", async (SetUrgencyOverrideRequest request, IUrgencyOverrideStore overrides, CancellationToken ct) =>
            {
                await overrides.SetOverrideAsync(request.EventType, request.IsUrgent, ct);
                return Results.Ok();
            });

            group.MapPost("/urgency-overrides/clear", async (ClearUrgencyOverrideRequest request, IUrgencyOverrideStore overrides, CancellationToken ct) =>
            {
                await overrides.ClearOverrideAsync(request.EventType, ct);
                return Results.Ok();
            });
        }
    }

    public record SaveNotificationSettingsRequest(
        bool EnableEmailNotifications,
        string SmtpHost,
        int SmtpPort,
        bool SmtpUseTls,
        string SmtpUsername,
        string? SmtpPassword,
        string SmtpFromAddress,
        string NotificationRecipientEmail);

    public record SetUrgencyOverrideRequest(string EventType, bool IsUrgent);
    public record ClearUrgencyOverrideRequest(string EventType);
}
