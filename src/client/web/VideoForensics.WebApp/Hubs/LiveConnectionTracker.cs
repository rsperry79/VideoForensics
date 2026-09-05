using Microsoft.AspNetCore.SignalR;

using System.Collections.Concurrent;

namespace VideoForensics.WebApp.Hubs
{
    /// <summary>
    /// Tracks which live <see cref="LiveHub"/> connections belong to which paired device, so
    /// revocation (plan §5.4) can forcibly terminate an already-open connection - not just block
    /// the device's next HTTP request, which is all token invalidation alone accomplishes. Stores
    /// the actual <see cref="HubCallerContext"/> (not just a connection-id string) specifically
    /// because <c>HubCallerContext.Abort()</c> is the one API that genuinely tears the connection
    /// down server-side; sending a "please disconnect" message over <c>IHubContext</c> only works
    /// if the client cooperates, which a compromised or malicious client has no reason to do.
    /// </summary>
    public interface ILiveConnectionTracker
    {
        void Register(Guid pairedDeviceId, HubCallerContext context);
        void Unregister(Guid pairedDeviceId, string connectionId);

        /// <summary>Forcibly aborts every currently-open connection for this device (there is normally at most one, but nothing prevents more).</summary>
        void ForceDisconnect(Guid pairedDeviceId);
    }

    public class LiveConnectionTracker : ILiveConnectionTracker
    {
        private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<string, HubCallerContext>> _connectionsByDevice = new();

        public void Register(Guid pairedDeviceId, HubCallerContext context)
        {
            ConcurrentDictionary<string, HubCallerContext> connections = _connectionsByDevice.GetOrAdd(pairedDeviceId, _ => new ConcurrentDictionary<string, HubCallerContext>());
            connections[context.ConnectionId] = context;
        }

        public void Unregister(Guid pairedDeviceId, string connectionId)
        {
            if (_connectionsByDevice.TryGetValue(pairedDeviceId, out ConcurrentDictionary<string, HubCallerContext>? connections))
            {
                _ = connections.TryRemove(connectionId, out _);
            }
        }

        public void ForceDisconnect(Guid pairedDeviceId)
        {
            if (!_connectionsByDevice.TryRemove(pairedDeviceId, out ConcurrentDictionary<string, HubCallerContext>? connections))
            {
                return;
            }

            foreach (HubCallerContext context in connections.Values)
            {
                context.Abort();
            }
        }
    }
}
