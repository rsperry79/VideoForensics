using Microsoft.AspNetCore.SignalR;

using Moq;

using VideoForensics.Core.Api.Contracts;
using VideoForensics.WebApp.Hubs;

using Xunit;

namespace VideoForensics.WebApp.Tests
{
    public class LiveThumbnailNotifierTests
    {
        private readonly Mock<IHubContext<LiveHub>> _hubContext = new();
        private readonly Mock<IHubClients> _clients = new();
        private readonly Mock<IClientProxy> _all = new();

        public LiveThumbnailNotifierTests()
        {
            _ = _hubContext.Setup(h => h.Clients).Returns(_clients.Object);
            _ = _clients.Setup(c => c.All).Returns(_all.Object);
        }

        /// <summary>Every live-hub connection is told, since the hub already requires paired-device auth for all of them.</summary>
        [Fact]
        public async Task ThumbnailReadyAsync_BroadcastsToAllHubConnections()
        {
            var bus = new ThumbnailReadyBus();
            var id = Guid.NewGuid();

            await new LiveThumbnailNotifier(_hubContext.Object, bus).ThumbnailReadyAsync(id, CancellationToken.None);

            _all.Verify(p => p.SendCoreAsync(
                LiveHubMethods.ThumbnailReady,
                It.Is<object?[]>(args => args.Length == 1 && ((ThumbnailReadyDto)args[0]!).MediaItemId == id),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        /// <summary>In-process views (the WebApp's own Blazor circuits) get the event without a network round trip.</summary>
        [Fact]
        public async Task ThumbnailReadyAsync_PublishesToInProcessBus()
        {
            var bus = new ThumbnailReadyBus();
            var id = Guid.NewGuid();
            var seen = new List<ThumbnailReadyDto>();
            using IDisposable subscription = bus.Events.Subscribe(seen.Add);

            await new LiveThumbnailNotifier(_hubContext.Object, bus).ThumbnailReadyAsync(id, CancellationToken.None);

            Assert.Equal(new[] { new ThumbnailReadyDto(id) }, seen);
        }

        /// <summary>The bus is an event stream: a subscriber that arrives later must not be given the earlier thumbnail.</summary>
        [Fact]
        public void ThumbnailReadyBus_LateSubscriber_DoesNotReceiveEarlierEvent()
        {
            var bus = new ThumbnailReadyBus();
            bus.Publish(new ThumbnailReadyDto(Guid.NewGuid()));

            var seen = new List<ThumbnailReadyDto>();
            using IDisposable subscription = bus.Events.Subscribe(seen.Add);

            Assert.Empty(seen);
        }
    }
}
