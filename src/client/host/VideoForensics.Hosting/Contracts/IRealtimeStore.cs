using VideoForensics.Api.Contracts;
using VideoForensics.Providers.Common.Contracts;

namespace VideoForensics.Hosting.Contracts
{
    /// <summary>
    /// Singleton holder of the latest value per real-time stream. It is the only subscriber to
    /// <see cref="IRealtimeHub"/>, so transient consumers read from here instead of subscribing per instance.
    /// Each stream replays its latest value to new subscribers. Before the first emission the stream is silent
    /// and the matching <c>Latest*</c> property returns null.
    /// </summary>
    public interface IRealtimeStore
    {
        /// <summary>Latest-value stream of download progress.</summary>
        IObservable<DownloadProgressDto> DownloadProgress { get; }

        /// <summary>Latest-value stream of urgent events. A new subscriber receives the most recent event, if any.</summary>
        IObservable<NotificationEvent> UrgentEvents { get; }

        /// <summary>Latest-value stream of admin-only self-test status.</summary>
        IObservable<SelfTestStatusDto> SelfTestStatus { get; }

        /// <summary>Latest-value stream of connection state.</summary>
        IObservable<ConnectionState> Connection { get; }

        /// <summary>Most recent download progress, or null before the first emission.</summary>
        DownloadProgressDto? LatestDownloadProgress { get; }

        /// <summary>Most recent urgent event, or null before the first emission.</summary>
        NotificationEvent? LatestUrgentEvent { get; }

        /// <summary>Most recent self-test status, or null before the first emission.</summary>
        SelfTestStatusDto? LatestSelfTestStatus { get; }

        /// <summary>Most recent connection state, or null before the first emission.</summary>
        ConnectionState? LatestConnectionState { get; }

        /// <summary>
        /// Returns the download activity lines received since the previous drain, oldest first, and clears the buffer.
        /// The buffer is bounded: beyond 500 lines the oldest are dropped, so a caller that never drains cannot grow memory.
        /// Draining is destructive and shared, so each line is returned to exactly one caller.
        /// </summary>
        IReadOnlyList<string> DrainActivityLog();
    }
}
