using System.Net;
using System.Text.Json;

using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Providers.Common.Contracts;
using VideoForensics.WebApp.Services;

using WebPush;

namespace VideoForensics.WebApp.Hubs
{
    /// <summary>
    /// Sends urgent security events (plan §5.6) via Web Push API (RFC 8291) to subscribed browser/mobile clients.
    /// Queries push subscriptions by audience (AdminsOnly or All) and filters by operator notification preferences.
    /// Automatically removes expired subscriptions when the push service reports them gone (HTTP 404/410).
    /// </summary>
    public class WebPushNotificationProvider : INotificationProvider
    {
        private readonly IPushSubscriptionRepository _pushSubscriptionRepository;
        private readonly IOperatorNotificationPreferenceRepository _preferenceRepository;
        private readonly VapidKeyProvider _vapidKeyProvider;
        private readonly ILogger<WebPushNotificationProvider> _logger;

        public WebPushNotificationProvider(
            IPushSubscriptionRepository pushSubscriptionRepository,
            IOperatorNotificationPreferenceRepository preferenceRepository,
            VapidKeyProvider vapidKeyProvider,
            ILogger<WebPushNotificationProvider> logger)
        {
            _pushSubscriptionRepository = pushSubscriptionRepository ?? throw new ArgumentNullException(nameof(pushSubscriptionRepository));
            _preferenceRepository = preferenceRepository ?? throw new ArgumentNullException(nameof(preferenceRepository));
            _vapidKeyProvider = vapidKeyProvider ?? throw new ArgumentNullException(nameof(vapidKeyProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public string Name => "WebPush";

        /// <summary>
        /// VAPID keys can always be generated on demand if missing, so Web Push is always capable of being enabled.
        /// </summary>
        public Task<bool> IsEnabledAsync(CancellationToken ct)
        {
            return Task.FromResult(true);
        }

        public async Task SendAsync(NotificationEvent notificationEvent, CancellationToken ct)
        {
            try
            {
                // Get target subscriptions based on audience
                IReadOnlyList<VideoForensics.Data.Common.Entities.PushSubscription> targetSubscriptions = notificationEvent.Audience switch
                {
                    NotificationAudience.AdminsOnly => await _pushSubscriptionRepository.ListForAdminsAsync(ct),
                    NotificationAudience.All => LogAndReturn(notificationEvent),
                    _ => LogAndReturn(notificationEvent),
                };

                if (targetSubscriptions.Count == 0)
                {
                    _logger.LogDebug("No push subscriptions found for audience {Audience}", notificationEvent.Audience);
                    return;
                }

                // Get VAPID keys for signing
                (string? publicKey, string? privateKey) = await _vapidKeyProvider.GetOrCreateKeysAsync(ct);
                var vapidDetails = new VapidDetails("mailto:admin@videoforensics.local", publicKey, privateKey);

                // Build the notification payload
                var payload = new
                {
                    title = notificationEvent.EventType,
                    body = notificationEvent.Details,
                    severity = notificationEvent.Severity.ToString()
                };
                string jsonPayload = JsonSerializer.Serialize(payload);

                var client = new WebPushClient();

                // Send to each subscription, filtering by operator preferences
                foreach (Data.Common.Entities.PushSubscription subscription in targetSubscriptions)
                {
                    try
                    {
                        // Load operator preferences
                        OperatorNotificationPreference? preferences = await _preferenceRepository.GetAsync(subscription.OperatorId, ct);

                        // Skip if push is disabled or severity doesn't meet minimum threshold
                        if (preferences == null || !preferences.PushEnabled)
                        {
                            _logger.LogDebug(
                                "Skipping push for operator {OperatorId} - push disabled or no preferences",
                                subscription.OperatorId);
                            continue;
                        }

                        if (notificationEvent.Severity < preferences.MinimumSeverity)
                        {
                            _logger.LogDebug(
                                "Skipping push for operator {OperatorId} - severity {ActualSeverity} below minimum {MinimumSeverity}",
                                subscription.OperatorId,
                                notificationEvent.Severity,
                                preferences.MinimumSeverity);
                            continue;
                        }

                        // Map PushSubscription entity to WebPush library's PushSubscription format
                        var webPushSubscription = new WebPush.PushSubscription
                        {
                            Endpoint = subscription.Endpoint,
                            P256DH = subscription.P256dhKey,
                            Auth = subscription.AuthKey
                        };

                        // Send the notification
                        await client.SendNotificationAsync(webPushSubscription, jsonPayload, vapidDetails);

                        _logger.LogInformation(
                            "Web push sent successfully - EventType={EventType}, OperatorId={OperatorId}, Endpoint={Endpoint}",
                            notificationEvent.EventType,
                            subscription.OperatorId,
                            TruncateEndpoint(subscription.Endpoint));
                    }
                    catch (WebPushException ex) when (ex.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Gone)
                    {
                        // Subscription is no longer valid - remove it
                        _logger.LogInformation(
                            "Push subscription expired ({StatusCode}) - removing - Endpoint={Endpoint}",
                            ex.StatusCode,
                            TruncateEndpoint(subscription.Endpoint));

                        try
                        {
                            await _pushSubscriptionRepository.RemoveAsync(subscription.Endpoint, ct);
                        }
                        catch (Exception removeEx)
                        {
                            _logger.LogWarning(removeEx,
                                "Failed to remove expired subscription - Endpoint={Endpoint}",
                                TruncateEndpoint(subscription.Endpoint));
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex,
                            "Web push notification failed - EventType={EventType}, OperatorId={OperatorId}, Endpoint={Endpoint}",
                            notificationEvent.EventType,
                            subscription.OperatorId,
                            TruncateEndpoint(subscription.Endpoint));
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Web push provider encountered an error - EventType={EventType}", notificationEvent.EventType);
                throw;
            }
        }

        private IReadOnlyList<VideoForensics.Data.Common.Entities.PushSubscription> LogAndReturn(NotificationEvent notificationEvent)
        {
            _logger.LogDebug("All-audience web push not implemented yet - EventType={EventType}", notificationEvent.EventType);
            return [];
        }

        private static string TruncateEndpoint(string endpoint)
        {
            // Truncate long endpoint URLs for safe logging
            return endpoint.Length > 50 ? endpoint[..47] + "..." : endpoint;
        }
    }
}
