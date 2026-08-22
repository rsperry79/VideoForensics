using System;
using System.Text.Json;
using System.Threading.Tasks;

using VideoForensics.Providers.Ring.Alarm;
using VideoForensics.Providers.Ring.Entities;
using VideoForensics.Providers.Ring.Sockets;

using IWebSocketTransport = Ring.Api.Sockets.IWebSocketTransport;

namespace VideoForensics.Providers.Ring
{
    /// <summary>
    /// Full Ring Alarm security-panel control (arm/disarm), done over a persistent authenticated
    /// device-command websocket ("asset socket") - not to be confused with the simpler REST-based
    /// Location Mode API (Session.LocationMode.cs) used by camera-only accounts without an Alarm hub.
    /// </summary>
    public partial class Session
    {
        /// <summary>
        /// Base Uri for Ring's newer "api/v1" endpoints, used for asset socket tickets.
        /// </summary>
        public Uri RingAppApiBaseUrl => new Uri("https://api.ring.com/api/v1/");

        /// <summary>
        /// Requests a ticket and opens a persistent device-command websocket ("asset socket") for the
        /// given location, used to discover devices and control the Ring Alarm security panel.
        /// Caller is responsible for disposing the returned RingAssetSocket when done with it.
        /// </summary>
        /// <param name="locationId">ID of the location to connect an asset socket for</param>
        /// <exception cref="Exceptions.AuthenticationFailedException">Thrown when the refresh token is invalid.</exception>
        /// <exception cref="Exceptions.SessionNotAuthenticatedException">Thrown when there's no OAuth token, or the OAuth token has expired and there is no valid refresh token.</exception>
        /// <exception cref="Exceptions.ThrottledException">Thrown when the web server indicates too many requests have been made (HTTP 429).</exception>
        public Task<RingAssetSocket> ConnectAssetSocket(Guid locationId) =>
            ConnectAssetSocket(locationId, new ClientWebSocketTransport());

        /// <summary>
        /// Overload accepting an explicit IWebSocketTransport - used by tests to inject a fake
        /// transport instead of opening a real websocket, and available to consumers who want to
        /// supply their own transport (e.g. for logging or a proxied connection).
        /// </summary>
        public async Task<RingAssetSocket> ConnectAssetSocket(Guid locationId, IWebSocketTransport transport)
        {
            await EnsureSessionValid();

            var ticketUri = new Uri(RingAppApiBaseUrl,
                $"clap/tickets?locationID={locationId:D}&enableExtendedEmergencyCellUsage=true&requestedTransport=ws");
            var response = await _httpUtility.GetContents(ticketUri, AuthenticationToken, _hardwareId);
            var ticketResponse = JsonSerializer.Deserialize<ClapTicketResponse>(response);

            if (ticketResponse == null || string.IsNullOrEmpty(ticketResponse.Host) || string.IsNullOrEmpty(ticketResponse.Ticket))
            {
                throw new InvalidOperationException("Ring did not return a valid asset socket ticket for this location.");
            }

            var webSocketUri = new Uri($"wss://{ticketResponse.Host}/ws?authcode={ticketResponse.Ticket}&ack=false");

            var socket = new RingAssetSocket(transport, ticketResponse.Assets);
            await socket.ConnectAsync(webSocketUri);
            return socket;
        }
    }
}

