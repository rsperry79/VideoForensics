using VideoForensics.Providers.Common.Contracts;

namespace VideoForensics.Hosting.Services
{
    /// <summary>
    /// Server-side <see cref="IMultiProviderAuthService"/> that constructs an <see cref="IProviderAuthService"/>
    /// on demand for any registered provider, independent of the single "active" provider configured for
    /// AddVideoForensicsServerCore. Used by the "Add Account" flow, which needs to authenticate against a
    /// provider the caller explicitly chose rather than whichever one the server booted with.
    /// </summary>
    public class MultiProviderAuthService : IMultiProviderAuthService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IReadOnlyDictionary<string, Func<IServiceProvider, IProviderAuthService>> _factories;

        public MultiProviderAuthService(
            IServiceProvider serviceProvider,
            IReadOnlyDictionary<string, Func<IServiceProvider, IProviderAuthService>> factories)
        {
            _serviceProvider = serviceProvider;
            _factories = factories;
        }

        public Task<IReadOnlyList<string>> GetAvailableProvidersAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<string>>(_factories.Keys.ToList());
        }

        public IProviderAuthService GetService(string providerName)
        {
            return !_factories.TryGetValue(providerName, out Func<IServiceProvider, IProviderAuthService>? factory)
                ? throw new ArgumentException($"Unknown provider '{providerName}'. Available: {string.Join(", ", _factories.Keys)}", nameof(providerName))
                : factory(_serviceProvider);
        }
    }
}
