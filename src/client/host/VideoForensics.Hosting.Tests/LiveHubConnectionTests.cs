using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

using Moq;

using VideoForensics.Ui.Shared.Services;

namespace VideoForensics.Hosting.Tests
{
    public class LiveHubConnectionTests
    {
        [Fact]
        public async Task LiveHubConnection_FirstStart_AttemptsConnection()
        {
            var services = new ServiceCollection();
            services.AddSingleton(new PairedSessionState(new Mock<IJSRuntime>().Object));
            using var provider = services.BuildServiceProvider();

            await using var connection = new LiveHubConnection(new Uri("http://127.0.0.1:1"), provider);
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

            // An unreachable server means a real connection attempt must throw; a silent return means it never tried.
            await Assert.ThrowsAnyAsync<Exception>(() => connection.StartAsync(cts.Token));
        }
    }
}
