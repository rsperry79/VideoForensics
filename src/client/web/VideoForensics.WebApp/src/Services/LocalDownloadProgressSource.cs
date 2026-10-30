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

        private readonly ISyncActivityLog _syncActivity;
        private readonly ISyncProgressStore _progress;
        private readonly ILogger<LocalDownloadProgressSource> _logger;
        private readonly TimeSpan _sampleInterval;
        private readonly ReplaySubject<DownloadProgressSnapshot> _snapshots = new(1);
        private readonly CancellationTokenSource _cancellation = new();
        private readonly object _gate = new();
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
        public IObservable<DownloadProgressSnapshot> Progress => Observable.Defer(() =>
                                                                              {
                                                                                  EnsureSampling();
                                                                                  return _snapshots.AsObservable();
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
                _logger.LogError(ex, "Download progress sample failed; skipping this tick");
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
                Activity: _syncActivity.Drain(),
                LastError: null,
                RemainingReason: null);
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
