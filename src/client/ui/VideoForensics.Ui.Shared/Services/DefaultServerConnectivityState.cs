namespace VideoForensics.Ui.Shared.Services
{
    /// <summary>
    /// Default implementation of server connectivity state for hosts where connectivity is always OK
    /// (e.g., WebApp where the current process IS the server) or not applicable.
    /// Defaults to "ok" and allows setting to track state.
    /// </summary>
    public class DefaultServerConnectivityState : IServerConnectivityState
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
