using SIPSorcery.Net;

using SIPSorceryMedia.Abstractions;

using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace VideoForensics.Providers.Ring.Streaming
{
    /// <summary>
    /// A connected WebRTC live-view session for a Ring camera. Wraps a SIPSorcery RTCPeerConnection
    /// driven by a RingSignalingClient. Obtained via Session.StartLiveView.
    ///
    /// Scope: this establishes the connection and exposes raw decoded-frame/RTP callbacks - it does
    /// not itself mux received media into a savable file (e.g. .mp4). That's a separate, substantial
    /// media-engineering task left for a follow-up once this connection layer is verified against
    /// real hardware.
    ///
    /// Protocol inferred from dgreif/ring's streaming/webrtc-connection.ts (which uses Werift on the
    /// Node.js side); this uses SIPSorcery as the .NET equivalent WebRTC engine. Codec negotiation
    /// (H264 for video, Opus for audio) is a reasonable default matching what Ring cameras use, but
    /// has not been verified end-to-end against real hardware in this environment - do that before
    /// relying on this in production.
    /// </summary>
    public class RingLiveViewSession : IDisposable
    {
        private readonly RingSignalingClient _signaling;
        private readonly RTCPeerConnection _pc;
        private readonly long _doorbotId;
        private readonly int _bitrateTimerIntervalMs;
        private readonly Timer _bitrateTimer;
        private long _rtpBytesAccumulated;

        // ICE candidates gathered before the offer is sent. Guarded by _iceGate, which also guards _offerSent.
        private readonly object _iceGate = new();
        private readonly List<(string Candidate, int MLineIndex)> _pendingIce = new();
        private bool _offerSent;

        /// <summary>
        /// Raised for each received, already-decoded video frame.
        /// </summary>
        public event Action<IPEndPoint, uint, byte[], VideoFormat> OnVideoFrameReceived;

        /// <summary>
        /// Raised for each received, encoded audio frame.
        /// </summary>
        public event Action<EncodedAudioFrame> OnAudioFrameReceived;

        /// <summary>
        /// Raised for every raw RTP packet received, for callers that want to do their own
        /// decoding/muxing rather than relying on SIPSorcery's built-in decoders.
        /// </summary>
        public event Action<IPEndPoint, SDPMediaTypesEnum, RTPPacket> OnRtpPacketReceived;

        /// <summary>
        /// Raised when the underlying peer connection's state changes.
        /// </summary>
        public event Action<RTCPeerConnectionState> OnConnectionStateChange;

        /// <summary>
        /// Raised when an RTCP receiver report is received with reception report data.
        /// </summary>
        public event Action<RTCPReceiverReportSample> OnReceiverReport;

        /// <summary>
        /// Raised periodically with a bitrate sample (bits per second) computed from received RTP packets.
        /// </summary>
        public event Action<long> OnBitrateSampleBps;

        /// <summary>
        /// RTCP receiver report sample data extracted from an RTCPCompoundPacket.
        /// </summary>
        public record RTCPReceiverReportSample(
            SDPMediaTypesEnum MediaType,
            byte FractionLost,
            int PacketsLost,
            uint Jitter,
            DateTime ReceivedAtUtc
        );

        internal RingLiveViewSession(RingSignalingClient signaling, long doorbotId, int? bitrateTimerIntervalMs = null)
        {
            _signaling = signaling;
            _doorbotId = doorbotId;
            _bitrateTimerIntervalMs = bitrateTimerIntervalMs ?? 1000; // Default 1 second
            _rtpBytesAccumulated = 0;

            var config = new RTCConfiguration
            {
                iceServers =
                [
                    new RTCIceServer { urls = "stun:stun.l.google.com:19302" }
                ]
            };
            _pc = new RTCPeerConnection(config);

            var videoFormats = new List<VideoFormat>
            {
                // The profile and level match the reference client's offer. Ring's pipeline needs them to start.
                new(VideoCodecsEnum.H264, 96, 90000, "packetization-mode=1;profile-level-id=640029;level-asymmetry-allowed=1")
            };
            var audioFormats = new List<AudioFormat>
            {
                new(AudioCodecsEnum.OPUS, 111, 48000, 2, string.Empty)
            };

            _pc.addTrack(new MediaStreamTrack(videoFormats, MediaStreamStatusEnum.RecvOnly));
            // Sendrecv, as the reference client offers: Ring's pipeline expects the audio return path to be negotiated.
            _pc.addTrack(new MediaStreamTrack(audioFormats, MediaStreamStatusEnum.SendRecv));

            _pc.OnVideoFrameReceived += (ep, timestamp, frame, format) => OnVideoFrameReceived?.Invoke(ep, timestamp, frame, format);
            _pc.OnAudioFrameReceived += frame => OnAudioFrameReceived?.Invoke(frame);
            _pc.OnRtpPacketReceived += (ep, mediaType, packet) =>
            {
                OnRtpPacketReceived?.Invoke(ep, mediaType, packet);
                // Accumulate bytes for bitrate tracking
                if (packet?.Payload != null)
                {
                    _ = Interlocked.Add(ref _rtpBytesAccumulated, packet.Payload.Length);
                }
            };
            _pc.onconnectionstatechange += state => OnConnectionStateChange?.Invoke(state);

            // Subscribe to RTCP receiver reports
            _pc.OnReceiveReport += HandleReceiveReport;

            _pc.onicecandidate += candidate =>
            {
                if (candidate == null)
                {
                    return;
                }

                // Ring rejects a session whose ICE arrives before its live_view offer, so candidates found earlier wait here.
                lock (_iceGate)
                {
                    if (!_offerSent)
                    {
                        _pendingIce.Add((candidate.candidate, candidate.sdpMLineIndex));
                        return;
                    }
                }

                _ = _signaling.SendIceCandidateAsync(_doorbotId, candidate.candidate, candidate.sdpMLineIndex);
            };

            _signaling.OnSdpAnswer += sdp =>
                _pc.setRemoteDescription(new RTCSessionDescriptionInit { type = RTCSdpType.answer, sdp = sdp });

            _signaling.OnIceCandidate += (candidate, mlineIndex) =>
                _pc.addIceCandidate(new RTCIceCandidateInit { candidate = candidate, sdpMLineIndex = (ushort)mlineIndex });

            // Start bitrate sampling timer
            _bitrateTimer = new Timer(_ => SampleBitrate(), null, _bitrateTimerIntervalMs, _bitrateTimerIntervalMs);
        }

        /// <summary>
        /// Creates the SDP offer and sends it to Ring over the signaling connection, kicking off the
        /// SDP/ICE exchange. The connection is not necessarily fully established when this returns -
        /// subscribe to OnConnectionStateChange to know when media actually starts flowing.
        /// </summary>
        internal async Task StartAsync()
        {
            RTCSessionDescriptionInit offer = _pc.createOffer(null);
            await _pc.setLocalDescription(offer);
            await _signaling.SendOfferAsync(_doorbotId, offer.sdp);

            List<(string Candidate, int MLineIndex)> pending;
            lock (_iceGate)
            {
                _offerSent = true;
                pending = new List<(string Candidate, int MLineIndex)>(_pendingIce);
                _pendingIce.Clear();
            }

            foreach ((string candidate, int mLineIndex) in pending)
            {
                _ = _signaling.SendIceCandidateAsync(_doorbotId, candidate, mLineIndex);
            }
        }

        public async Task CloseAsync()
        {
            _bitrateTimer?.Dispose();
            _pc.close();
            await _signaling.CloseAsync();
        }

        public void Dispose()
        {
            _bitrateTimer?.Dispose();
            _pc.close();
            _signaling.Dispose();
        }

        private void HandleReceiveReport(IPEndPoint endpoint, SDPMediaTypesEnum mediaType, RTCPCompoundPacket compoundPacket)
        {
            if (compoundPacket == null)
            {
                return;
            }

            // Check for receiver report (remote sent us a report about what it's receiving from us)
            if (compoundPacket.ReceiverReport?.ReceptionReports != null)
            {
                foreach (ReceptionReportSample? report in compoundPacket.ReceiverReport.ReceptionReports)
                {
                    if (report != null)
                    {
                        var sample = new RTCPReceiverReportSample(
                            mediaType,
                            report.FractionLost,
                            report.PacketsLost,
                            report.Jitter,
                            DateTime.UtcNow
                        );
                        OnReceiverReport?.Invoke(sample);
                    }
                }
            }

            // Also check for sender report (we sent data, remote is reporting back)
            if (compoundPacket.SenderReport?.ReceptionReports != null)
            {
                foreach (ReceptionReportSample? report in compoundPacket.SenderReport.ReceptionReports)
                {
                    if (report != null)
                    {
                        var sample = new RTCPReceiverReportSample(
                            mediaType,
                            report.FractionLost,
                            report.PacketsLost,
                            report.Jitter,
                            DateTime.UtcNow
                        );
                        OnReceiverReport?.Invoke(sample);
                    }
                }
            }
        }

        private void SampleBitrate()
        {
            long bytes = Interlocked.Exchange(ref _rtpBytesAccumulated, 0);
            long bits = bytes * 8;
            long bitsPerSecond = bits * 1000 / _bitrateTimerIntervalMs;
            OnBitrateSampleBps?.Invoke(bitsPerSecond);
        }

        /// <summary>
        /// Test seam: simulates receiving an RTP packet with the given payload size.
        /// Used to inject bytes for bitrate tracking testing.
        /// </summary>
        public void SimulateRtpPacket(int payloadSize)
        {
            _ = Interlocked.Add(ref _rtpBytesAccumulated, payloadSize);
        }
    }
}
