using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using SIPSorcery.Net;
using VideoForensics.Client.Common.Contracts;
using VideoForensics.WebApp.Services;

namespace VideoForensics.WebApp.Hubs
{
    /// <summary>
    /// SignalR hub for browser-side WebRTC video streaming. Coordinates the SDP offer/answer
    /// and ICE candidate exchange between the browser and the server's browser-leg RTCPeerConnection.
    /// The hub creates a second peer connection for each browser session, bridging RTP packets
    /// from the Ring camera's connection to the browser's WebRTC peer without re-encoding.
    ///
    /// Unlike LiveHub (send-only for paired MAUI devices), this hub has bidirectional RPC:
    /// the browser calls RequestOffer, SubmitAnswer, SubmitIceCandidate; the hub pushes back ICE candidates.
    /// This is the WebApp's own Blazor Server UI connecting directly, not a paired remote device.
    /// </summary>
    [Authorize]
    public class LiveViewSignalingHub : Hub
    {
        private readonly ILiveViewSessionService _liveViewSessionService;
        private readonly BrowserLiveViewBridge _browserBridge;
        private readonly ILogger<LiveViewSignalingHub> _logger;

        public LiveViewSignalingHub(
            ILiveViewSessionService liveViewSessionService,
            BrowserLiveViewBridge browserBridge,
            ILogger<LiveViewSignalingHub> logger)
        {
            _liveViewSessionService = liveViewSessionService ?? throw new ArgumentNullException(nameof(liveViewSessionService));
            _browserBridge = browserBridge ?? throw new ArgumentNullException(nameof(browserBridge));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Called by the browser to request the server's offer. Creates the browser-leg peer connection
        /// if it doesn't exist, creates an SDP offer, and returns it for the browser to set as remote.
        /// </summary>
        public async Task<string> RequestOffer(Guid liveViewSessionId)
        {
            try
            {
                _logger.LogInformation(
                    "RequestOffer from SignalR {ConnectionId} for session {SessionId}",
                    Context.ConnectionId, liveViewSessionId);

                // Get the underlying Ring connection from the orchestrator
                var ringConnection = await _liveViewSessionService.GetConnectionForSessionAsync(liveViewSessionId, CancellationToken.None);
                if (ringConnection == null)
                {
                    throw new InvalidOperationException(
                        $"No live connection found for session {liveViewSessionId}. Session may have terminated.");
                }

                // Create the browser-leg peer connection if it doesn't exist
                var browserPc = await _browserBridge.GetBrowserLegAsync(Context.ConnectionId, liveViewSessionId);
                if (browserPc == null)
                {
                    browserPc = await _browserBridge.CreateBrowserLegAsync(
                        Context.ConnectionId, liveViewSessionId, ringConnection);

                    // Set up ICE candidate handler - push candidates to browser as they're generated
                    browserPc.onicecandidate += candidate =>
                    {
                        if (candidate != null)
                        {
                            _ = Clients.Caller.SendAsync(
                                "ReceiveIceCandidate",
                                candidate.candidate,
                                candidate.sdpMLineIndex,
                                CancellationToken.None);
                        }
                    };
                }

                // Create offer
                var offer = browserPc.createOffer(null);
                await browserPc.setLocalDescription(offer);

                _logger.LogInformation(
                    "Sending offer for session {SessionId} to SignalR {ConnectionId}",
                    liveViewSessionId, Context.ConnectionId);

                return offer.sdp;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in RequestOffer for session {SessionId}", liveViewSessionId);
                throw;
            }
        }

        /// <summary>
        /// Called by the browser to submit its answer to the server's offer.
        /// Sets the remote description on the browser-leg peer connection.
        /// </summary>
        public async Task SubmitAnswer(Guid liveViewSessionId, string sdpAnswer)
        {
            try
            {
                _logger.LogInformation(
                    "SubmitAnswer from SignalR {ConnectionId} for session {SessionId}",
                    Context.ConnectionId, liveViewSessionId);

                var browserPc = await _browserBridge.GetBrowserLegAsync(Context.ConnectionId, liveViewSessionId);
                if (browserPc == null)
                {
                    throw new InvalidOperationException(
                        $"Browser peer connection not found for session {liveViewSessionId}.");
                }

                browserPc.setRemoteDescription(
                    new RTCSessionDescriptionInit
                    {
                        type = RTCSdpType.answer,
                        sdp = sdpAnswer
                    });

                _logger.LogInformation(
                    "Answer set for session {SessionId}, SignalR {ConnectionId}",
                    liveViewSessionId, Context.ConnectionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in SubmitAnswer for session {SessionId}", liveViewSessionId);
                throw;
            }
        }

        /// <summary>
        /// Called by the browser to submit an ICE candidate discovered on the browser side.
        /// Adds the candidate to the browser-leg peer connection.
        /// </summary>
        public async Task SubmitIceCandidate(Guid liveViewSessionId, string candidate, int sdpMLineIndex)
        {
            try
            {
                _logger.LogDebug(
                    "SubmitIceCandidate from SignalR {ConnectionId} for session {SessionId}",
                    Context.ConnectionId, liveViewSessionId);

                var browserPc = await _browserBridge.GetBrowserLegAsync(Context.ConnectionId, liveViewSessionId);
                if (browserPc == null)
                {
                    throw new InvalidOperationException(
                        $"Browser peer connection not found for session {liveViewSessionId}.");
                }

                browserPc.addIceCandidate(
                    new RTCIceCandidateInit
                    {
                        candidate = candidate,
                        sdpMLineIndex = (ushort)sdpMLineIndex
                    });

                _logger.LogDebug(
                    "ICE candidate added for session {SessionId}",
                    liveViewSessionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in SubmitIceCandidate for session {SessionId}", liveViewSessionId);
                throw;
            }
        }

        /// <summary>
        /// Called when the SignalR connection is disconnected. Cleans up all browser-leg connections
        /// associated with this connection ID.
        /// </summary>
        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            try
            {
                _logger.LogInformation(
                    "SignalR {ConnectionId} disconnected. Cleaning up browser peer connections.",
                    Context.ConnectionId);

                await _browserBridge.DisposeAllForConnectionAsync(Context.ConnectionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up browser peers for connection {ConnectionId}", Context.ConnectionId);
            }

            await base.OnDisconnectedAsync(exception);
        }
    }
}
