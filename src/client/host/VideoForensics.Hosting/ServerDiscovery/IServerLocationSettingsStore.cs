namespace VideoForensics.Hosting.ServerDiscovery
{
    /// <summary>
    /// Manages persistent storage of server location settings, including cached Internet server URLs
    /// for fallback when local discovery fails. Implementations are host-specific (e.g., MAUI uses
    /// Preferences API, while other hosts might use config files).
    /// </summary>
    public interface IServerLocationSettingsStore
    {
        /// <summary>
        /// Gets the cached Internet server URL, used as a fallback when local mDNS discovery times out
        /// or fails. Returns null if no cached URL is available.
        /// </summary>
        string? GetCachedInternetServerUrl();

        /// <summary>
        /// Sets the cached Internet server URL to be used as a fallback in future discovery attempts.
        /// </summary>
        void SetCachedInternetServerUrl(string url);
    }
}
