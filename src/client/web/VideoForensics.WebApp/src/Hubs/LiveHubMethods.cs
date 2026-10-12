namespace VideoForensics.WebApp.Hubs
{
    /// <summary>
    /// Wire method names pushed from <see cref="LiveHub"/> to connected clients. Each name is defined
    /// once here so every send site uses the same string.
    /// </summary>
    public static class LiveHubMethods
    {
        /// <summary>Download progress push. Sent to every connection on connect and broadcast on change.</summary>
        public const string DownloadProgress = "DownloadProgress";

        /// <summary>Self-test status push. Sent only to admin callers (the "admins" group and admin snapshot-on-connect).</summary>
        public const string SelfTestStatus = "SelfTestStatus";

        /// <summary>Live-view session state push. Sent only to connections subscribed to that session's group.</summary>
        public const string LiveViewSessionChanged = "LiveViewSessionChanged";

        /// <summary>Live-view telemetry sample push. Sent only to connections subscribed to that session's group.</summary>
        public const string LiveViewTelemetry = "LiveViewTelemetry";

        /// <summary>
        /// SignalR group name for live-view subscribers of one session. Only connections that called
        /// <c>SubscribeLiveView</c> for this session are members, so session pushes never go to All.
        /// </summary>
        /// <param name="sessionId">The live-view session the group represents.</param>
        public static string LiveViewGroup(Guid sessionId) => $"live-view:{sessionId:D}";
    }
}
