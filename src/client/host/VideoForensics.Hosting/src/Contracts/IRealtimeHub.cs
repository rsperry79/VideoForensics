using VideoForensics.Core.Api.Contracts;
using VideoForensics.Core.Providers.Common.Contracts;

namespace VideoForensics.Client.Hosting.Contracts
{
    /// <summary>
    /// Client-side view of the server's LiveHub. Each stream is an <see cref="IObservable{T}"/> of raw pushes
    /// as they arrive; the hub does not replay. Use <see cref="IRealtimeStore"/> when a caller needs the latest value.
    /// </summary>
    public interface IRealtimeHub
    {
        /// <summary>Download progress pushes (server sends a snapshot on connect, then only on change).</summary>
        IObservable<DownloadProgressDto> DownloadProgress { get; }

        /// <summary>Urgent security events.</summary>
        IObservable<NotificationEvent> UrgentEvents { get; }

        /// <summary>Admin-only self-test status. Non-admin sessions never receive emissions on this stream.</summary>
        IObservable<SelfTestStatusDto> SelfTestStatus { get; }

        /// <summary>Connection lifecycle transitions. Emits the current state to each new subscriber.</summary>
        IObservable<ConnectionState> Connection { get; }

        /// <summary>A video's thumbnail was produced on the server. Raw push, so a late subscriber gets nothing old.</summary>
        IObservable<ThumbnailReadyDto> ThumbnailReady { get; }

        /// <summary>Evidence for a device changed on the server. Raw push, so a late subscriber gets nothing old.</summary>
        IObservable<EvidenceChangedDto> EvidenceChanged { get; }

        /// <summary>
        /// Starts connecting to the hub. The first call connects; repeated calls while connecting or connected are no-ops.
        /// Throws if the initial connection attempt fails.
        /// </summary>
        Task StartAsync(CancellationToken cancellationToken);

        /// <summary>Stops the hub connection and any background reconnect loop.</summary>
        Task StopAsync();
    }
}
