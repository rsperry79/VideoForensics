using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

using VideoForensics.Api.Contracts;
using VideoForensics.Client.Common.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Hosting;
using VideoForensics.WebApp.Auth;

namespace VideoForensics.WebApp.Hubs
{
    /// <summary>
    /// One real-time channel serving both download progress and urgent-event push (plan §6) - a
    /// consolidation of what an earlier plan draft had as two competing mechanisms (HTTP polling
    /// for progress, a separately-floated SignalR idea for urgent events). Intended for remote
    /// paired clients (MAUI today); the WebApp's own Blazor Server UI does not need this hub at all
    /// - its interactive circuit already gets live updates via ordinary <c>StateHasChanged</c> calls
    /// on an in-process wrapper (plan §6's explicit "no separate real-time infrastructure needed for
    /// the server's own UI").
    ///
    /// Sends only, deliberately, with one exception: the hub accepts exactly two subscription calls
    /// (<c>SubscribeLiveView</c>, <c>UnsubscribeLiveView</c>) for per-session live-view group
    /// membership, and no other client calls. Triggering actions (starting or stopping live view, a
    /// "StartDownload" RPC, and so on) stays a plain HTTP POST per plan §6, kept separate from this
    /// push channel's own concerns.
    /// </summary>
    [Authorize(AuthenticationSchemes = PairedDeviceAuthenticationDefaults.SchemeName, Policy = VideoForensicsPolicies.ReadOnly)]
    public class LiveHub : Hub
    {
        private readonly ILiveConnectionTracker _connectionTracker;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<LiveHub> _logger;

        public LiveHub(ILiveConnectionTracker connectionTracker, IServiceScopeFactory scopeFactory, ILogger<LiveHub> logger)
        {
            _connectionTracker = connectionTracker;
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        public override async Task OnConnectedAsync()
        {
            Guid? deviceId = GetPairedDeviceId();
            if (deviceId is not null)
            {
                _connectionTracker.Register(deviceId.Value, Context);
            }

            // Admin gate shared by the admins-group join and the admin-only SelfTestStatus snapshot.
            string? roleClaim = Context.User?.FindFirst(VideoForensicsClaimTypes.Role)?.Value;
            bool isAdmin = Enum.TryParse<OperatorRole>(roleClaim, out OperatorRole role) && role >= OperatorRole.Admin;

            // Add admins to the admins group for admin-only notifications
            if (isAdmin)
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, "admins", CancellationToken.None);
            }

            // Snapshot-on-connect: a new client (including every reconnect) gets the current state
            // immediately instead of waiting for the next change. Uses the non-draining builder so
            // this connection never consumes activity lines that other clients still need.
            // Snapshots are non-fatal: a failing data source must not stop the connection from opening.
            // Errors are logged and the connection continues; rethrow only when the connection is aborting,
            // since that cancellation is the real reason the send failed.
            try
            {
                DownloadProgressDto snapshot;
                using (IServiceScope scope = _scopeFactory.CreateScope())
                {
                    IVideoDownloadService downloadService = scope.ServiceProvider.GetRequiredService<IVideoDownloadService>();
                    snapshot = downloadService.ToDownloadProgressSnapshot();
                }

                await Clients.Caller.SendAsync(LiveHubMethods.DownloadProgress, snapshot, Context.ConnectionAborted);
            }
            catch (Exception ex)
            {
                if (Context.ConnectionAborted.IsCancellationRequested)
                {
                    throw;
                }

                _logger.LogError(ex, "Failed to send download progress snapshot to connection {ConnectionId}", Context.ConnectionId);
            }

            // Admin-only snapshot-on-connect for the self-test status. Non-admins never receive this
            // stream, and their connections don't touch the self-test service at all.
            if (isAdmin)
            {
                try
                {
                    SelfTestStatusDto selfTestSnapshot;
                    using (IServiceScope scope = _scopeFactory.CreateScope())
                    {
                        IRingSelfTestService selfTestService = scope.ServiceProvider.GetRequiredService<IRingSelfTestService>();
                        selfTestSnapshot = await selfTestService.GetStatusAsync(Context.ConnectionAborted);
                    }

                    await Clients.Caller.SendAsync(LiveHubMethods.SelfTestStatus, selfTestSnapshot, Context.ConnectionAborted);
                }
                catch (Exception ex)
                {
                    if (Context.ConnectionAborted.IsCancellationRequested)
                    {
                        throw;
                    }

                    _logger.LogError(ex, "Failed to send self-test status snapshot to connection {ConnectionId}", Context.ConnectionId);
                }
            }

            await base.OnConnectedAsync();
        }

        public override Task OnDisconnectedAsync(Exception? exception)
        {
            Guid? deviceId = GetPairedDeviceId();
            if (deviceId is not null)
            {
                _connectionTracker.Unregister(deviceId.Value, Context.ConnectionId);
            }

            return base.OnDisconnectedAsync(exception);
        }

        /// <summary>
        /// Joins the caller to the live-view group for <paramref name="sessionId"/>, so it receives that
        /// session's state changes. Only a session that exists can be subscribed to.
        /// </summary>
        /// <param name="sessionId">The live-view session to follow.</param>
        /// <exception cref="HubException">Thrown when no session with that id exists. Sent to the client as a hub error.</exception>
        public async Task SubscribeLiveView(Guid sessionId)
        {
            using (IServiceScope scope = _scopeFactory.CreateScope())
            {
                ILiveViewSessionRepository sessionRepository = scope.ServiceProvider.GetRequiredService<ILiveViewSessionRepository>();
                LiveViewSession? session = await sessionRepository.GetByIdAsync(sessionId, Context.ConnectionAborted);
                if (session is null)
                {
                    throw new HubException("Live-view session not found.");
                }
            }

            await Groups.AddToGroupAsync(Context.ConnectionId, LiveHubMethods.LiveViewGroup(sessionId), Context.ConnectionAborted);
            _logger.LogInformation("Connection {ConnectionId} subscribed to live-view session {SessionId}", Context.ConnectionId, sessionId);
        }

        /// <summary>
        /// Removes the caller from the live-view group for <paramref name="sessionId"/>. Idempotent: removing a
        /// connection that is not a member is a no-op.
        /// </summary>
        /// <param name="sessionId">The live-view session to stop following.</param>
        public Task UnsubscribeLiveView(Guid sessionId)
        {
            return Groups.RemoveFromGroupAsync(Context.ConnectionId, LiveHubMethods.LiveViewGroup(sessionId), Context.ConnectionAborted);
        }

        private Guid? GetPairedDeviceId()
        {
            string? claim = Context.User?.FindFirst(VideoForensicsClaimTypes.PairedDeviceId)?.Value;
            return Guid.TryParse(claim, out Guid id) ? id : null;
        }
    }
}
