using SIPSorcery.Net;

using SIPSorceryMedia.Abstractions;

using VideoForensics.Providers.Common.Contracts;
using VideoForensics.Providers.Ring.Streaming;

namespace VideoForensics.Providers.Ring.Services
{
    /// <summary>
    /// Adapter between Ring's <see cref="RingLiveViewSession"/> (provider-internal, SIPSorcery-based)
    /// and the provider-agnostic <see cref="ILiveViewConnection"/> interface. Translates SIPSorcery
    /// events and types to DTOs that don't leak vendor dependencies above the provider tier.
    /// </summary>
    public class RingLiveViewConnection : ILiveViewConnection
    {
        private readonly RingLiveViewSession _session;

        public event Action<RTCPReceiverReportSampleDto>? OnReceiverReport;
        public event Action<long>? OnBitrateSampleBps;
        public event Action<LiveViewConnectionStateDto>? OnConnectionStateChange;
        public event Action<byte[], uint, int>? OnVideoRtpPayload;

        public RingLiveViewConnection(RingLiveViewSession session)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));

            // Subscribe to the wrapped session's events and re-raise as DTOs
            _session.OnReceiverReport += HandleReceiverReport;
            _session.OnBitrateSampleBps += bitrateBps => OnBitrateSampleBps?.Invoke(bitrateBps);
            _session.OnConnectionStateChange += HandleConnectionStateChange;
            _session.OnRtpPacketReceived += HandleRtpPacket;
        }

        private void HandleReceiverReport(RingLiveViewSession.RTCPReceiverReportSample sample)
        {
            // Drop the MediaType field (not included in the DTO)
            var dto = new RTCPReceiverReportSampleDto(
                FractionLost: sample.FractionLost,
                PacketsLost: sample.PacketsLost,
                Jitter: sample.Jitter,
                ReceivedAtUtc: sample.ReceivedAtUtc
            );
            OnReceiverReport?.Invoke(dto);
        }

        private void HandleConnectionStateChange(RTCPeerConnectionState state)
        {
            // Map SIPSorcery's RTCPeerConnectionState enum to our provider-agnostic LiveViewConnectionStateDto
            var dto = MapConnectionState(state);
            OnConnectionStateChange?.Invoke(dto);
        }

        private static LiveViewConnectionStateDto MapConnectionState(RTCPeerConnectionState state)
        {
            return state switch
            {
                RTCPeerConnectionState.connecting => LiveViewConnectionStateDto.Connecting,
                RTCPeerConnectionState.connected => LiveViewConnectionStateDto.Connected,
                RTCPeerConnectionState.disconnected => LiveViewConnectionStateDto.Disconnected,
                RTCPeerConnectionState.failed => LiveViewConnectionStateDto.Failed,
                RTCPeerConnectionState.closed => LiveViewConnectionStateDto.Closed,
                // Fallback for any unexpected states (shouldn't happen in normal operation)
                _ => LiveViewConnectionStateDto.Disconnected
            };
        }

        private void HandleRtpPacket(System.Net.IPEndPoint ep, SDPMediaTypesEnum mediaType, RTPPacket packet)
        {
            // Filter to video-only packets and re-raise via the provider-agnostic event
            if (mediaType == SDPMediaTypesEnum.video && packet?.Payload != null)
            {
                OnVideoRtpPayload?.Invoke(packet.Payload, packet.Header.Timestamp, packet.Header.MarkerBit);
            }
        }

        public async Task CloseAsync(CancellationToken ct)
        {
            await _session.CloseAsync();
        }

        public async ValueTask DisposeAsync()
        {
            // Wrap the sync Dispose in ValueTask (RingLiveViewSession.Dispose is synchronous)
            _session?.Dispose();
            await ValueTask.CompletedTask;
        }
    }
}
