namespace VideoForensics.Client.Common.Contracts
{
    /// <summary>
    /// Announces that the saved forensics configuration changed, so live clients can refresh their settings without polling.
    /// The data layer calls it after a successful save. Implementations must not throw for a failed push: the save has
    /// already been written, and a missed announcement only means clients read the new values on their next load.
    /// </summary>
    public interface IConfigurationChangeNotifier
    {
        /// <summary>Announces the configuration that was just saved.</summary>
        Task ConfigurationSavedAsync(IForensicsConfiguration config, CancellationToken cancellationToken);
    }
}
