using System.Reactive.Subjects;

using VideoForensics.Core.Api.Contracts;

namespace VideoForensics.Client.Hosting.Maintenance
{
    /// <summary>
    /// The live gate. Counts leases for in-flight work, and when maintenance begins it blocks new leases and waits for the
    /// count to reach zero. State changes are published to subscribers, which the server broadcasts to clients.
    /// </summary>
    public sealed class MaintenanceGate : IMaintenanceGate, IDisposable
    {
        private readonly object _lock = new();
        private readonly BehaviorSubject<MaintenanceStatusDto> _status = new(MaintenanceStatusDto.Normal);
        private int _inFlight;
        private TaskCompletionSource _drained = NewDrainSignal();

        /// <inheritdoc />
        public MaintenanceStatusDto Current => _status.Value;

        /// <inheritdoc />
        public IDisposable? TryEnterWork()
        {
            lock (_lock)
            {
                if (_status.Value.Phase != MaintenancePhases.Idle)
                {
                    return null;
                }

                _inFlight++;
                return new Lease(this);
            }
        }

        /// <inheritdoc />
        public Task BeginDrainAsync(string message, CancellationToken ct)
        {
            MaintenanceStatusDto draining;
            lock (_lock)
            {
                if (_status.Value.Phase != MaintenancePhases.Idle)
                {
                    throw new InvalidOperationException("Server maintenance is already in progress.");
                }

                _drained = NewDrainSignal();
                draining = new MaintenanceStatusDto(MaintenancePhases.Draining, message, DateTime.UtcNow);
                if (_inFlight == 0)
                {
                    _drained.TrySetResult();
                }
            }

            Publish(draining);
            return _drained.Task.WaitAsync(ct);
        }

        /// <inheritdoc />
        public void MarkRunning(string message)
        {
            MaintenanceStatusDto running;
            lock (_lock)
            {
                running = new MaintenanceStatusDto(MaintenancePhases.Running, message, _status.Value.StartedUtc);
            }

            Publish(running);
        }

        /// <inheritdoc />
        public void End()
        {
            Publish(MaintenanceStatusDto.Normal);
        }

        /// <inheritdoc />
        public IDisposable Subscribe(IObserver<MaintenanceStatusDto> observer) => _status.Subscribe(observer);

        /// <inheritdoc />
        public void Dispose() => _status.Dispose();

        private void Release()
        {
            lock (_lock)
            {
                _inFlight--;
                if (_status.Value.Phase == MaintenancePhases.Draining && _inFlight == 0)
                {
                    _drained.TrySetResult();
                }
            }
        }

        // Published outside the lock, so a subscriber that calls back into the gate cannot deadlock.
        private void Publish(MaintenanceStatusDto status) => _status.OnNext(status);

        private static TaskCompletionSource NewDrainSignal() =>
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        private sealed class Lease(MaintenanceGate gate) : IDisposable
        {
            private int _disposed;

            public void Dispose()
            {
                if (Interlocked.Exchange(ref _disposed, 1) == 0)
                {
                    gate.Release();
                }
            }
        }
    }
}
