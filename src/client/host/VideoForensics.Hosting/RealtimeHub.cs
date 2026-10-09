using System.Reactive.Linq;
using System.Reactive.Subjects;

using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using VideoForensics.Api.Contracts;
using VideoForensics.Hosting.Contracts;
using VideoForensics.Providers.Common.Contracts;
using VideoForensics.Ui.Shared.Services;

namespace VideoForensics.Hosting
{
    /// <summary>
    /// SignalR-backed <see cref="IRealtimeHub"/> for the server's /hubs/live. It is the only type that touches
    /// <see cref="HubConnection"/>. Pushes are raw (no replay), and <see cref="IRealtimeStore"/> keeps the latest values.
    /// </summary>
    /// <remarks>
    /// Reconnect behavior: an indefinite <see cref="CappedBackoffRetryPolicy"/> drives the hub's own automatic
    /// reconnect. A close that is not a 401 falls back to a backoff loop here. A 401 at any point stops retrying and
    /// emits <see cref="ConnectionState.AuthFailed"/>, because a rejected token cannot succeed on retry.
    /// </remarks>
    internal sealed class RealtimeHub : IRealtimeHub, IAsyncDisposable
    {
        private static readonly TimeSpan ReconnectBaseDelay = TimeSpan.FromSeconds(1);
        private static readonly TimeSpan ReconnectMaxDelay = TimeSpan.FromSeconds(30);

        private readonly Uri _serverAddress;
        private readonly PairedSessionState _sessionState;
        private readonly ILogger _logger;
        private readonly Func<CancellationToken, Task> _connectAsync;
        private readonly CappedBackoffRetryPolicy _retryPolicy = new(ReconnectBaseDelay, ReconnectMaxDelay);
        private readonly object _gate = new();
        private readonly Subject<DownloadProgressDto> _downloadProgress = new();
        private readonly Subject<NotificationEvent> _urgentEvents = new();
        private readonly Subject<SelfTestStatusDto> _selfTestStatus = new();
        private readonly Subject<LiveViewSessionDto> _liveViewSessionChanged = new();
        private readonly BehaviorSubject<ConnectionState> _connection = new(ConnectionState.Disconnected);
        // Guards the subscription set only. It is separate from _gate so a subscriber that re-enters a subscribe call
        // from inside an emission cannot deadlock on the hub's state lock.
        private readonly object _subscriptionGate = new();
        private readonly HashSet<Guid> _liveViewSubscriptions = new();
        private readonly Func<string, Guid, CancellationToken, Task>? _invokeOverride;
        private HubConnection? _hubConnection;
        private CancellationTokenSource _lifetime = new();
        private bool _stopping;
        private bool _disposed;

        /// <summary>Creates the hub for <paramref name="serverAddress"/>. The paired-session token is read on each connect.</summary>
        public RealtimeHub(Uri serverAddress, IServiceProvider serviceProvider)
            : this(serverAddress, serviceProvider, connectOverride: null)
        {
        }

        /// <summary>
        /// Test seam: <paramref name="connectOverride"/> replaces the real HubConnection connect step, and
        /// <paramref name="invokeOverride"/> replaces the hub invoke used for live-view subscriptions.
        /// </summary>
        internal RealtimeHub(
            Uri serverAddress,
            IServiceProvider serviceProvider,
            Func<CancellationToken, Task>? connectOverride,
            Func<string, Guid, CancellationToken, Task>? invokeOverride = null)
        {
            ArgumentNullException.ThrowIfNull(serverAddress);
            ArgumentNullException.ThrowIfNull(serviceProvider);

            _serverAddress = serverAddress;
            _sessionState = serviceProvider.GetRequiredService<PairedSessionState>();
            _logger = (ILogger?)serviceProvider.GetService<ILoggerFactory>()?.CreateLogger<RealtimeHub>() ?? NullLogger.Instance;
            _connectAsync = connectOverride ?? ConnectHubAsync;
            _invokeOverride = invokeOverride;
        }

        /// <inheritdoc />
        public IObservable<DownloadProgressDto> DownloadProgress => _downloadProgress.AsObservable();

        /// <inheritdoc />
        public IObservable<NotificationEvent> UrgentEvents => _urgentEvents.AsObservable();

        /// <inheritdoc />
        public IObservable<SelfTestStatusDto> SelfTestStatus => _selfTestStatus.AsObservable();

        /// <inheritdoc />
        public IObservable<ConnectionState> Connection => _connection.AsObservable();

        /// <summary>
        /// Live-view session state pushes ("LiveViewSessionChanged") for sessions this client subscribed to via
        /// <see cref="SubscribeLiveViewAsync"/>. Pushes are raw (no replay).
        /// </summary>
        public IObservable<LiveViewSessionDto> LiveViewSessionChanged => _liveViewSessionChanged.AsObservable();

