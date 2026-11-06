using Microsoft.Extensions.DependencyInjection;

using VideoForensics.Client.Ui.Shared.Contracts;
using VideoForensics.Client.WebApp.Hubs;
using VideoForensics.Client.WebApp.Services;
using VideoForensics.Core.Api.Contracts;

using Xunit;

namespace VideoForensics.Client.WebApp.Tests.Hubs
{
    /// <summary>The server's media-flag change pieces resolve, and a notifier publish reaches the source the pages subscribe to.</summary>
    public class MediaFlagChangeRegistrationTests
    {
        private static ServiceProvider Build()
        {
            var services = new ServiceCollection();
            _ = services.AddLogging();
            _ = services.AddSignalR();
            _ = services.AddMediaFlagChangeServices();
            return services.BuildServiceProvider();
        }

        [Fact]
        public void AddMediaFlagChangeServices_ResolvesNotifier_AsLiveNotifier()
        {
            using ServiceProvider provider = Build();

            IMediaFlagChangeNotifier notifier = provider.GetRequiredService<IMediaFlagChangeNotifier>();

            _ = Assert.IsType<LiveMediaFlagNotifier>(notifier);
        }

        [Fact]
        public void AddMediaFlagChangeServices_ResolvesSource_AsLocalSourceInScope()
        {
            using ServiceProvider provider = Build();
            using IServiceScope scope = provider.CreateScope();

            IMediaFlagChangedSource source = scope.ServiceProvider.GetRequiredService<IMediaFlagChangedSource>();

            _ = Assert.IsType<LocalMediaFlagChangedSource>(source);
        }

        [Fact]
        public async Task AddMediaFlagChangeServices_NotifierPublish_ReachesSource()
        {
            using ServiceProvider provider = Build();
            using IServiceScope scope = provider.CreateScope();
            IMediaFlagChangedSource source = scope.ServiceProvider.GetRequiredService<IMediaFlagChangedSource>();
            IMediaFlagChangeNotifier notifier = scope.ServiceProvider.GetRequiredService<IMediaFlagChangeNotifier>();
            var flagId = Guid.NewGuid();
            var deviceId = Guid.NewGuid();
            var seen = new List<MediaFlagChangedDto>();
            using IDisposable subscription = source.MediaFlagChanged.Subscribe(seen.Add);

            await notifier.MediaFlagChangedAsync(flagId, deviceId, CancellationToken.None);

            Assert.Equal(new[] { new MediaFlagChangedDto(flagId, deviceId) }, seen);
        }
    }
}
