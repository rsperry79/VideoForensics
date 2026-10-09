namespace VideoForensics.Hosting.Contracts
{
    /// <summary>
    /// Lifecycle state of the client's real-time hub connection, published by <see cref="IRealtimeHub.Connection"/>.
    /// </summary>
    public enum ConnectionState
    {
        /// <summary>Not connected and not trying to connect (before the first start, or after stop/dispose).</summary>
        Disconnected,

        /// <summary>The first connection attempt is in progress.</summary>
        Connecting,

        /// <summary>The hub connection is open and receiving pushes.</summary>
        Connected,

        /// <summary>The connection dropped and the client is retrying with backoff.</summary>
        Reconnecting,

        /// <summary>The server rejected the paired-device token (HTTP 401). Retrying stops until the user re-pairs.</summary>
        AuthFailed
    }
}
