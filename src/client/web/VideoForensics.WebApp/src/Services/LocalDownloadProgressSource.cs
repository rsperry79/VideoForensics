using System.Reactive.Linq;
using System.Reactive.Subjects;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Client.Hosting.Contracts;
using VideoForensics.Client.Ui.Shared.Contracts;

namespace VideoForensics.Client.WebApp.Services
{
    /// <summary>
    /// <see cref="IDownloadProgressSource"/> for the WebApp, which runs downloads in-process and has no realtime
    /// hub. It samples the <see cref="ISyncActivityLog"/> and the <see cref="ISyncProgressStore"/> on a fixed interval.
    /// It pushes a snapshot when activity lines arrive, when the progress reading changes, and once initially. This
    /// replaces the panel's own polling timer.
    /// <para>
    /// Progress comes from the store, which each pull writes while it downloads. Activity lines come from the log.
    /// </para>
    /// <para>
    /// Activity is drained exactly once per sample. The drained lines always travel with the sample that drained
    /// them, so none are lost.
    /// </para>
    /// </summary>
    public sealed class LocalDownloadProgressSource : IDownloadProgressSource, IDisposable
    {
        /// <summary>Default time between samples of the activity log.</summary>
        public static readonly TimeSpan DefaultSampleInterval = TimeSpan.FromMilliseconds(750);

        /// <summary>Activity lines a new subscriber is replayed: the most recent ones, matching the log's own retention.</summary>
        private const int MaxSeenActivityLines = 500;

        private readonly ISyncActivityLog _syncActivity;
        private readonly ISyncProgressStore _progress;
        private readonly ILogger<LocalDownloadProgressSource> _logger;
        private readonly TimeSpan _sampleInterval;
        private readonly Subject<DownloadProgressSnapshot> _snapshots = new();
        private readonly CancellationTokenSource _cancellation = new();
        private readonly object _gate = new();
        private readonly List<string> _seenActivity = new();
        private long _activityPosition;
        private DownloadProgressSnapshot? _lastEmitted;
        private bool _started;
        private bool _disposed;

        /// <summary>
        /// Creates the source over the sync activity log.
        /// </summary>
        /// <param name="syncActivity">The log drained for activity lines. Each sample consumes the lines it returns.</param>
        /// <param name="progress">The store the pulls write live per-file progress to. Read on each sample, never consumed.</param>
        /// <param name="logger">Receives an error when a sample fails. A failed sample is skipped, not rethrown.</param>
        /// <param name="sampleInterval">Time between samples; defaults to <see cref="DefaultSampleInterval"/>.</param>
        public LocalDownloadProgressSource(
            ISyncActivityLog syncActivity,
            ISyncProgressStore progress,
            ILogger<LocalDownloadProgressSource> logger,
            TimeSpan? sampleInterval = null)
        {
            _syncActivity = syncActivity;
            _progress = progress;
            _logger = logger;
            _sampleInterval = sampleInterval ?? DefaultSampleInterval;
        }

        /// <inheritdoc />
        /// <remarks>
        /// A new subscriber first receives the latest state with every activity line seen so far, so a page opened
        /// mid-sync restores its output, then the live snapshots.
        /// </remarks>
        public IObservable<DownloadProgressSnapshot> Progress => Observable.Create<DownloadProgressSnapshot>(observer =>
        {
            EnsureSampling();
            lock (_gate)
            {
                IDisposable subscription = _snapshots.Subscribe(observer);
                if (_lastEmitted is not null)
                {
                    observer.OnNext(_lastEmitted with { Activity = _seenActivity.ToArray() });
                }

                return subscription;
            }
        });

