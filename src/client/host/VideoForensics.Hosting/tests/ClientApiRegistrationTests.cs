using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Client.Hosting.Remote;
using VideoForensics.Client.Ui.Shared.Contracts;
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

        [Fact]
        public void AddVideoForensicsClientApi_ResolvesIMediaFlagRepository_AsRemote()
        {
            var services = new ServiceCollection();
            _ = services.AddSingleton(new PairedSessionState(new Mock<IJSRuntime>().Object));
            _ = services.AddVideoForensicsClientApi(new Uri("http://localhost:5000"));

            using ServiceProvider provider = services.BuildServiceProvider();
            VideoForensics.Core.Api.Contracts.Repositories.IMediaFlagRepository repository = provider.GetRequiredService<VideoForensics.Core.Api.Contracts.Repositories.IMediaFlagRepository>();

            _ = Assert.IsType<RemoteMediaFlagRepository>(repository);
        }

        [Fact]
        public async Task AddVideoForensicsClientApi_ResolvesIMediaFlagChangedSource_AsRemote()
        {
            var services = new ServiceCollection();
            _ = services.AddSingleton(new PairedSessionState(new Mock<IJSRuntime>().Object));
            _ = services.AddVideoForensicsClientApi(new Uri("http://localhost:5000"));

            // The source pulls in the realtime hub, which is async-disposable only.
            await using ServiceProvider provider = services.BuildServiceProvider();
            IMediaFlagChangedSource source = provider.GetRequiredService<IMediaFlagChangedSource>();

            _ = Assert.IsType<RemoteMediaFlagChangedSource>(source);
        }
    }
}
