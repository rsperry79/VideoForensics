using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

using Moq;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Hosting.Remote;
using VideoForensics.Ui.Shared.Services;

namespace VideoForensics.Hosting.Tests
{
    public class ClientApiRegistrationTests
    {
        [Fact]
        public void AddVideoForensicsClientApi_ResolvesILiveViewSessionService_AsRemote()
        {
            var services = new ServiceCollection();
            services.AddSingleton(new PairedSessionState(new Mock<IJSRuntime>().Object));
            _ = services.AddVideoForensicsClientApi(new Uri("http://localhost:5000"));

            using ServiceProvider provider = services.BuildServiceProvider();
            ILiveViewSessionService service = provider.GetRequiredService<ILiveViewSessionService>();

            Assert.IsType<RemoteLiveViewSessionService>(service);
        }
    }
}
