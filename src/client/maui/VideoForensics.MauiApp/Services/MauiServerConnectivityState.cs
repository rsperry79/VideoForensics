using VideoForensics.Ui.Shared.Services;

namespace VideoForensics.MauiApp.Services
{
    /// <summary>
    /// MAUI-specific implementation of server connectivity state.
    /// Tracks whether the server is currently reachable or in a failed discovery state.
    /// Initialized to "ok" and set to "unreachable" when ServerNotReachableException occurs in MauiProgram.
    /// </summary>
    public class MauiServerConnectivityState : IServerConnectivityState
    {
        /// <inheritdoc/>
        public string State { get; private set; } = "ok";

        /// <inheritdoc/>
        public void MarkAsUnreachable()
        {
            State = "unreachable";
        }

        /// <inheritdoc/>
        public void MarkAsConnected()
        {
            State = "ok";
        }
    }
}