        /// <summary>True while the live hub is connected. Lets <see cref="RealtimeLiveViewSessionSource"/> report availability.</summary>
        internal bool IsLiveHubConnected => IsConnected();

        /// <summary>
        /// Joins the server's live-view group for <paramref name="sessionId"/> so its state pushes are delivered.
        /// The session is remembered and re-subscribed after every reconnect. If the connection is not up, the
        /// subscription is recorded and sent on the next connect. A session the server rejects is dropped, not retried.
        /// </summary>
        /// <param name="sessionId">The live-view session to follow.</param>
        /// <param name="cancellationToken">Cancels the outgoing invoke.</param>
        public async Task SubscribeLiveViewAsync(Guid sessionId, CancellationToken cancellationToken)
        {
            lock (_subscriptionGate)
            {
                _ = _liveViewSubscriptions.Add(sessionId);
            }

            if (!IsConnected())
            {
                _logger.LogDebug("Live-view subscription recorded; it will be sent when the live hub connects");
                return;
            }

            await SendSubscribeAsync(sessionId, cancellationToken);
        }

        /// <summary>
        /// Leaves the server's live-view group for <paramref name="sessionId"/> and stops replaying it on reconnect.
        /// </summary>
        /// <param name="sessionId">The live-view session to stop following.</param>
        /// <param name="cancellationToken">Cancels the outgoing invoke.</param>
        public async Task UnsubscribeLiveViewAsync(Guid sessionId, CancellationToken cancellationToken)
        {
            lock (_subscriptionGate)
            {
                _ = _liveViewSubscriptions.Remove(sessionId);
            }

            if (!IsConnected())
            {
                return;
            }

            await InvokeHubAsync("UnsubscribeLiveView", sessionId, cancellationToken);
        }

        /// <inheritdoc />
        /// <remarks>
        /// The guard is on the observed state, not on whether a HubConnection exists, so the first call always connects.
        /// Calls made while connecting, connected or reconnecting are no-ops.
        /// </remarks>
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            lock (_gate)
            {
                ObjectDisposedException.ThrowIf(_disposed, this);

                ConnectionState current = _connection.Value;
                if (current is ConnectionState.Connecting or ConnectionState.Connected or ConnectionState.Reconnecting)
                {
                    return;
                }

                _stopping = false;
                Publish(ConnectionState.Connecting);
            }

            try
            {
                await _connectAsync(cancellationToken);
            }
            catch (Exception ex) when (HubAuthFailure.IsAuthFailure(ex))
            {
                _logger.LogError(ex, "Live hub rejected the session token (401); not retrying until the device is re-paired");
                Publish(ConnectionState.AuthFailed);
                throw;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // The caller gave up. Do not start a background retry on their behalf.
                _logger.LogInformation("Live hub start was cancelled before connecting");
                Publish(ConnectionState.Disconnected);
                throw;
            }
            catch (Exception ex)
            {
                // Transient failure (server not up yet, network down). Keep retrying in the background instead of
                // throwing, so an app launched before the server is reachable recovers without a restart.
                // If StopAsync already ran, the failure is the stop's doing; starting a retry now would resurrect the hub.
                CancellationToken lifetimeToken;
                lock (_gate)
                {
                    if (_stopping)
                    {
                        _logger.LogInformation("Live hub initial connection failed after stop was requested; not retrying");
                        Publish(ConnectionState.Disconnected);
                        return;
                    }

                    lifetimeToken = _lifetime.Token;
                    Publish(ConnectionState.Reconnecting);
                }

                _logger.LogWarning(ex, "Live hub initial connection failed; retrying with backoff");
                _ = ReconnectLoopAsync(lifetimeToken);
                return;
            }

            // Check and publish under one lock. StopAsync takes the same lock, so a stop cannot slip between them.
            lock (_gate)
            {
                if (_stopping)
                {
                    _logger.LogInformation("Live hub connect completed after stop was requested; not reporting Connected");
                    Publish(ConnectionState.Disconnected);
                    return;
                }

                _logger.LogInformation("Live hub connected");
                Publish(ConnectionState.Connected);
            }

            // After Connected is published: a subscribe that saw Connected sends its own invoke, and one that saw
            // anything else was recorded before this snapshot, so it is included here.
            await ReplaySubscriptionsAsync(cancellationToken);
        }

