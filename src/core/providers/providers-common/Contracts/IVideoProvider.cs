namespace VideoForensics.Providers.Common.Contracts
{
    /// <summary>
    /// Platform-agnostic interface for video providers (Ring, Wyze, etc.)
    /// </summary>
    public interface IVideoProvider
    {
        /// <summary>Gets the provider name (e.g., "Ring", "Wyze")</summary>
        string ProviderName { get; }

        /// <summary>Gets the authentication service for this provider</summary>
        IProviderAuthService AuthService { get; }

        /// <summary>Gets the device discovery service for this provider</summary>
        IDeviceDiscoveryService DeviceService { get; }

        /// <summary>Gets the download service for this provider</summary>
        IMediaDownloadService DownloadService { get; }

        /// <summary>Gets the event and configuration service for this provider</summary>
        IEventAndConfigService EventService { get; }
    }
}
