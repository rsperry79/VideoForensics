using System.Collections.Concurrent;

using Microsoft.Extensions.Logging;

using SIPSorcery.Net;

using SIPSorceryMedia.Abstractions;

using VideoForensics.Providers.Common.Contracts;

namespace VideoForensics.WebApp.Services
{
    /// <summary>
    /// Manages browser-side WebRTC peer connections that bridge RTP packets from a Ring camera
    /// (or other provider) to a browser via a second RTCPeerConnection. Each browser operator
    /// gets their own peer connection and RTP bridging subscription.
    ///
    /// Registered as a Singleton so that peer connections persist across HTTP request/response cycles
    /// within a SignalR session.
    /// </summary>
    public class BrowserLiveViewBridge
    {
        private readonly ILogger<BrowserLiveViewBridge> _logger;
        private readonly ConcurrentDictionary<(string connectionId, Guid sessionId), BrowserLiveViewPeerConnection> _activePeers = new();

        /// <summary>
        /// Internal record tracking a browser-side peer connection and its RTP bridge subscription.
        /// </summary>
        private record BrowserLiveViewPeerConnection(
            RTCPeerConnection PeerConnection,
            IDisposable? RtpSubscription);

        public BrowserLiveViewBridge(ILogger<BrowserLiveViewBridge> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Creates a new browser-side WebRTC peer connection and subscribes it to receive video RTP packets
        /// from the provider's live connection. The browser peer is configured with H264 video (payload type 96)
        /// and OPUS audio to match the provider connection.
        /// </summary>
        /// <param name="connectionId">SignalR connection ID of the browser client.</param>
        /// <param name="sessionId">Live view session ID.</param>
        /// <param name="ringConnection">The provider's live connection to forward RTP packets from.</param>
        /// <returns>The created RTCPeerConnection, ready to negotiate offer/answer.</returns>
        /// <exception cref="ArgumentNullException">If ringConnection is null.</exception>
        public async Task<RTCPeerConnection> CreateBrowserLegAsync(
            string connectionId,
            Guid sessionId,
            ILiveViewConnection ringConnection)
        {
            if (ringConnection == null)
                throw new ArgumentNullException(nameof(ringConnection));

            _logger.LogInformation(
                "Creating browser peer connection for session {SessionId}, connection {ConnectionId}",
                sessionId, connectionId);

            // Configure RTCPeerConnection with STUN server
            var config = new RTCConfiguration
            {
                iceServers =
                [
                    new RTCIceServer { urls = "stun:stun.l.google.com:19302" }
                ]
            };
            var pc = new RTCPeerConnection(config);

            // Add video and audio tracks with the same codecs as the provider leg
            // H264 payload type 96 matches Ring's hardcoded configuration
            var videoFormats = new List<VideoFormat>
            {
                new(VideoCodecsEnum.H264, 96, 90000, "packetization-mode=1")
            };
            var audioFormats = new List<AudioFormat>
            {
                new(AudioCodecsEnum.OPUS, 111, 48000, 2, string.Empty)
            };

            pc.addTrack(new MediaStreamTrack(videoFormats, MediaStreamStatusEnum.SendRecv));
            pc.addTrack(new MediaStreamTrack(audioFormats, MediaStreamStatusEnum.SendRecv));

            // Subscribe to video RTP packets from the provider and forward to browser peer
            Action<byte[], uint, int> rtpBridgeHandler = (payload, timestamp, markerBit) =>
            {
                try
                {
                    // SendRtpRaw signature: (SDPMediaTypesEnum mediaType, byte[] payload, uint timestamp, int markerBit, int payloadType)
                    pc.SendRtpRaw(SDPMediaTypesEnum.video, payload, timestamp, markerBit, 96);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error forwarding RTP packet to browser peer for session {SessionId}", sessionId);
                }
            };

            ringConnection.OnVideoRtpPayload += rtpBridgeHandler;

            // Store the peer and its subscription so we can clean up later
            var subscription = new RtpBridgeSubscription(ringConnection, rtpBridgeHandler);
            var key = (connectionId, sessionId);
            _activePeers.TryAdd(key, new BrowserLiveViewPeerConnection(pc, subscription));

            return await Task.FromResult(pc);
        }

        /// <summary>
        /// Retrieves an existing browser peer connection for a given connection ID and session.
        /// </summary>
        public async Task<RTCPeerConnection?> GetBrowserLegAsync(string connectionId, Guid sessionId)
        {
            var key = (connectionId, sessionId);
            if (_activePeers.TryGetValue(key, out var entry))
            {
                return await Task.FromResult(entry.PeerConnection);
            }

            return await Task.FromResult<RTCPeerConnection?>(null);
        }

        /// <summary>
        /// Disposes a browser peer connection and unsubscribes it from the provider connection's RTP stream.
        /// Safe to call even if the connection does not exist.
        /// </summary>
        public async Task DisposeBrowserLegAsync(string connectionId, Guid sessionId)
        {
            var key = (connectionId, sessionId);
            if (_activePeers.TryRemove(key, out var entry))
            {
                _logger.LogInformation(
                    "Disposing browser peer connection for session {SessionId}, connection {ConnectionId}",
                    sessionId, connectionId);

                try
                {
                    entry.RtpSubscription?.Dispose();
                    entry.PeerConnection?.Dispose();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error disposing browser peer for session {SessionId}", sessionId);
                }
            }

            await Task.CompletedTask;
        }

        /// <summary>
        /// Disposes all browser peer connections associated with a SignalR connection ID.
        /// Called during hub disconnection to clean up all browser legs.
        /// </summary>
        public async Task DisposeAllForConnectionAsync(string connectionId)
        {
            var keysToRemove = _activePeers.Keys
                .Where(k => k.connectionId == connectionId)
                .ToList();

            foreach (var key in keysToRemove)
            {
                await DisposeBrowserLegAsync(key.connectionId, key.sessionId);
            }
        }

        /// <summary>
        /// Internal subscription holder to keep a reference to the RTP handler so we can unsubscribe later.
        /// </summary>
        private class RtpBridgeSubscription : IDisposable
        {
            private readonly ILiveViewConnection _connection;
            private readonly Action<byte[], uint, int> _handler;

            public RtpBridgeSubscription(ILiveViewConnection connection, Action<byte[], uint, int> handler)
            {
                _connection = connection;
                _handler = handler;
            }

            public void Dispose()
            {
                _connection.OnVideoRtpPayload -= _handler;
            }
        }
    }
}
