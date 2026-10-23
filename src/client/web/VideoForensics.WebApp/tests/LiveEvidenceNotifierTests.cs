using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging.Abstractions;

using Moq;

using VideoForensics.Client.WebApp.Hubs;
using VideoForensics.Core.Api.Contracts;

using Xunit;

namespace VideoForensics.Client.WebApp.Tests
{
    public class LiveEvidenceNotifierTests
    {
        private readonly Mock<IHubContext<LiveHub>> _hubContext = new();
        private readonly Mock<IHubClients> _clients = new();
        private readonly Mock<IClientProxy> _all = new();

        public LiveEvidenceNotifierTests()
        {
            _ = _hubContext.Setup(h => h.Clients).Returns(_clients.Object);
            _ = _clients.Setup(c => c.All).Returns(_all.Object);
        }

        /// <summary>Every live-hub connection is told the device's evidence changed, so open Evidence pages reload.</summary>
        [Fact]
        public async Task EvidenceChangedAsync_BroadcastsToAllHubConnections()
        {
            var deviceId = Guid.NewGuid();

            await new LiveEvidenceNotifier(_hubContext.Object, new EvidenceChangedBus(), NullLogger<LiveEvidenceNotifier>.Instance)
                .EvidenceChangedAsync(deviceId, CancellationToken.None);

            _all.Verify(p => p.SendCoreAsync(
                LiveHubMethods.EvidenceChanged,
                It.Is<object?[]>(args => args.Length == 1 && ((EvidenceChangedDto)args[0]!).DeviceId == deviceId),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        /// <summary>In-process views (the WebApp's own Blazor circuits) hear the change without a network round trip.</summary>
        [Fact]
        public async Task EvidenceChangedAsync_PublishesToInProcessBus()
        {
            var bus = new EvidenceChangedBus();
            var deviceId = Guid.NewGuid();
            var seen = new List<EvidenceChangedDto>();
            using IDisposable subscription = bus.Events.Subscribe(seen.Add);

            await new LiveEvidenceNotifier(_hubContext.Object, bus, NullLogger<LiveEvidenceNotifier>.Instance)
                .EvidenceChangedAsync(deviceId, CancellationToken.None);

            Assert.Equal(new[] { new EvidenceChangedDto(deviceId) }, seen);
        }

        /// <summary>A failed push must not fail the write that caused it, so the notifier logs and returns.</summary>
        [Fact]
        public async Task EvidenceChangedAsync_BroadcastFails_DoesNotThrow()
        {
            _ = _all.Setup(p => p.SendCoreAsync(It.IsAny<string>(), It.IsAny<object?[]>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("hub down"));

            await new LiveEvidenceNotifier(_hubContext.Object, new EvidenceChangedBus(), NullLogger<LiveEvidenceNotifier>.Instance)
                .EvidenceChangedAsync(Guid.NewGuid(), CancellationToken.None);
        }
    }
}
