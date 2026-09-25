namespace VideoForensics.Providers.Common.Contracts
{
    /// <summary>
    /// Optional capability a provider implements when it can establish a live WebRTC connection
    /// to a camera and stream real-time media/telemetry. Not every provider supports live view —
    /// a provider without this capability simply has no implementation registered, and callers
    /// attempting live view on that provider's devices receive a "not supported" result.
    ///
    /// This interface and its DTOs form a boundary: provider implementations (Ring, Wyze, etc.)
    /// use their own internal types (SIPSorcery enums, etc.) and translate to these provider-agnostic
    /// DTOs at the adapter boundary, ensuring no vendor-specific types leak into layers above the
    /// provider tier (orchestrators, APIs, UI).
    /// </summary>
    public interface ILiveViewCapableProvider
    {
        /// <summary>
        /// Starts a live view connection to a device. The device ID is the provider's own identifier
        /// (e.g., a Ring doorbot ID as a string), not the DB Guid.
        /// </summary>
        /// <param name="providerDeviceId">Provider-specific device identifier (e.g., "doorbot:1234567" for Ring).</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>A connected live view session providing media and telemetry events.</returns>
        /// <exception cref="InvalidOperationException">Thrown if not authenticated or the device cannot be accessed.</exception>
        /// <exception cref="NotSupportedException">Thrown by provider implementations that do not support live view.</exception>
        Task<ILiveViewConnection> StartLiveViewAsync(string providerDeviceId, CancellationToken ct);
    }

    /// <summary>
    /// An active live view connection to a camera, providing real-time media stream callbacks
    /// and telemetry (RTCP receiver reports, bitrate samples, connection state changes).
    /// </summary>
    public interface ILiveViewConnection : IAsyncDisposable
    {
        /// <summary>
        /// Raised when an RTCP receiver report is received, containing packet loss, jitter, and timing data.
        /// Fired periodically as the peer reports reception statistics.
        /// </summary>
        event Action<RTCPReceiverReportSampleDto>? OnReceiverReport;

        /// <summary>
        /// Raised periodically with a bitrate sample (bits per second) computed from received RTP packets
        /// over the configured sampling interval.
        /// </summary>
        event Action<long>? OnBitrateSampleBps;

        /// <summary>
        /// Raised when the underlying peer connection's state changes (connecting, connected, disconnected, etc.).
        /// </summary>
        event Action<LiveViewConnectionStateDto>? OnConnectionStateChange;

        /// <summary>
        /// Closes the live view connection cleanly, stopping media flow and releasing underlying resources.
        /// Safe to call multiple times; subsequent calls are no-ops.
        /// </summary>
        Task CloseAsync(CancellationToken ct);
    }

    /// <summary>
    /// RTCP receiver report sample containing packet loss and jitter measurements.
    /// Extracted from an RTCPCompoundPacket and provided to callers in a provider-agnostic format.
    /// </summary>
    public record RTCPReceiverReportSampleDto(
        byte FractionLost,
        int PacketsLost,
        uint Jitter,
        DateTime ReceivedAtUtc
    );

    /// <summary>
    /// Live view connection state, normalized across all providers to a provider-agnostic enum.
    /// Maps from provider-specific enums (e.g., SIPSorcery's RTCPeerConnectionState).
    /// </summary>
    public enum LiveViewConnectionStateDto
    {
        Connecting = 0,
        Connected = 1,
        Disconnected = 2,
        Failed = 3,
        Closed = 4
    }
}
