namespace VideoForensics.Ui.Shared.Services
{
    /// <summary>
    /// Tracks the current server connectivity state - whether the app can reach the server
    /// or if server resolution has failed (no local server found and no cached URL available).
    /// </summary>
    public interface IServerConnectivityState
    {
        /// <summary>
        /// Gets the current connectivity state: "ok" if connected, "unreachable" if server resolution failed.
        /// </summary>
        string State { get; }

        /// <summary>
        /// Gets whether the app is in a failed connectivity state (server not reachable).
        /// </summary>
        bool IsUnreachable => State == "unreachable";

        /// <summary>
        /// Sets the connectivity state to unreachable (called when ServerNotReachableException occurs).
        /// </summary>
        void MarkAsUnreachable();

        /// <summary>
        /// Sets the connectivity state back to ok (called when connection is reestablished or pairing succeeds).
        /// </summary>
        void MarkAsConnected();
    }
}
