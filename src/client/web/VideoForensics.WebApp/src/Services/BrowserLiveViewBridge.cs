using System.Collections.Concurrent;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using SIPSorcery.Net;

using SIPSorceryMedia.Abstractions;

using VideoForensics.Core.Providers.Common.Contracts;

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
        /// <summary>RTP payload type of the OPUS audio track negotiated on the browser leg.</summary>
        private const int OpusPayloadType = 111;

        private readonly ILogger<BrowserLiveViewBridge> _logger;
        private readonly BrowserLiveViewOptions _options;
        private readonly ConcurrentDictionary<(string connectionId, Guid sessionId), BrowserLiveViewPeerConnection> _activePeers = new();

        /// <summary>
        /// Internal record tracking a browser-side peer connection and its RTP bridge subscription.
        /// </summary>
        private record BrowserLiveViewPeerConnection(
            RTCPeerConnection PeerConnection,
            IDisposable? RtpSubscription);

        public BrowserLiveViewBridge(ILogger<BrowserLiveViewBridge> logger, IOptions<BrowserLiveViewOptions> options)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
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

            // Reuse the existing leg for this key. A second peer plus a second RTP subscription
            // would leak: only one entry is stored, so the extra peer and handler can never be disposed.
            var key = (connectionId, sessionId);
            if (_activePeers.TryGetValue(key, out var existing))
            {
                return existing.PeerConnection;
            }

            _logger.LogDebug(
                "Creating browser peer connection for session {SessionId}, connection {ConnectionId}",
                sessionId, connectionId);

            // ICE servers come from configuration so tests can point at an unreachable STUN server.
            var config = new RTCConfiguration
            {
                iceServers = _options.IceServerUrls
                    .Select(url => new RTCIceServer { urls = url })
                    .ToList()
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

            // Subscribe to audio RTP packets and forward only the negotiated OPUS payload type (111).
            // Packets with any other payload type are dropped rather than sent under the wrong codec.
            Action<byte[], uint, int, int> audioRtpBridgeHandler = (payload, timestamp, markerBit, payloadType) =>
            {
                if (payloadType != OpusPayloadType)
                {
                    _logger.LogDebug(
                        "Dropping audio RTP packet with unexpected payload type {PayloadType} for session {SessionId}",
                        payloadType, sessionId);
                    return;
                }

                try
                {
                    pc.SendRtpRaw(SDPMediaTypesEnum.audio, payload, timestamp, markerBit, OpusPayloadType);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error forwarding audio RTP packet to browser peer for session {SessionId}", sessionId);
                }
            };

            ringConnection.OnVideoRtpPayload += rtpBridgeHandler;
            ringConnection.OnAudioRtpPayload += audioRtpBridgeHandler;

            // Store the peer and its subscriptions so we can clean up later
            var subscription = new RtpBridgeSubscription(ringConnection, rtpBridgeHandler, audioRtpBridgeHandler);
            var created = new BrowserLiveViewPeerConnection(pc, subscription);
            if (!_activePeers.TryAdd(key, created))
            {
                // Lost a race with a concurrent call for the same key. Tear down the peer we just built
                // and return the one already stored, so nothing stays subscribed or open.
                ringConnection.OnVideoRtpPayload -= rtpBridgeHandler;
                ringConnection.OnAudioRtpPayload -= audioRtpBridgeHandler;
                pc.close();
                pc.Dispose();

                if (_activePeers.TryGetValue(key, out var winner))
                {
                    return winner.PeerConnection;
                }

                // The racing entry was removed before we could read it. Retry from the top so the
                // caller still gets a live peer. The disposed one above is never returned.
                return await CreateBrowserLegAsync(connectionId, sessionId, ringConnection);
            }

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
                _logger.LogDebug(
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
            private readonly Action<byte[], uint, int> _videoHandler;
            private readonly Action<byte[], uint, int, int> _audioHandler;

            public RtpBridgeSubscription(
                ILiveViewConnection connection,
                Action<byte[], uint, int> videoHandler,
                Action<byte[], uint, int, int> audioHandler)
            {
                _connection = connection;
                _videoHandler = videoHandler;
                _audioHandler = audioHandler;
            }

            public void Dispose()
            {
                _connection.OnVideoRtpPayload -= _videoHandler;
                _connection.OnAudioRtpPayload -= _audioHandler;
            }
        }
    }
}
