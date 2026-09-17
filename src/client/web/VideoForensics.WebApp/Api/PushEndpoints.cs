using System.Text.Json;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.WebApp.Auth;
using VideoForensics.WebApp.Services;

namespace VideoForensics.WebApp.Api
{
    /// <summary>
    /// Web Push subscription and VAPID key management endpoints (plan §5.6).
    /// Clients use these to subscribe/unsubscribe for push notifications and retrieve the server's public VAPID key.
    /// Subscriptions are tied to the authenticated operator and stored in the database for persistence across sessions.
    /// </summary>
    public static class PushEndpoints
    {
        public static void MapPushEndpoints(this WebApplication app)
        {
            // GET /api/v1/push/vapid-public-key - retrieve the server's public VAPID key for browser/client
            _ = app.MapGet("/api/v1/push/vapid-public-key", async (
                VapidKeyProvider vapidProvider,
                CancellationToken ct) =>
            {
                var (publicKey, _) = await vapidProvider.GetOrCreateKeysAsync(ct);
                return Results.Ok(new { publicKey });
            })
            .RequireAuthorization();

            // POST /api/v1/push/subscribe - register a new push subscription for the authenticated operator
            _ = app.MapPost("/api/v1/push/subscribe", async (
                PushSubscribeRequest request,
                IPushSubscriptionRepository subscriptionRepository,
                HttpContext context,
                CancellationToken ct) =>
            {
                // Extract the authenticated operator ID from claims
                string? operatorIdClaim = context.User.FindFirst(VideoForensicsClaimTypes.OperatorId)?.Value;
                if (!Guid.TryParse(operatorIdClaim, out Guid operatorId))
                {
                    return Results.Unauthorized();
                }

                // Create and store the subscription
                var subscription = new PushSubscription
                {
                    Id = Guid.NewGuid(),
                    OperatorId = operatorId,
                    Endpoint = request.Endpoint,
                    P256dhKey = request.P256dhKey,
                    AuthKey = request.AuthKey,
                    CreatedUtc = DateTime.UtcNow,
                    LastUsedUtc = DateTime.UtcNow
                };

                await subscriptionRepository.AddOrUpdateAsync(subscription, ct);
                return Results.Ok();
            })
            .RequireAuthorization();

            // POST /api/v1/push/unsubscribe - remove a push subscription by endpoint
            _ = app.MapPost("/api/v1/push/unsubscribe", async (
                PushUnsubscribeRequest request,
                IPushSubscriptionRepository subscriptionRepository,
                CancellationToken ct) =>
            {
                await subscriptionRepository.RemoveAsync(request.Endpoint, ct);
                return Results.Ok();
            })
            .RequireAuthorization();
        }
    }

    public record PushSubscribeRequest(string Endpoint, string P256dhKey, string AuthKey);
    public record PushUnsubscribeRequest(string Endpoint);
}
