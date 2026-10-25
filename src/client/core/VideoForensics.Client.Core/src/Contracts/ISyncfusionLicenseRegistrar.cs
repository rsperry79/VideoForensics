namespace VideoForensics.Client.Core.Contracts
{
    /// <summary>
    /// Registers the Syncfusion Blazor license key with the process-wide Syncfusion license provider.
    /// The single home for license registration; every host (MAUI, WebApp, test projects) calls through this.
    /// </summary>
    public interface ISyncfusionLicenseRegistrar
    {
        /// <summary>
        /// Registers the license from the first source that has a key: the key file in the data root, then Client.Core
        /// user secrets, then the key baked into the Client.Core assembly at CI build time. Registers nothing when no
        /// source has a key (unlicensed dev mode).
        /// </summary>
        void RegisterIfPresent();
    }
}
