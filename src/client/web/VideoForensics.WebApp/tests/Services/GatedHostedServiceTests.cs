using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Hosting;

using VideoForensics.Client.WebApp.Services;

using Xunit;

namespace VideoForensics.Client.WebApp.Tests.Services
{
    public class GatedHostedServiceTests
    {
        private sealed class RecordingHostedService : IHostedService
        {
            private readonly TaskCompletionSource _started = new(TaskCreationOptions.RunContinuationsAsynchronously);

            public int StartCount { get; private set; }

            public Task Started => _started.Task;

            public Task StartAsync(CancellationToken cancellationToken)
            {
                StartCount++;
                _started.TrySetResult();
                return Task.CompletedTask;
            }

            public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        }

        [Fact]
        public async Task GatedHostedService_StartAsync_DoesNotStartInnerBeforeGateOpens()
        {
            var gate = new StartupInitializationGate();
            var inner = new RecordingHostedService();
            var gated = new GatedHostedService(inner, gate);

            await gated.StartAsync(CancellationToken.None);
            await Task.Delay(100);

            Assert.Equal(0, inner.StartCount);
            await gated.StopAsync(CancellationToken.None);
        }

        [Fact]
        public async Task GatedHostedService_GateOpens_StartsInner()
        {
            var gate = new StartupInitializationGate();
            var inner = new RecordingHostedService();
            var gated = new GatedHostedService(inner, gate);
            await gated.StartAsync(CancellationToken.None);

            gate.MarkReady();
            await inner.Started.WaitAsync(System.TimeSpan.FromSeconds(5));
            await gated.StopAsync(CancellationToken.None);

            Assert.Equal(1, inner.StartCount);
        }
    }
}
