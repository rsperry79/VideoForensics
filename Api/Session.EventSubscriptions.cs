using System;
using System.Threading.Tasks;

namespace KoenZomers.Ring.Api
{
    /// <summary>
    /// Push-notification subscription toggles for a doorbot's ding and motion events. Endpoint
    /// paths mirror ring-client-api's subscribeToDingEvents/subscribeToMotionEvents family.
    /// CONFIRMED VIA LIVE APITESTER RUN: Ring rejects these with HTTP 422 "Client Device required"
    /// for a session that has never registered a push receiver. Tried adding a device_id to the
    /// body (matching this session's hardware_id) - made no difference, same error. This strongly
    /// suggests Ring only accepts a subscribe/unsubscribe call once RegisterPushReceiver() has
    /// successfully registered a real push token (APNs/FCM) for this session first - which a
    /// headless/server-side client like this one has no real token to provide. Left unconfirmed
    /// beyond that; revisit if RegisterPushReceiver() is ever exercised with a genuine token.
    /// </summary>
    public partial class Session
    {
        /// <summary>Subscribes the current session to ding (doorbell press) push events for a doorbot.</summary>
        public Task SubscribeToDingEvents(long doorbotId) => PostDoorbotEventToggle(doorbotId, "subscribe");

        /// <summary>Unsubscribes the current session from ding push events for a doorbot.</summary>
        public Task UnsubscribeFromDingEvents(long doorbotId) => PostDoorbotEventToggle(doorbotId, "unsubscribe");

        /// <summary>Subscribes the current session to motion push events for a doorbot.</summary>
        public Task SubscribeToMotionEvents(long doorbotId) => PostDoorbotEventToggle(doorbotId, "motions_subscribe");

        /// <summary>Unsubscribes the current session from motion push events for a doorbot.</summary>
        public Task UnsubscribeFromMotionEvents(long doorbotId) => PostDoorbotEventToggle(doorbotId, "motions_unsubscribe");

        private async Task PostDoorbotEventToggle(long doorbotId, string action)
        {
            await EnsureSessionValid();

            var uri = new Uri(RingApiBaseUrl, $"doorbots/{doorbotId}/{action}");
            await _httpUtility.SendRequestWithExpectedStatusOutcome(uri, System.Net.Http.HttpMethod.Post, null, null, AuthenticationToken);
        }
    }
}
