using System.Text.Json;
using System.Text.Json.Serialization;

using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace VideoForensics.Client.Ui.Shared.Services
{
    /// <summary>
    /// Drives the web push subscription lifecycle against the browser's Service Worker API via
    /// <c>wwwroot/scripts/webpush.js</c>, mirroring the WebAuthnClient pattern. Handles subscription,
    /// unsubscription, and permission checking for push notifications.
    /// </summary>
    public class WebPushClient
    {
        /// <summary>Shape of the object <c>vfWebPush.subscribe</c> returns.</summary>
        private sealed record WebPushSubscriptionJs(
            [property: JsonPropertyName("endpoint")] string? Endpoint,
            [property: JsonPropertyName("p256dhKey")] string? P256dhKey,
            [property: JsonPropertyName("authKey")] string? AuthKey);

        private readonly IJSRuntime _js;
        private readonly NavigationManager _nav;

        public WebPushClient(IJSRuntime js, NavigationManager nav)
        {
            _js = js;
            _nav = nav;
        }

        /// <summary>
        /// Check if the browser supports the Web Push API.
        /// </summary>
        public async Task<bool> IsSupportedAsync()
        {
            try
            {
                return await _js.InvokeAsync<bool>("vfWebPush.isSupported");
            }
            catch (JSException)
            {
                return false;
            }
        }

        /// <summary>
        /// Get the current notification permission state.
        /// </summary>
        /// <returns>"granted", "denied", or "default"</returns>
        public async Task<string> GetPermissionStateAsync()
        {
            try
            {
                return await _js.InvokeAsync<string>("vfWebPush.getPermissionState") ?? "default";
            }
            catch (JSException)
            {
                return "default";
            }
        }

        /// <summary>
        /// Subscribe to push notifications with the given VAPID public key.
        /// </summary>
        /// <param name="vapidPublicKey">Base64url-encoded VAPID public key</param>
        /// <returns>Subscription details: endpoint, p256dh key, and auth key</returns>
        public async Task<(string Endpoint, string P256dhKey, string AuthKey)> SubscribeAsync(string vapidPublicKey)
        {
            JsonElement result;
            try
            {
                // Blazor returns a JS object as JsonElement; read it through a typed record, never a dynamic indexer.
                result = await _js.InvokeAsync<JsonElement>("vfWebPush.subscribe", vapidPublicKey);
            }
            catch (JSException ex)
            {
                throw new InvalidOperationException($"Push subscription failed: {ex.Message}", ex);
            }

            WebPushSubscriptionJs? subscription = null;
            if (result.ValueKind == JsonValueKind.Object)
            {
                try
                {
                    subscription = JsonSerializer.Deserialize<WebPushSubscriptionJs>(result.GetRawText());
                }
                catch (JsonException)
                {
                    // Malformed shape: reported below with the same failure as a missing result.
                }
            }

            // Validate at the boundary: a subscription without all three values cannot be stored or used.
            if (subscription is null
                || string.IsNullOrEmpty(subscription.Endpoint)
                || string.IsNullOrEmpty(subscription.P256dhKey)
                || string.IsNullOrEmpty(subscription.AuthKey))
            {
                throw new InvalidOperationException("Failed to subscribe to push notifications");
            }

            return (subscription.Endpoint, subscription.P256dhKey, subscription.AuthKey);
        }

        /// <summary>
        /// Unsubscribe from push notifications.
        /// </summary>
        /// <returns>The endpoint that was unsubscribed, or null if no subscription was active</returns>
        public async Task<string?> UnsubscribeAsync()
        {
            try
            {
                string? result = await _js.InvokeAsync<string?>("vfWebPush.unsubscribe");
                return result;
            }
            catch (JSException ex)
            {
                throw new InvalidOperationException($"Push unsubscription failed: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Get the current subscription endpoint without re-subscribing.
        /// </summary>
        /// <returns>The current subscription endpoint, or null if none exists</returns>
        public async Task<string?> GetCurrentSubscriptionEndpointAsync()
        {
            try
            {
                string? result = await _js.InvokeAsync<string?>("vfWebPush.getCurrentSubscriptionEndpoint");
                return result;
            }
            catch (JSException)
            {
                return null;
            }
        }
    }
}
