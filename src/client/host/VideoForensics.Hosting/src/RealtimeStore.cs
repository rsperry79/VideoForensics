using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;

using VideoForensics.Core.Api.Contracts;
using VideoForensics.Core.Providers.Common.Contracts;
using VideoForensics.Client.Hosting.Contracts;

namespace VideoForensics.Client.Hosting
{
    /// <summary>
    /// Singleton latest-value store. It subscribes to each <see cref="IRealtimeHub"/> stream once, at construction,
    /// and keeps the most recent value in a <see cref="BehaviorSubject{T}"/>. Nullable subjects hold "no value yet"
    /// as null, and the public streams filter that out so subscribers only see real emissions.
    /// </summary>
    internal sealed class RealtimeStore : IRealtimeStore, IDisposable
    {
        private const int MaxActivityLogLines = 500;

        private readonly object _gate = new();
        private readonly CompositeDisposable _hubSubscriptions = [];
        private readonly BehaviorSubject<DownloadProgressDto?> _downloadProgress = new(null);
        private readonly BehaviorSubject<NotificationEvent?> _urgentEvents = new(null);
        private readonly BehaviorSubject<SelfTestStatusDto?> _selfTestStatus = new(null);
        private readonly BehaviorSubject<ConnectionState?> _connection = new(null);
        // Events, not state: no replay, so a late subscriber only hears about thumbnails produced after it subscribed.
        private readonly Subject<ThumbnailReadyDto> _thumbnailReady = new();
        private readonly Subject<EvidenceChangedDto> _evidenceChanged = new();
        // Activity lines are not state: every line must reach a drainer, so they are queued rather than latest-value.
        // The cap stops a caller that never drains from growing memory without limit; the oldest lines go first.
        private readonly Queue<string> _activityLog = new();
        private bool _disposed;

        /// <summary>Subscribes to every hub stream. Registration makes this the only subscriber to the hub.</summary>
        public RealtimeStore(IRealtimeHub hub)
        {
            ArgumentNullException.ThrowIfNull(hub);

            _hubSubscriptions.Add(hub.DownloadProgress.Subscribe(OnDownloadProgress));
            _hubSubscriptions.Add(hub.UrgentEvents.Subscribe(value => Publish(_urgentEvents, value)));
            _hubSubscriptions.Add(hub.SelfTestStatus.Subscribe(value => Publish(_selfTestStatus, value)));
            _hubSubscriptions.Add(hub.Connection.Subscribe(value => Publish(_connection, value)));
            _hubSubscriptions.Add(hub.ThumbnailReady.Subscribe(value => PublishEvent(_thumbnailReady, value)));
            _hubSubscriptions.Add(hub.EvidenceChanged.Subscribe(value => PublishEvent(_evidenceChanged, value)));
        }

        /// <inheritdoc />
        public IObservable<DownloadProgressDto> DownloadProgress => _downloadProgress.Where(v => v is not null).Select(v => v!);

        /// <inheritdoc />
        public IObservable<NotificationEvent> UrgentEvents => _urgentEvents.Where(v => v is not null).Select(v => v!);

        /// <inheritdoc />
        public IObservable<SelfTestStatusDto> SelfTestStatus => _selfTestStatus.Where(v => v is not null).Select(v => v!);

        /// <inheritdoc />
        public IObservable<ConnectionState> Connection => _connection.Where(v => v.HasValue).Select(v => v!.Value);

        /// <inheritdoc />
        public IObservable<ThumbnailReadyDto> ThumbnailReady => _thumbnailReady.AsObservable();

        /// <inheritdoc />
        public IObservable<EvidenceChangedDto> EvidenceChanged => _evidenceChanged.AsObservable();

        /// <inheritdoc />
        public DownloadProgressDto? LatestDownloadProgress => _downloadProgress.Value;

        /// <inheritdoc />
        public NotificationEvent? LatestUrgentEvent => _urgentEvents.Value;

        /// <inheritdoc />
        public SelfTestStatusDto? LatestSelfTestStatus => _selfTestStatus.Value;

        /// <inheritdoc />
        public ConnectionState? LatestConnectionState => _connection.Value;

        /// <inheritdoc />
        public IReadOnlyList<string> DrainActivityLog()
        {
            lock (_gate)
            {
                string[] lines = _activityLog.ToArray();
                _activityLog.Clear();
                return lines;
            }
        }

        /// <summary>Releases the hub subscriptions and completes the store's streams.</summary>
        public void Dispose()
        {
            lock (_gate)
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
            }

            _hubSubscriptions.Dispose();
            _downloadProgress.OnCompleted();
            _urgentEvents.OnCompleted();
            _selfTestStatus.OnCompleted();
            _connection.OnCompleted();
            _thumbnailReady.OnCompleted();
            _evidenceChanged.OnCompleted();
        }

        // Event streams carry no latest value, so they are published without a BehaviorSubject. Same lock as Publish,
        // so a hub callback arriving during Dispose is dropped rather than emitted into a completed subject.
        private void PublishEvent<T>(Subject<T> subject, T value)
        {
            lock (_gate)
            {
                if (!_disposed)
                {
                    subject.OnNext(value);
                }
            }
        }

        // Buffers the activity lines and publishes the payload under one lock, so a drain never sees a payload's lines
        // half-queued. A null Activity (not expected from the wire, but tolerated) contributes no lines.
        private void OnDownloadProgress(DownloadProgressDto value)
        {
            lock (_gate)
            {
                if (_disposed)
                {
                    return;
                }

                if (value.Activity is not null)
                {
                    foreach (string line in value.Activity)
                    {
                        _activityLog.Enqueue(line);
                    }

                    while (_activityLog.Count > MaxActivityLogLines)
                    {
                        _ = _activityLog.Dequeue();
                    }
                }

                _downloadProgress.OnNext(value);
            }
        }

        // Subject OnNext is not safe to call from multiple threads at once, and hub callbacks arrive on SignalR threads.
        // The lock serializes them and drops anything that arrives after Dispose.
        private void Publish<T>(BehaviorSubject<T> subject, T value)
        {
            lock (_gate)
            {
                if (!_disposed)
                {
                    subject.OnNext(value);
                }
            }
        }
    }
}
