namespace VideoForensics.Client.Common
{
    /// <summary>Manages persistence and loading of forensics configuration</summary>
    public interface IForensicsConfigurationService
    {
        /// <summary>Loads configuration from disk</summary>
        Task<IForensicsConfiguration> LoadConfigurationAsync(string configPath, CancellationToken cancellationToken = default);

        /// <summary>Saves configuration to disk</summary>
        Task SaveConfigurationAsync(IForensicsConfiguration config, CancellationToken cancellationToken = default);
    }
}