        /// <summary>
        /// Takes one sample and emits it if it carries activity lines, if its progress state differs from the last emitted
        /// one, or if it is the first sample. The sampling loop calls this on each tick. It is public so a caller can
        /// drive sampling deterministically.
        /// </summary>
        public void SampleOnce()
        {
            DownloadProgressSnapshot current;
            try
            {
                current = Capture();
            }
            catch (Exception ex)
            {
                _logger.LogError("Download progress sample failed; skipping this tick: {Message} ({ErrorType})", ex.Message, ex.GetType().Name);
                return;
            }

            lock (_gate)
            {
                if (_disposed)
                {
                    return;
                }

                // An identical idle repeat is not re-broadcast. The first sample is emitted so a subscriber gets an
                // initial value. A changed reading or any activity line is always sent.
                bool stateChanged = _lastEmitted is null || !SameState(_lastEmitted, current);
                if (current.Activity.Count > 0)
                {
                    _seenActivity.AddRange(current.Activity);
                    int excess = _seenActivity.Count - MaxSeenActivityLines;
                    if (excess > 0)
                    {
                        _seenActivity.RemoveRange(0, excess);
                    }
                }

                if (stateChanged || current.Activity.Count > 0)
                {
                    _lastEmitted = current;
                    _snapshots.OnNext(current);
                }
            }
        }

        /// <summary>Stops sampling and completes subscribers.</summary>
        public void Dispose()
        {
            lock (_gate)
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                _snapshots.OnCompleted();
            }

            _cancellation.Cancel();
            _cancellation.Dispose();
        }

        private void EnsureSampling()
        {
            lock (_gate)
            {
                if (_started || _disposed)
                {
                    return;
                }

                _started = true;
                // Capture the token now: the source may be disposed before the task body first runs.
                CancellationToken token = _cancellation.Token;
                _ = Task.Run(() => RunAsync(token));
            }
        }

        private async Task RunAsync(CancellationToken cancellationToken)
        {
            using var timer = new PeriodicTimer(_sampleInterval);
            try
            {
                while (await timer.WaitForNextTickAsync(cancellationToken))
                {
                    SampleOnce();
                }
            }
            catch (OperationCanceledException)
            {
                // Dispose stopped the loop. This is the normal shutdown path.
            }
        }

        private DownloadProgressSnapshot Capture()
        {
            SyncProgress reading = _progress.Current;
            return new DownloadProgressSnapshot(
                IsDownloading: reading.IsDownloading,
                FilesCompleted: reading.FilesCompleted,
                FilesTotal: reading.FilesTotal,
                BytesDownloaded: reading.BytesDownloaded,
                CurrentFile: reading.CurrentFile,
                TotalFilesCompleted: 0,
                TotalFilesMatched: 0,
                TotalBytesDownloaded: 0L,
                ActiveConnections: reading.ActiveConnections,
                CurrentSpeedMbps: 0.0,
                CurrentDeviceIndex: reading.DeviceIndex,
                CurrentDeviceTotal: reading.DeviceTotal,
                CurrentDeviceName: reading.DeviceName ?? string.Empty,
                PreScanCounts: new Dictionary<string, int>(),
                Activity: ReadNewActivity(),
                LastError: null,
                RemainingReason: null);
        }

        /// <summary>
        /// Reads the lines appended since this source last read. The log is shared with the hub broadcaster and other
        /// pages, so this reads by position and never removes lines from it.
        /// </summary>
        private IReadOnlyList<string> ReadNewActivity()
        {
            IReadOnlyList<string> lines = _syncActivity.ReadSince(_activityPosition, out long next);
            _activityPosition = next;
            return lines;
        }

        /// <summary>
        /// Compares the progress state, ignoring <see cref="DownloadProgressSnapshot.Activity"/>. Compared field by field
        /// because each capture builds new collections, so record equality would always report a change.
        /// </summary>
        private static bool SameState(DownloadProgressSnapshot left, DownloadProgressSnapshot right)
        {
            return left.IsDownloading == right.IsDownloading
                && left.FilesCompleted == right.FilesCompleted
                && left.FilesTotal == right.FilesTotal
                && left.BytesDownloaded == right.BytesDownloaded
                && left.CurrentFile == right.CurrentFile
                && left.ActiveConnections == right.ActiveConnections
                && left.CurrentDeviceIndex == right.CurrentDeviceIndex
                && left.CurrentDeviceTotal == right.CurrentDeviceTotal
                && left.CurrentDeviceName == right.CurrentDeviceName
                && left.LastError == right.LastError
                && left.RemainingReason == right.RemainingReason
                && left.PreScanCounts.Count == right.PreScanCounts.Count;
        }
    }
}
