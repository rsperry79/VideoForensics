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

        /// <summary>
        /// Per-(connection, session) gates that serialize <see cref="RequestOffer"/>. Static because each
        /// hub invocation runs on a new hub instance. An entry exists only while a call holds or awaits
        /// its gate, so the table does not grow with every connection.
        /// </summary>
        private static readonly Dictionary<(string ConnectionId, Guid SessionId), OfferGate> OfferGates = new();

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
        /// Concurrent calls for the same connection and session share one leg and one offer.
        /// </summary>
        public async Task<string> RequestOffer(Guid liveViewSessionId)
        {
            try
            {
                _logger.LogInformation(
                    "RequestOffer from SignalR {ConnectionId} for session {SessionId}",
                    Context.ConnectionId, liveViewSessionId);

                CancellationToken ct = Context.ConnectionAborted;

                // Get the underlying Ring connection from the orchestrator
                var ringConnection = await _liveViewSessionService.GetConnectionForSessionAsync(liveViewSessionId, ct);
                if (ringConnection == null)
                {
                    throw new InvalidOperationException(
                        $"No live connection found for session {liveViewSessionId}. Session may have terminated.");
                }

                // Hold the gate across the leg lookup, creation, and offer. Without it, two calls can both miss
                // the leg, both subscribe an ICE push handler to the same peer, and both generate an offer.
                using IDisposable gate = await AcquireOfferGateAsync((Context.ConnectionId, liveViewSessionId), ct);

                // Create the browser-leg peer connection if it doesn't exist
                var browserPc = await _browserBridge.GetBrowserLegAsync(Context.ConnectionId, liveViewSessionId);
                if (browserPc == null)
                {
                    browserPc = await _browserBridge.CreateBrowserLegAsync(
                        Context.ConnectionId, liveViewSessionId, ringConnection);

                    // Set up ICE candidate handler - push candidates to browser as they're generated.
                    // The handler is synchronous, so the push can't be awaited here. Observe the outcome
                    // instead: an unobserved faulted task would drop the error silently.
                    browserPc.onicecandidate += candidate =>
                    {
                        if (candidate != null)
                        {
                            _ = Clients.Caller.SendAsync(
                                "ReceiveIceCandidate",
                                candidate.candidate,
                                candidate.sdpMLineIndex,
                                ct)
                                .ContinueWith(
                                    t => _logger.LogError(
                                        t.Exception,
                                        "Failed to push ICE candidate for session {SessionId}",
                                        liveViewSessionId),
                                    TaskContinuationOptions.OnlyOnFaulted);
                        }
                    };
                }

                // Reuse the offer already set on this leg. A second createOffer on the same peer would
                // replace the local description the browser is negotiating against.
                if (browserPc.localDescription?.type == RTCSdpType.offer)
                {
                    return browserPc.localDescription.sdp.ToString();
                }

                // Create offer
                var offer = browserPc.createOffer(null);
                await browserPc.setLocalDescription(offer);

                _logger.LogInformation(
                    "Sending offer for session {SessionId} to SignalR {ConnectionId}",
                    liveViewSessionId, Context.ConnectionId);

                // Return the local description, not the pre-set offer, so every call returns the same SDP.
                return browserPc.localDescription?.sdp.ToString() ?? offer.sdp;
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
        /// <exception cref="InvalidOperationException">
        /// If there is no browser leg for the session, or SIPSorcery rejects the answer.
        /// </exception>
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

                // SIPSorcery reports a rejected answer through its result, not an exception. Ignoring it
                // would leave the leg stuck with no media and no error for the browser.
                SetDescriptionResultEnum result = browserPc.setRemoteDescription(
                    new RTCSessionDescriptionInit
                    {
                        type = RTCSdpType.answer,
                        sdp = sdpAnswer
                    });
                if (result != SetDescriptionResultEnum.OK)
                {
                    throw new InvalidOperationException(
                        $"Browser answer rejected for session {liveViewSessionId}: {result}.");
                }

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
        /// <exception cref="ArgumentOutOfRangeException">
        /// If <paramref name="sdpMLineIndex"/> is outside 0..<see cref="ushort.MaxValue"/>.
        /// </exception>
        public async Task SubmitIceCandidate(Guid liveViewSessionId, string candidate, int sdpMLineIndex)
        {
            try
            {
                _logger.LogDebug(
                    "SubmitIceCandidate from SignalR {ConnectionId} for session {SessionId}",
                    Context.ConnectionId, liveViewSessionId);

                // Validate before narrowing. An unchecked (ushort) cast turns -1 into 65535 without error.
                if (sdpMLineIndex < 0 || sdpMLineIndex > ushort.MaxValue)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(sdpMLineIndex), sdpMLineIndex,
                        $"sdpMLineIndex must be between 0 and {ushort.MaxValue}.");
                }

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

        /// <summary>
        /// Waits for the gate for one (connection, session) key. Other keys never wait on it.
        /// </summary>
        private static async Task<IDisposable> AcquireOfferGateAsync((string ConnectionId, Guid SessionId) key, CancellationToken ct)
        {
            OfferGate gate;
            lock (OfferGates)
            {
                if (!OfferGates.TryGetValue(key, out gate!))
                {
                    gate = new OfferGate();
                    OfferGates[key] = gate;
                }
                gate.Holders++;
            }

            try
            {
                await gate.Semaphore.WaitAsync(ct);
            }
            catch
            {
                ReleaseHold(key, gate);
                throw;
            }

            return new OfferGateLease(key, gate);
        }

        /// <summary>
        /// Drops one hold on a gate and removes the table entry when no call holds or awaits it.
        /// </summary>
        private static void ReleaseHold((string ConnectionId, Guid SessionId) key, OfferGate gate)
        {
            lock (OfferGates)
            {
                if (--gate.Holders == 0)
                {
                    OfferGates.Remove(key);
                }
            }
        }

        /// <summary>
        /// Semaphore and waiter count for one offer key. <see cref="Holders"/> is guarded by the
        /// <see cref="OfferGates"/> lock.
        /// </summary>
        private sealed class OfferGate
        {
            public SemaphoreSlim Semaphore { get; } = new(1, 1);

            public int Holders { get; set; }
        }

        /// <summary>
        /// Releases the gate and drops this call's hold when disposed.
        /// </summary>
        private sealed class OfferGateLease : IDisposable
        {
            private readonly (string ConnectionId, Guid SessionId) _key;
            private readonly OfferGate _gate;
            private bool _disposed;

            public OfferGateLease((string ConnectionId, Guid SessionId) key, OfferGate gate)
            {
                _key = key;
                _gate = gate;
            }

            public void Dispose()
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                _gate.Semaphore.Release();
                ReleaseHold(_key, _gate);
            }
        }
    }
}
