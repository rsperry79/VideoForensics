using System.Reactive.Linq;
using System.Reactive.Subjects;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Client.Ui.Shared.Contracts;

namespace VideoForensics.Client.WebApp.Services
{
    /// <summary>
    /// <see cref="IDownloadProgressSource"/> for the WebApp, which runs downloads in-process and has no realtime
    /// hub. It samples the <see cref="ISyncActivityLog"/> on a fixed interval and pushes a snapshot when activity
    /// lines arrive, plus one initial snapshot. This replaces the panel's own polling timer.
    /// <para>
    /// The sync activity log is the only live progress source: the download service's progress getters do not
    /// update during sync, so the numeric and status fields are always neutral.
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
        private readonly ILogger<LocalDownloadProgressSource> _logger;
        private readonly TimeSpan _sampleInterval;
        private readonly ReplaySubject<DownloadProgressSnapshot> _snapshots = new(1);
        private readonly CancellationTokenSource _cancellation = new();
        private readonly object _gate = new();
        private bool _hasEmitted;
        private bool _started;
        private bool _disposed;

        /// <summary>
        /// Creates the source over the sync activity log.
        /// </summary>
        /// <param name="syncActivity">The log drained for activity lines. Each sample consumes the lines it returns.</param>
        /// <param name="logger">Receives an error when a sample fails. A failed sample is skipped, not rethrown.</param>
        /// <param name="sampleInterval">Time between samples; defaults to <see cref="DefaultSampleInterval"/>.</param>
        public LocalDownloadProgressSource(
            ISyncActivityLog syncActivity,
            ILogger<LocalDownloadProgressSource> logger,
            TimeSpan? sampleInterval = null)
        {
            _syncActivity = syncActivity;
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
        /// Takes one sample and emits it if it carries activity lines, or if it is the first sample. The sampling
        /// loop calls this on each tick. It is public so a caller can drive sampling deterministically.
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

                // Idle ticks with no lines are not re-broadcast: the state is always neutral, so there is nothing
                // to compare. The first sample is emitted so a subscriber gets an initial value.
                if (!_hasEmitted || current.Activity.Count > 0)
                {
                    _hasEmitted = true;
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
            return new DownloadProgressSnapshot(
                IsDownloading: false,
                FilesCompleted: 0,
                FilesTotal: 0,
                BytesDownloaded: 0L,
                CurrentFile: null,
                TotalFilesCompleted: 0,
                TotalFilesMatched: 0,
                TotalBytesDownloaded: 0L,
                ActiveConnections: 0,
                CurrentSpeedMbps: 0.0,
                CurrentDeviceIndex: 0,
                CurrentDeviceTotal: 0,
                CurrentDeviceName: string.Empty,
                PreScanCounts: new Dictionary<string, int>(),
                Activity: _syncActivity.Drain(),
                LastError: null,
                RemainingReason: null);
        }
    }
}
