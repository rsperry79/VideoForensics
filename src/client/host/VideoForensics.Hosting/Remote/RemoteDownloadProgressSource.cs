using System.Reactive.Linq;

using VideoForensics.Api.Contracts;
using VideoForensics.Hosting.Contracts;
using VideoForensics.Ui.Shared.Contracts;

namespace VideoForensics.Hosting.Remote
{
    /// <summary>
    /// <see cref="IDownloadProgressSource"/> for remote (MAUI) hosts. It forwards the server's download progress
    /// from the shared <see cref="IRealtimeStore"/> and maps each <see cref="DownloadProgressDto"/> to a
    /// <see cref="DownloadProgressSnapshot"/>, so Ui.Shared never sees the wire type.
    /// <para>
    /// Activity is taken from each emission and never drained from the store. Draining is destructive and
    /// shared, so the store's activity queue is left alone.
    /// </para>
    /// <para>
    /// The store replays its latest value to a new subscriber. That replay has already been shown, so its
    /// activity lines are dropped to stop the panel from appending them again each time it is mounted.
    /// </para>
    /// </summary>
    public sealed class RemoteDownloadProgressSource : IDownloadProgressSource
    {
        private readonly IRealtimeStore _store;

        /// <summary>Creates the source over the singleton realtime store.</summary>
        public RemoteDownloadProgressSource(IRealtimeStore store)
        {
            _store = store;
        }

        /// <inheritdoc />
        public IObservable<DownloadProgressSnapshot> Progress
        {
            get
            {
                // Read per subscription, not at construction, so each panel sees whether the store already
                // held a value when it subscribed.
                return Observable.Defer(() =>
                {
                    bool replaysLatest = _store.LatestDownloadProgress is not null;
                    return _store.DownloadProgress.Select(
                        (dto, index) => ToSnapshot(dto, includeActivity: !(replaysLatest && index == 0)));
                });
            }
        }

        private static DownloadProgressSnapshot ToSnapshot(DownloadProgressDto dto, bool includeActivity)
        {
            DownloadStatusDto status = dto.Progress;
            return new DownloadProgressSnapshot(
                IsDownloading: status.IsDownloading,
                FilesCompleted: status.FilesCompleted,
                FilesTotal: status.FilesTotal,
                BytesDownloaded: status.BytesDownloaded,
                CurrentFile: status.CurrentFile,
                TotalFilesCompleted: status.TotalFilesCompleted,
                TotalFilesMatched: status.TotalFilesMatched,
                TotalBytesDownloaded: status.TotalBytesDownloaded,
                ActiveConnections: status.ActiveConnections,
                CurrentSpeedMbps: status.CurrentSpeedMbps,
                CurrentDeviceIndex: dto.CurrentDeviceIndex,
                CurrentDeviceTotal: dto.CurrentDeviceTotal,
                CurrentDeviceName: dto.CurrentDeviceName ?? string.Empty,
                PreScanCounts: dto.PreScanCounts,
                Activity: includeActivity ? dto.Activity : Array.Empty<string>(),
                LastError: dto.LastError,
                RemainingReason: dto.RemainingReason);
        }
    }
}
