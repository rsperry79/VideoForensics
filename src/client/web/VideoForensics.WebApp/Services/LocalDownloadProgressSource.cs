using System.Reactive.Linq;
using System.Reactive.Subjects;

using Microsoft.Extensions.Logging;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Providers.Common.Contracts;
using VideoForensics.Ui.Shared.Contracts;

namespace VideoForensics.WebApp.Services
{
    /// <summary>
    /// <see cref="IDownloadProgressSource"/> for the WebApp, which runs downloads in-process and has no realtime
    /// hub. It samples the local <see cref="IVideoDownloadService"/> on a fixed interval and pushes a snapshot
    /// only when the state changed or activity lines arrived. This replaces the panel's own polling timer.
    /// <para>
    /// Scoped per circuit, because <see cref="IVideoDownloadService"/> is scoped. Sampling starts on the first
    /// subscription and stops on <see cref="Dispose"/>, which the scope's disposal calls.
    /// </para>
    /// <para>
    /// Activity is drained exactly once per sample. The drained lines always travel with the sample that drained
    /// them, so none are lost when the state is otherwise unchanged.
    /// </para>
    /// </summary>
    public sealed class LocalDownloadProgressSource : IDownloadProgressSource, IDisposable
    {
        /// <summary>Default time between samples of the download service.</summary>
        public static readonly TimeSpan DefaultSampleInterval = TimeSpan.FromMilliseconds(750);

        private readonly IVideoDownloadService _downloadService;
        private readonly ILogger<LocalDownloadProgressSource> _logger;
        private readonly TimeSpan _sampleInterval;
        private readonly ReplaySubject<DownloadProgressSnapshot> _snapshots = new(1);
        private readonly CancellationTokenSource _cancellation = new();
        private readonly object _gate = new();
        private DownloadProgressSnapshot? _lastState;
        private bool _started;
        private bool _disposed;

        /// <summary>
        /// Creates the source over the circuit's download service.
        /// </summary>
        /// <param name="downloadService">The service sampled for progress. Its activity queue is drained by this source.</param>
        /// <param name="logger">Receives an error when a sample fails. A failed sample is skipped, not rethrown.</param>
        /// <param name="sampleInterval">Time between samples; defaults to <see cref="DefaultSampleInterval"/>.</param>
        public LocalDownloadProgressSource(
            IVideoDownloadService downloadService,
            ILogger<LocalDownloadProgressSource> logger,
            TimeSpan? sampleInterval = null)
        {
            _downloadService = downloadService;
            _logger = logger;
            _sampleInterval = sampleInterval ?? DefaultSampleInterval;
        }

        /// <inheritdoc />
        public IObservable<DownloadProgressSnapshot> Progress
        {
            get
            {
                return Observable.Defer(() =>
                {
                    EnsureSampling();
                    return _snapshots.AsObservable();
                });
            }
        }

        /// <summary>
        /// Takes one sample and emits it if the state changed or activity arrived. The sampling loop calls this on
        /// each tick. It is public so a caller can drive sampling deterministically.
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

                bool stateChanged = HasStateChanged(_lastState, current);
                _lastState = current;
                if (stateChanged || current.Activity.Count > 0)
                {
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
            DownloadStatus status = _downloadService.GetProgress();
            (int index, int total, string name) = _downloadService.GetCurrentDevice();

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
                CurrentDeviceIndex: index,
                CurrentDeviceTotal: total,
                CurrentDeviceName: name ?? string.Empty,
                // Copied: the service may return a live or reused dictionary, and a later mutation would otherwise
                // change the previous state this source compares against.
                PreScanCounts: new Dictionary<string, int>(_downloadService.GetPreScanCounts()),
                Activity: _downloadService.DrainActivityLog().ToArray(),
                LastError: _downloadService.GetLastError(),
                RemainingReason: _downloadService.GetRemainingReason());
        }

        /// <summary>
        /// Value comparison of everything except <see cref="DownloadProgressSnapshot.Activity"/>, which is transient
        /// and decided separately. Pre-scan counts compare by content, because the service may return a new
        /// dictionary on every call.
        /// </summary>
        private static bool HasStateChanged(DownloadProgressSnapshot? previous, DownloadProgressSnapshot current)
        {
            if (previous is null)
            {
                return true;
            }

            return previous.IsDownloading != current.IsDownloading
                || previous.FilesCompleted != current.FilesCompleted
                || previous.FilesTotal != current.FilesTotal
                || previous.BytesDownloaded != current.BytesDownloaded
                || previous.CurrentFile != current.CurrentFile
                || previous.TotalFilesCompleted != current.TotalFilesCompleted
                || previous.TotalFilesMatched != current.TotalFilesMatched
                || previous.TotalBytesDownloaded != current.TotalBytesDownloaded
                || previous.ActiveConnections != current.ActiveConnections
                || previous.CurrentSpeedMbps != current.CurrentSpeedMbps
                || previous.CurrentDeviceIndex != current.CurrentDeviceIndex
                || previous.CurrentDeviceTotal != current.CurrentDeviceTotal
                || previous.CurrentDeviceName != current.CurrentDeviceName
                || previous.LastError != current.LastError
                || previous.RemainingReason != current.RemainingReason
                || !PreScanCountsEqual(previous.PreScanCounts, current.PreScanCounts);
        }

        private static bool PreScanCountsEqual(IReadOnlyDictionary<string, int> left, IReadOnlyDictionary<string, int> right)
        {
            if (left.Count != right.Count)
            {
                return false;
            }

            foreach (KeyValuePair<string, int> entry in left)
            {
                if (!right.TryGetValue(entry.Key, out int value) || value != entry.Value)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
