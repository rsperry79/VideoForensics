using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

using VideoForensics.Api.Contracts;
using VideoForensics.Client.Common.Contracts;
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
    /// Sends only, deliberately: clients don't call hub methods here (no "StartDownload" RPC on this
    /// hub) - triggering actions is a plain HTTP POST per plan §6, kept separate from this push
    /// channel's own concerns.
    /// </summary>
    [Authorize(AuthenticationSchemes = PairedDeviceAuthenticationDefaults.SchemeName, Policy = VideoForensicsPolicies.ReadOnly)]
    public class LiveHub : Hub
    {
        private readonly ILiveConnectionTracker _connectionTracker;
        private readonly IServiceScopeFactory _scopeFactory;

        public LiveHub(ILiveConnectionTracker connectionTracker, IServiceScopeFactory scopeFactory)
        {
            _connectionTracker = connectionTracker;
            _scopeFactory = scopeFactory;
        }

        public override async Task OnConnectedAsync()
        {
            Guid? deviceId = GetPairedDeviceId();
            if (deviceId is not null)
            {
                _connectionTracker.Register(deviceId.Value, Context);
            }

            // Add admins to the admins group for admin-only notifications
            string? roleClaim = Context.User?.FindFirst(VideoForensicsClaimTypes.Role)?.Value;
            if (Enum.TryParse<OperatorRole>(roleClaim, out OperatorRole role) && role >= OperatorRole.Admin)
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, "admins", CancellationToken.None);
            }

            // Snapshot-on-connect: a new client (including every reconnect) gets the current state
            // immediately instead of waiting for the next change. Uses the non-draining builder so
            // this connection never consumes activity lines that other clients still need.
            DownloadProgressDto snapshot;
            using (IServiceScope scope = _scopeFactory.CreateScope())
            {
                IVideoDownloadService downloadService = scope.ServiceProvider.GetRequiredService<IVideoDownloadService>();
                snapshot = downloadService.ToDownloadProgressSnapshot();
            }

            await Clients.Caller.SendAsync("DownloadProgress", snapshot, Context.ConnectionAborted);

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

        private Guid? GetPairedDeviceId()
        {
            string? claim = Context.User?.FindFirst(VideoForensicsClaimTypes.PairedDeviceId)?.Value;
            return Guid.TryParse(claim, out Guid id) ? id : null;
        }
    }
}
