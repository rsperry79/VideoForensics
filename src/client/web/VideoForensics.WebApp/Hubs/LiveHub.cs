using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
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

        public LiveHub(ILiveConnectionTracker connectionTracker)
        {
            _connectionTracker = connectionTracker;
        }

        public override Task OnConnectedAsync()
        {
            var deviceId = GetPairedDeviceId();
            if (deviceId is not null)
            {
                _connectionTracker.Register(deviceId.Value, Context);
            }

            return base.OnConnectedAsync();
        }

        public override Task OnDisconnectedAsync(Exception? exception)
        {
            var deviceId = GetPairedDeviceId();
            if (deviceId is not null)
            {
                _connectionTracker.Unregister(deviceId.Value, Context.ConnectionId);
            }

            return base.OnDisconnectedAsync(exception);
        }

        private Guid? GetPairedDeviceId()
        {
            var claim = Context.User?.FindFirst(VideoForensicsClaimTypes.PairedDeviceId)?.Value;
            return Guid.TryParse(claim, out var id) ? id : null;
        }
    }
}
