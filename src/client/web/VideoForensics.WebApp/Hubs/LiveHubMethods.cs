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
    }
}