        /// <inheritdoc />
        public async Task StopAsync()
        {
            CancellationTokenSource previousLifetime;
            HubConnection? connection;
            lock (_gate)
            {
                if (_disposed)
                {
                    return;
                }

                _stopping = true;
                previousLifetime = _lifetime;
                _lifetime = new CancellationTokenSource();
                connection = _hubConnection;
            }

            // Cancelling stops any background reconnect loop before the hub connection is stopped.
            previousLifetime.Cancel();
            if (connection is not null)
            {
                await connection.StopAsync();
            }

            Publish(ConnectionState.Disconnected);
            _logger.LogInformation("Live hub stopped");
        }

        /// <summary>
        /// Handles a HubConnection Closed event. Callers pass the closing error, or null for a clean close.
        /// Internal so tests can drive it without a network.
        /// </summary>
        internal async Task OnConnectionClosedAsync(Exception? error)
        {
            bool stopping;
            lock (_gate)
            {
                stopping = _stopping || _disposed;
            }

            if (stopping)
            {
                Publish(ConnectionState.Disconnected);
                return;
            }

            if (HubAuthFailure.IsAuthFailure(error))
            {
                _logger.LogError(error, "Live hub connection closed with 401; reconnect stopped until the device is re-paired");
                await ClearExpiredSessionAsync();
                Publish(ConnectionState.AuthFailed);
                return;
            }

            _logger.LogWarning(error, "Live hub connection closed unexpectedly; reconnecting with backoff");
            Publish(ConnectionState.Reconnecting);
            _ = ReconnectLoopAsync(_lifetime.Token);
        }

        /// <inheritdoc />
        public async ValueTask DisposeAsync()
        {
            CancellationTokenSource lifetime;
            HubConnection? connection;
            lock (_gate)
            {
                if (_disposed)
                {
                    return;
                }

                _stopping = true;
                lifetime = _lifetime;
                connection = _hubConnection;
                _hubConnection = null;
            }

            lifetime.Cancel();
            if (connection is not null)
            {
                await connection.DisposeAsync();
            }

            lock (_gate)
            {
                Publish(ConnectionState.Disconnected);
                _disposed = true;
            }

            _downloadProgress.OnCompleted();
            _urgentEvents.OnCompleted();
            _selfTestStatus.OnCompleted();
            _liveViewSessionChanged.OnCompleted();
            _connection.OnCompleted();
            lifetime.Dispose();
            _logger.LogInformation("Live hub disposed");
        }

        // Fire-and-forget entry point. Any exception escaping the loop body would otherwise be unobserved, so it is
        // logged here and the state is published as Disconnected. A cancelled loop exits quietly; its canceller
        // already published the terminal state.
        private async Task ReconnectLoopAsync(CancellationToken cancellationToken)
        {
            try
            {
                await RunReconnectLoopAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Stopped or disposed.
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Live hub reconnect loop stopped unexpectedly; the live feed is offline until the next start");
                Publish(ConnectionState.Disconnected);
            }
        }

        // Retries the connect with backoff after a non-401 close. Ends on success, 401, or cancellation.
        private async Task RunReconnectLoopAsync(CancellationToken cancellationToken)
        {
            int attempt = 0;
            while (true)
            {
                TimeSpan delay = _retryPolicy.DelayForAttempt(attempt);
                try
                {
                    await Task.Delay(delay, cancellationToken);
                    await _connectAsync(cancellationToken);
                    _logger.LogInformation("Live hub reconnected after {Attempts} attempt(s)", attempt + 1);
                    Publish(ConnectionState.Connected);
                    await ReplaySubscriptionsAsync(cancellationToken);
                    return;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex) when (HubAuthFailure.IsAuthFailure(ex))
                {
                    _logger.LogError(ex, "Live hub reconnect rejected with 401; reconnect stopped until the device is re-paired");
                    await ClearExpiredSessionAsync();
                    Publish(ConnectionState.AuthFailed);
                    return;
                }
                catch (Exception ex)
                {
                    attempt++;
                    _logger.LogWarning(ex, "Live hub reconnect attempt {Attempt} failed; retrying in {DelaySeconds}s", attempt, delay.TotalSeconds);
                }
            }
        }

        // Builds the HubConnection once and reuses it, so restarts do not leak old connections.
        private async Task ConnectHubAsync(CancellationToken cancellationToken)
        {
            HubConnection connection;
            lock (_gate)
            {
                _hubConnection ??= BuildHubConnection();
                connection = _hubConnection;
            }

            await connection.StartAsync(cancellationToken);
        }

