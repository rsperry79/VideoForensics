using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Client.Hosting.Remote;
using VideoForensics.Client.Ui.Shared.Services;

namespace VideoForensics.Client.Hosting.Tests
{
    public class ClientApiRegistrationTests
    {
        [Fact]
        public void AddVideoForensicsClientApi_ResolvesILiveViewSessionService_AsRemote()
        {
            var services = new ServiceCollection();
            _ = services.AddSingleton(new PairedSessionState(new Mock<IJSRuntime>().Object));
            _ = services.AddVideoForensicsClientApi(new Uri("http://localhost:5000"));

            using ServiceProvider provider = services.BuildServiceProvider();
            ILiveViewSessionService service = provider.GetRequiredService<ILiveViewSessionService>();

            _ = Assert.IsType<RemoteLiveViewSessionService>(service);
        }
    }
}
