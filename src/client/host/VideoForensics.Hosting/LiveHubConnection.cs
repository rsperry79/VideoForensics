using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;

using VideoForensics.Providers.Common.Contracts;
using VideoForensics.Ui.Shared.Services;

namespace VideoForensics.Hosting
{
    /// <summary>
    /// Payload for a download-progress broadcast from the server (plan §6). Matches the shape
    /// sent by <see cref="VideoForensics.WebApp.Hubs.DownloadProgressBroadcastService"/>.
    /// </summary>
    public record DownloadProgressPayload(
        DownloadStatus Progress,
        int CurrentDeviceIndex,
        int CurrentDeviceTotal,
        string? CurrentDeviceName,
        IReadOnlyList<string> Activity,
        IReadOnlyDictionary<string, int> PreScanCounts);

    /// <summary>
    /// Manages a SignalR connection to the server's LiveHub, allowing remote clients (MAUI) to
    /// receive real-time updates for download progress and urgent events (plan §6). The hub
    /// requires paired-device bearer-token authentication; a client calls <see cref="StartAsync"/>
    /// only after obtaining a valid session token and wishes to begin receiving updates.
    /// </summary>
    public interface ILiveHubConnection
    {
        /// <summary>Fired when the server broadcasts a download-progress update.</summary>
        event Action<DownloadProgressPayload>? DownloadProgressReceived;

        /// <summary>Fired when the server broadcasts an urgent security event.</summary>
        event Action<NotificationEvent>? UrgentEventReceived;

        /// <summary>Starts connecting to the LiveHub on the server. Caller is responsible for ensuring a valid paired-device session token is available.</summary>
        Task StartAsync(CancellationToken ct);

        /// <summary>Stops the hub connection and disposes resources.</summary>
        Task StopAsync();
    }

    /// <summary>
    /// SignalR-backed implementation of <see cref="ILiveHubConnection"/> - connects to
    /// {serverAddress}/hubs/live with WebSocket transport and automatic reconnection (plan §6).
    /// </summary>
    public class LiveHubConnection : ILiveHubConnection, IAsyncDisposable
    {
        private readonly Uri _serverAddress;
        private readonly IServiceProvider _serviceProvider;
        private readonly PairedSessionState _sessionState;
        private HubConnection? _connection;
        private readonly object _lockObj = new();

        public event Action<DownloadProgressPayload>? DownloadProgressReceived;
        public event Action<NotificationEvent>? UrgentEventReceived;

        public LiveHubConnection(Uri serverAddress, IServiceProvider serviceProvider)
        {
            _serverAddress = serverAddress;
            _serviceProvider = serviceProvider;
            _sessionState = serviceProvider.GetRequiredService<PairedSessionState>();
        }

        /// <summary>
        /// Connects to the LiveHub. The bearer token is obtained on-demand via <see cref="GetSessionTokenAsync"/>,
        /// which reads from the circuit-scoped <see cref="PairedSessionState"/>, allowing the connection to
        /// refresh its auth as the session evolves without requiring reconnection. This token source is unified
        /// with the <see cref="PairedDeviceAuthHandler"/> used by all HTTP-backed <c>Remote*</c> repositories.
        /// </summary>
        public async Task StartAsync(CancellationToken ct)
        {
            lock (_lockObj)
            {
                if (_connection?.State != HubConnectionState.Disconnected)
                {
                    return; // Already connected or connecting
                }
            }

            var hubUri = new Uri(_serverAddress, "/hubs/live");

            _connection = new HubConnectionBuilder()
                .WithUrl(hubUri, options =>
                {
                    // WebSocket only - no transport auto-negotiation. The MAUI/client runtime
                    // always supports WebSockets, so there's no need for the fallback complexity.
                    options.Transports = HttpTransportType.WebSockets;

                    // Attach the paired-device session token as a bearer credential.
                    options.AccessTokenProvider = GetSessionTokenAsync;
                })
                .WithAutomaticReconnect()
                .Build();

            _ = _connection.On<DownloadProgressPayload>("DownloadProgress", payload =>
            {
                DownloadProgressReceived?.Invoke(payload);
            });

            _ = _connection.On<NotificationEvent>("UrgentEvent", notificationEvent =>
            {
                UrgentEventReceived?.Invoke(notificationEvent);
            });

            await _connection.StartAsync(ct);
        }

        public async Task StopAsync()
        {
            if (_connection != null)
            {
                await _connection.StopAsync();
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (_connection != null)
            {
                await _connection.DisposeAsync();
            }
        }

        /// <summary>
        /// Gets the current paired-device session token. Called on-demand by HubConnectionBuilder's
        /// AccessTokenProvider callback whenever the connection needs to authenticate (initially and
        /// on reconnect). Returns the token from the circuit-scoped <see cref="PairedSessionState"/>.
        /// </summary>
        private async Task<string?> GetSessionTokenAsync()
        {
            await Task.CompletedTask;
            return _sessionState.SessionToken;
        }
    }
}
