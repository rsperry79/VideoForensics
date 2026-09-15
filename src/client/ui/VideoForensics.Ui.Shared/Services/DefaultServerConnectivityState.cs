namespace VideoForensics.Ui.Shared.Services
{
    /// <summary>
    /// Default implementation of server connectivity state for hosts where connectivity is always OK
    /// (e.g., WebApp where the current process IS the server) or not applicable.
    /// Defaults to "ok" and allows setting to track state.
    /// </summary>
    public class DefaultServerConnectivityState : IServerConnectivityState
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
