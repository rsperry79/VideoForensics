using VideoForensics.Core.Api.Contracts;

namespace VideoForensics.Client.Hosting.Maintenance
{
    /// <summary>
    /// Holds back new work while the server is under maintenance or about to restart for an update. Background loops
    /// and user-started work take a lease before each unit of work; while maintenance is active no lease is granted,
    /// so the work is skipped and picked up again on its next tick once maintenance ends.
    /// </summary>
    public interface IMaintenanceGate : IObservable<MaintenanceStatusDto>
    {
        /// <summary>The state now. Clients receive the same value on connect.</summary>
        MaintenanceStatusDto Current { get; }

        /// <summary>
        /// A lease that lets one unit of work run, or null while maintenance blocks new work. Dispose the lease when the
        /// unit finishes; the drain waits for every lease to be disposed.
        /// </summary>
        IDisposable? TryEnterWork();

        /// <summary>
        /// Blocks new work, then completes when every in-flight lease has been disposed. Throws when maintenance is already
        /// in progress, so two maintenance runs never overlap.
        /// </summary>
        Task BeginDrainAsync(string message, CancellationToken ct);

        /// <summary>Marks the drained server as running maintenance. New work stays blocked.</summary>
        void MarkRunning(string message);

        /// <summary>Ends maintenance. Paused work resumes on its next tick.</summary>
        void End();
    }

    /// <summary>Gate that never blocks. Used where no maintenance is wired, such as unit tests of the loops.</summary>
    public sealed class NoOpMaintenanceGate : IMaintenanceGate
    {
        private static readonly IDisposable Lease = new EmptyLease();

        /// <inheritdoc />
        public MaintenanceStatusDto Current => MaintenanceStatusDto.Normal;

        /// <inheritdoc />
        public IDisposable? TryEnterWork() => Lease;

        /// <inheritdoc />
        public Task BeginDrainAsync(string message, CancellationToken ct) => Task.CompletedTask;

        /// <inheritdoc />
        public void MarkRunning(string message)
        {
        }

        /// <inheritdoc />
        public void End()
        {
        }

        /// <inheritdoc />
        public IDisposable Subscribe(IObserver<MaintenanceStatusDto> observer)
        {
            observer.OnNext(Current);
            return EmptyLease.Instance;
        }

        private sealed class EmptyLease : IDisposable
        {
            public static readonly EmptyLease Instance = new();

            public void Dispose()
            {
            }
        }
    }
}
