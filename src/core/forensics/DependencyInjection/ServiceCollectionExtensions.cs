using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using VideoForensics.Providers.Ring.Forensics.Implementations;

namespace VideoForensics.Providers.Ring.Forensics.DependencyInjection
{
    /// <summary>Extension methods for registering VideoForensics signal-anomaly/jamming detection services.</summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Adds <see cref="ISignalAnomalyDetector"/> to the dependency injection container. The concrete
        /// implementation type is internal to this assembly, so callers must register it through this
        /// extension rather than constructing it directly.
        /// </summary>
        public static IServiceCollection AddVideoForensicsSignalAnomalyDetection(this IServiceCollection services)
        {
            services.TryAddSingleton<ISignalAnomalyDetector, SignalAnomalyDetector>();
            return services;
        }
    }
}
