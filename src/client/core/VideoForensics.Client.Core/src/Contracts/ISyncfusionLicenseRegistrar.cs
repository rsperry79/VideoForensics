using Microsoft.Extensions.Configuration;

namespace VideoForensics.Client.Core.Contracts
{
    /// <summary>
    /// Registers the Syncfusion Blazor license key with the process-wide Syncfusion license provider.
    /// The single home for license registration; every host (MAUI, WebApp, test projects) calls through this.
    /// </summary>
    public interface ISyncfusionLicenseRegistrar
    {
        /// <summary>
        /// Registers the license using only the non-configuration sources: the %ProgramData% key file, then the
        /// SYNCFUSION_LICENSE_KEY environment variable, then the key baked into the Client.Core assembly at CI build time.
        /// Registers nothing when no source has a key (unlicensed dev mode).
        /// </summary>
        void RegisterIfPresent();

        /// <summary>
        /// Registers the license using the same priority order as <see cref="RegisterIfPresent()"/>, but reads the
        /// SYNCFUSION_LICENSE_KEY value from <paramref name="configuration"/> so user secrets and Azure App Settings
        /// are honored once the host's configuration is built.
        /// </summary>
        /// <param name="configuration">Configuration to read SYNCFUSION_LICENSE_KEY from.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="configuration"/> is null.</exception>
        void RegisterIfPresent(IConfiguration configuration);
    }
}