        private HubConnection BuildHubConnection()
        {
            var hubUri = new Uri(_serverAddress, "/hubs/live");

            HubConnection connection = new HubConnectionBuilder()
                .WithUrl(hubUri, options =>
                {
                    // WebSocket only. The MAUI and desktop runtimes always support it, so no transport negotiation is needed.
                    options.Transports = HttpTransportType.WebSockets;
                    options.AccessTokenProvider = GetSessionTokenAsync;
                })
                .WithAutomaticReconnect(_retryPolicy)
                .Build();

            _ = connection.On<DownloadProgressDto>("DownloadProgress", payload => Emit(_downloadProgress, payload));
            _ = connection.On<NotificationEvent>("UrgentEvent", notificationEvent => Emit(_urgentEvents, notificationEvent));
            _ = connection.On<SelfTestStatusDto>("SelfTestStatus", status => Emit(_selfTestStatus, status));
            _ = connection.On<LiveViewSessionDto>("LiveViewSessionChanged", dto => EmitLiveViewSessionChanged(dto));

            connection.Reconnecting += error =>
            {
                _logger.LogWarning(error, "Live hub connection lost; reconnecting");
                Publish(ConnectionState.Reconnecting);
                return Task.CompletedTask;
            };
            connection.Reconnected += _ => HandleReconnectedAsync();
            connection.Closed += OnConnectionClosedAsync;

            return connection;
        }

        // Reads the current paired-session token on each connect and reconnect. Read here rather than captured,
        // so a re-pair takes effect without rebuilding the connection.
        private Task<string?> GetSessionTokenAsync() => Task.FromResult(_sessionState.SessionToken);

        private async Task ClearExpiredSessionAsync()
        {
            try
            {
                await _sessionState.NotifyAuthenticationExpiredAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to clear the expired paired session");
            }
        }

        // Takes _gate itself. The lock is reentrant, so it is safe to call while already holding it (as StartAsync does).
        private void Publish(ConnectionState state)
        {
            lock (_gate)
            {
                if (!_disposed && _connection.Value != state)
                {
                    _connection.OnNext(state);
                }
            }
        }

        /// <summary>
        /// Routes a "LiveViewSessionChanged" push to subscribers. The hub binding calls this; it is internal so tests
        /// can drive it without a network.
        /// </summary>
        internal void EmitLiveViewSessionChanged(LiveViewSessionDto dto) => Emit(_liveViewSessionChanged, dto);

        private void Emit<T>(Subject<T> subject, T value)
        {
            lock (_gate)
            {
                if (!_disposed)
                {
                    subject.OnNext(value);
                }
            }
        }

        private bool IsConnected()
        {
            lock (_gate)
            {
                return _connection.Value == ConnectionState.Connected;
            }
        }

        private CancellationToken CurrentLifetimeToken()
        {
            lock (_gate)
            {
                return _lifetime.Token;
            }
        }

        /// <summary>
        /// Handles the HubConnection Reconnected event. Internal so tests can drive it without a network.
        /// </summary>
        /// <remarks>
        /// The server drops group membership on disconnect, so the live-view subscriptions are re-sent here.
        /// </remarks>
        internal async Task HandleReconnectedAsync()
        {
            _logger.LogInformation("Live hub reconnected");
            Publish(ConnectionState.Connected);
            await ReplaySubscriptionsAsync(CurrentLifetimeToken());
        }

        // Re-sends every tracked subscription. Called after Connected is published on each successful connect.
        private async Task ReplaySubscriptionsAsync(CancellationToken cancellationToken)
        {
            Guid[] snapshot;
            lock (_subscriptionGate)
            {
                snapshot = _liveViewSubscriptions.ToArray();
            }

            foreach (Guid sessionId in snapshot)
            {
                await SendSubscribeAsync(sessionId, cancellationToken);
            }
        }

        // A HubException means the server rejected the session (for example, it does not exist). The id is dropped
        // so a bad id is not replayed on every reconnect. Other failures propagate to the caller.
        private async Task SendSubscribeAsync(Guid sessionId, CancellationToken cancellationToken)
        {
            try
            {
                await InvokeHubAsync("SubscribeLiveView", sessionId, cancellationToken);
            }
            catch (HubException ex)
            {
                lock (_subscriptionGate)
                {
                    _ = _liveViewSubscriptions.Remove(sessionId);
                }

                // The session id is not logged: it is a GUID, and only the failure matters here.
                _logger.LogWarning(ex, "Live hub rejected a live-view subscription; it will not be replayed");
            }
        }

        private Task InvokeHubAsync(string method, Guid sessionId, CancellationToken cancellationToken)
        {
            if (_invokeOverride is not null)
            {
                return _invokeOverride(method, sessionId, cancellationToken);
            }

            HubConnection? connection;
            lock (_gate)
            {
                connection = _hubConnection;
            }

            if (connection is null)
            {
                throw new InvalidOperationException("Live hub connection has not been built");
            }

            return connection.InvokeAsync(method, sessionId, cancellationToken);
        }
    }
}
