using VideoForensics.Client.Hosting.Maintenance;
using VideoForensics.Core.Api.Contracts;

using Xunit;

namespace VideoForensics.Client.Hosting.Tests.Maintenance
{
    public class MaintenanceGateTests
    {
        [Fact]
        public void TryEnterWork_Idle_GrantsLease()
        {
            var gate = new MaintenanceGate();

            using IDisposable? lease = gate.TryEnterWork();

            Assert.NotNull(lease);
        }

        [Fact]
        public async Task BeginDrainAsync_NoInFlightWork_BlocksNewWorkAndCompletes()
        {
            var gate = new MaintenanceGate();

            await gate.BeginDrainAsync("test", CancellationToken.None);

            Assert.Equal(MaintenancePhases.Draining, gate.Current.Phase);
            Assert.Null(gate.TryEnterWork());
        }

        [Fact]
        public async Task BeginDrainAsync_WorkInFlight_WaitsUntilLeaseDisposed()
        {
            var gate = new MaintenanceGate();
            IDisposable? lease = gate.TryEnterWork();
            Task drain = gate.BeginDrainAsync("test", CancellationToken.None);

            Assert.False(drain.IsCompleted);
            lease!.Dispose();
            await drain.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.True(drain.IsCompletedSuccessfully);
        }

        [Fact]
        public async Task End_AfterMaintenance_RestoresNormalWork()
        {
            var gate = new MaintenanceGate();
            await gate.BeginDrainAsync("test", CancellationToken.None);
            gate.MarkRunning("test");

            gate.End();

            Assert.Equal(MaintenancePhases.Idle, gate.Current.Phase);
            Assert.NotNull(gate.TryEnterWork());
        }

        [Fact]
        public async Task BeginDrainAsync_AlreadyInMaintenance_Throws()
        {
            var gate = new MaintenanceGate();
            await gate.BeginDrainAsync("first", CancellationToken.None);

            _ = await Assert.ThrowsAsync<InvalidOperationException>(() => gate.BeginDrainAsync("second", CancellationToken.None));
        }

        [Fact]
        public async Task Subscribe_DuringMaintenance_ReceivesCurrentState()
        {
            var gate = new MaintenanceGate();
            await gate.BeginDrainAsync("compacting", CancellationToken.None);
            MaintenanceStatusDto? received = null;

            using IDisposable subscription = gate.Subscribe(new Observer(status => received = status));

            Assert.Equal(MaintenancePhases.Draining, received?.Phase);
            Assert.Equal("compacting", received?.Message);
        }

        [Fact]
        public async Task BeginDrainAsync_TwoConcurrentCallsOnFreshGate_ExactlyOneSucceeds()
        {
            // Why: the race window is a few instructions wide, so many fresh gates are needed to hit it reliably.
            for (int i = 0; i < 2000; i++)
            {
                using var gate = new MaintenanceGate();
                using var start = new Barrier(2);

                Task<bool> Attempt() => Task.Run(() =>
                {
                    start.SignalAndWait();
                    try
                    {
                        _ = gate.BeginDrainAsync("race", CancellationToken.None);
                        return true;
                    }
                    catch (InvalidOperationException)
                    {
                        return false;
                    }
                });

                bool[] results = await Task.WhenAll(Attempt(), Attempt());

                Assert.Equal(1, results.Count(succeeded => succeeded));
                Assert.Equal(MaintenancePhases.Draining, gate.Current.Phase);
            }
        }

        private sealed class Observer(Action<MaintenanceStatusDto> onNext) : IObserver<MaintenanceStatusDto>
        {
            public void OnNext(MaintenanceStatusDto value) => onNext(value);

            public void OnError(Exception error)
            {
            }

            public void OnCompleted()
            {
            }
        }
    }
}
