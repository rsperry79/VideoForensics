using System;
using System.Text.Json;
using System.Threading.Tasks;

using KoenZomers.Ring.Api.Alarm;
using KoenZomers.Ring.Api.Entities;
using KoenZomers.Ring.Api.Sockets;
using KoenZomers.Ring.Api.Streaming;

using IWebSocketTransport = KoenZomers.Ring.Api.Sockets.IWebSocketTransport;

namespace KoenZomers.Ring.Api
{
    /// <summary>
    /// WebRTC live view - connects to a Ring camera's real-time video feed. Distinct from the
    /// recorded-event download methods elsewhere in Session (GetDoorbotHistoryRecording etc.), which
    /// remain the primary way this library is used.
    /// </summary>
    public partial class Session
    {
        private static readonly Uri SignalingServerBaseUri = new("wss://api.prod.signalling.ring.devices.a2z.com:443/ws");

        /// <summary>
        /// Starts a WebRTC live view session for the given camera. Caller is responsible for
        /// disposing the returned RingLiveViewSession when done with it.
        /// </summary>
        /// <param name="doorbotId">ID of the camera to start a live view for</param>
        /// <exception cref="Exceptions.AuthenticationFailedException">Thrown when the refresh token is invalid.</exception>
        /// <exception cref="Exceptions.SessionNotAuthenticatedException">Thrown when there's no OAuth token, or the OAuth token has expired and there is no valid refresh token.</exception>
        /// <exception cref="Exceptions.ThrottledException">Thrown when the web server indicates too many requests have been made (HTTP 429).</exception>
        public Task<RingLiveViewSession> StartLiveView(long doorbotId) =>
            StartLiveView(doorbotId, new ClientWebSocketTransport());

        /// <summary>
        /// Overload accepting an explicit IWebSocketTransport - used by tests to inject a fake
        /// transport instead of opening a real websocket, and available to consumers who want to
        /// supply their own transport (e.g. for logging or a proxied connection).
        /// </summary>
        public async Task<RingLiveViewSession> StartLiveView(long doorbotId, IWebSocketTransport transport)
        {
            await EnsureSessionValid();

            var ticketUri = new Uri(RingAppApiBaseUrl, "clap/ticket/request/signalsocket");
            var response = await _httpUtility.SendRequest(ticketUri, System.Net.Http.HttpMethod.Post, null, AuthenticationToken);
            var ticketResponse = JsonSerializer.Deserialize<ClapSignalingTicketResponse>(response);

            if (ticketResponse == null || string.IsNullOrEmpty(ticketResponse.Ticket))
            {
                throw new InvalidOperationException("Ring did not return a valid signaling ticket for this camera.");
            }

            var dialogId = Guid.NewGuid().ToString();
            var clientId = $"ring_site-{Guid.NewGuid()}";

            // Security Note: Token passed in URL is logged by proxies/servers. Future improvement:
            // Pass token via WebSocket headers or first message after connection instead.
            // ClientWebSocket.Options could be extended to support custom headers.
            var signalingUri = new Uri(
                $"{SignalingServerBaseUri}?api_version=4.0&auth_type=ring_solutions&client_id={clientId}&token={Uri.EscapeDataString(ticketResponse.Ticket)}");

            var signalingClient = new RingSignalingClient(transport, dialogId);
            await signalingClient.ConnectAsync(signalingUri);

            var session = new RingLiveViewSession(signalingClient, doorbotId);
            await session.StartAsync();
            return session;
        }
    }
}
