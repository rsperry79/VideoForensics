using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;

using VideoForensics.Api.Contracts;
using VideoForensics.Hosting.Contracts;
using VideoForensics.Providers.Common.Contracts;

namespace VideoForensics.Hosting
{
    /// <summary>
    /// Singleton latest-value store. It subscribes to each <see cref="IRealtimeHub"/> stream once, at construction,
    /// and keeps the most recent value in a <see cref="BehaviorSubject{T}"/>. Nullable subjects hold "no value yet"
    /// as null, and the public streams filter that out so subscribers only see real emissions.
    /// </summary>
    internal sealed class RealtimeStore : IRealtimeStore, IDisposable
    {
        private readonly object _gate = new();
        private readonly CompositeDisposable _hubSubscriptions = new();
        private readonly BehaviorSubject<DownloadProgressDto?> _downloadProgress = new(null);
        private readonly BehaviorSubject<NotificationEvent?> _urgentEvents = new(null);
        private readonly BehaviorSubject<SelfTestStatusDto?> _selfTestStatus = new(null);
        private readonly BehaviorSubject<ConnectionState?> _connection = new(null);
        private bool _disposed;

        /// <summary>Subscribes to every hub stream. Registration makes this the only subscriber to the hub.</summary>
        public RealtimeStore(IRealtimeHub hub)
        {
            ArgumentNullException.ThrowIfNull(hub);

            _hubSubscriptions.Add(hub.DownloadProgress.Subscribe(value => Publish(_downloadProgress, value)));
            _hubSubscriptions.Add(hub.UrgentEvents.Subscribe(value => Publish(_urgentEvents, value)));
            _hubSubscriptions.Add(hub.SelfTestStatus.Subscribe(value => Publish(_selfTestStatus, value)));
            _hubSubscriptions.Add(hub.Connection.Subscribe(value => Publish(_connection, value)));
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
        public DownloadProgressDto? LatestDownloadProgress => _downloadProgress.Value;

        /// <inheritdoc />
        public NotificationEvent? LatestUrgentEvent => _urgentEvents.Value;

        /// <inheritdoc />
        public SelfTestStatusDto? LatestSelfTestStatus => _selfTestStatus.Value;

        /// <inheritdoc />
        public ConnectionState? LatestConnectionState => _connection.Value;

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
