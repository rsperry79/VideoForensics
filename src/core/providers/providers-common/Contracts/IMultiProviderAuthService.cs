namespace VideoForensics.Providers.Common.Contracts
{
    /// <summary>
    /// Resolves an <see cref="IProviderAuthService"/> for a caller-chosen provider by name, for
    /// flows that must authenticate against any registered provider (e.g. "Add Account") rather
    /// than only the server's single configured "active" provider (see AddVideoForensicsServerCore).
    /// </summary>
    public interface IMultiProviderAuthService
    {
        /// <summary>Provider names this instance can authenticate against (e.g. "Ring", "Wyze", "Uniview").</summary>
        Task<IReadOnlyList<string>> GetAvailableProvidersAsync(CancellationToken cancellationToken = default);

        /// <summary>Gets the auth service for the named provider. Throws <see cref="ArgumentException"/> if the name isn't a known provider.</summary>
        IProviderAuthService GetService(string providerName);
    }
}
