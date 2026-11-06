using VideoForensics.Client.WebApp.Services;

using Xunit;

namespace VideoForensics.Client.WebApp.Tests.Services
{
    public class StartupInitializationGateTests
    {
        [Fact]
        public void StartupInitializationGate_BeforeMarkReady_IsNotReady()
        {
            var gate = new StartupInitializationGate();

            Assert.False(gate.IsReady);
        }

        [Fact]
        public async Task StartupInitializationGate_MarkReady_CompletesWhenReadyAsync()
        {
            var gate = new StartupInitializationGate();
            Task waiting = gate.WhenReadyAsync(CancellationToken.None);

            gate.MarkReady();

            await waiting.WaitAsync(System.TimeSpan.FromSeconds(5));
            Assert.True(gate.IsReady);
        }
    }
}
