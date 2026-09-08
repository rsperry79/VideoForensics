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
        private string _state = "ok";

        /// <inheritdoc/>
        public string State => _state;

        /// <inheritdoc/>
        public void MarkAsUnreachable()
        {
            _state = "unreachable";
        }

        /// <inheritdoc/>
        public void MarkAsConnected()
        {
            _state = "ok";
        }
    }
}
