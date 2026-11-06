using Microsoft.Extensions.DependencyInjection;

namespace VideoForensics.Client.Hosting.Maintenance
{
    /// <summary>Helpers that let background work take a lease from the maintenance gate.</summary>
    public static class MaintenanceWork
    {
        private static readonly TimeSpan RetryInterval = TimeSpan.FromSeconds(5);

        /// <summary>The gate registered in <paramref name="services"/>, or a gate that never blocks when none is registered.</summary>
        public static IMaintenanceGate GateFrom(IServiceProvider services) =>
            services.GetService<IMaintenanceGate>() ?? new NoOpMaintenanceGate();

        /// <summary>A lease for one unit of work, or null while maintenance blocks it. Used by loops that retry on their next tick.</summary>
        public static IDisposable? TryEnter(this IServiceProvider services) => GateFrom(services).TryEnterWork();

        /// <summary>Waits until new work is allowed, then returns its lease. Used for work that must run once and must not be dropped.</summary>
        public static async Task<IDisposable> EnterWorkWhenOpenAsync(IMaintenanceGate gate, CancellationToken ct)
        {
            while (true)
            {
                IDisposable? lease = gate.TryEnterWork();
                if (lease is not null)
                {
                    return lease;
                }

                await Task.Delay(RetryInterval, ct);
            }
        }
    }
}
