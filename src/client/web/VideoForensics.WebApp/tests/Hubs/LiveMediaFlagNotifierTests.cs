using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging.Abstractions;

using Moq;

using VideoForensics.Client.WebApp.Hubs;
using VideoForensics.Core.Api.Contracts;

using Xunit;

namespace VideoForensics.Client.WebApp.Tests.Hubs
{
    public class LiveMediaFlagNotifierTests
    {
        private readonly Mock<IHubContext<LiveHub>> _hubContext = new();
        private readonly Mock<IHubClients> _clients = new();
        private readonly Mock<IClientProxy> _all = new();

        public LiveMediaFlagNotifierTests()
        {
            _ = _hubContext.Setup(h => h.Clients).Returns(_clients.Object);
            _ = _clients.Setup(c => c.All).Returns(_all.Object);
        }

        /// <summary>Every live-hub connection is told the flag changed, so open review pages reload.</summary>
        [Fact]
        public async Task MediaFlagChangedAsync_BroadcastsToAllHubConnections()
        {
            var flagId = Guid.NewGuid();
            var deviceId = Guid.NewGuid();

            await new LiveMediaFlagNotifier(_hubContext.Object, new MediaFlagChangedBus(), NullLogger<LiveMediaFlagNotifier>.Instance)
                .MediaFlagChangedAsync(flagId, deviceId, CancellationToken.None);

            _all.Verify(p => p.SendCoreAsync(
                LiveHubMethods.MediaFlagChanged,
                It.Is<object?[]>(args => args.Length == 1 && ((MediaFlagChangedDto)args[0]!).FlagId == flagId && ((MediaFlagChangedDto)args[0]!).DeviceId == deviceId),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        /// <summary>In-process views hear the change without a network round trip.</summary>
        [Fact]
        public async Task MediaFlagChangedAsync_PublishesToInProcessBus()
        {
            var bus = new MediaFlagChangedBus();
            var flagId = Guid.NewGuid();
            var deviceId = Guid.NewGuid();
            var seen = new List<MediaFlagChangedDto>();
            using IDisposable subscription = bus.Events.Subscribe(seen.Add);

            await new LiveMediaFlagNotifier(_hubContext.Object, bus, NullLogger<LiveMediaFlagNotifier>.Instance)
                .MediaFlagChangedAsync(flagId, deviceId, CancellationToken.None);

            Assert.Equal(new[] { new MediaFlagChangedDto(flagId, deviceId) }, seen);
        }

        /// <summary>A failed push must not fail the flag write that caused it, so the notifier logs and returns.</summary>
        [Fact]
        public async Task MediaFlagChangedAsync_BroadcastFails_DoesNotThrow()
        {
            _ = _all.Setup(p => p.SendCoreAsync(It.IsAny<string>(), It.IsAny<object?[]>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("hub down"));

            await new LiveMediaFlagNotifier(_hubContext.Object, new MediaFlagChangedBus(), NullLogger<LiveMediaFlagNotifier>.Instance)
                .MediaFlagChangedAsync(Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);
        }
    }
}
