using Microsoft.AspNetCore.SignalR;

using Moq;

using VideoForensics.Api.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.WebApp.Hubs;

using Xunit;

namespace VideoForensics.WebApp.Tests
{
    public class LiveViewTelemetryBroadcastServiceTests
    {
        private static (LiveViewTelemetryBroadcastService Service, Mock<IHubClients> Clients, Mock<IClientProxy> Group) Build()
        {
            var group = new Mock<IClientProxy>();
            var clients = new Mock<IHubClients>();
            clients.Setup(c => c.Group(It.IsAny<string>())).Returns(group.Object);

            var hubContext = new Mock<IHubContext<LiveHub>>();
            hubContext.Setup(h => h.Clients).Returns(clients.Object);

            return (new LiveViewTelemetryBroadcastService(hubContext.Object), clients, group);
        }

        [Fact]
        public async Task PublishSessionChanged_SendsToSessionGroupOnly()
        {
            (LiveViewTelemetryBroadcastService service, Mock<IHubClients> clients, _) = Build();
            var session = new LiveViewSession { Id = Guid.NewGuid(), DeviceId = Guid.NewGuid() };

            await service.PublishSessionChangedAsync(session, CancellationToken.None);

            clients.Verify(c => c.Group(LiveHubMethods.LiveViewGroup(session.Id)), Times.Once);
            clients.Verify(c => c.All, Times.Never);
        }

        [Fact]
        public async Task PublishSessionChanged_SendsSessionDto()
        {
            (LiveViewTelemetryBroadcastService service, _, Mock<IClientProxy> group) = Build();
            var session = new LiveViewSession { Id = Guid.NewGuid(), DeviceId = Guid.NewGuid() };
            LiveViewSessionDto? sent = null;
            group.Setup(p => p.SendCoreAsync(LiveHubMethods.LiveViewSessionChanged, It.IsAny<object?[]>(), It.IsAny<CancellationToken>()))
                .Callback<string, object?[], CancellationToken>((_, args, _) => sent = args[0] as LiveViewSessionDto)
                .Returns(Task.CompletedTask);

            await service.PublishSessionChangedAsync(session, CancellationToken.None);

            Assert.NotNull(sent);
            Assert.Equal(session.ToDto(), sent);
            Assert.Equal(session.Id, sent!.Id);
        }

        [Fact]
        public async Task PublishSample_SendsToSessionGroupOnly()
        {
            (LiveViewTelemetryBroadcastService service, Mock<IHubClients> clients, _) = Build();
            var sample = new LiveViewTelemetrySample { Id = Guid.NewGuid(), SessionId = Guid.NewGuid(), CapturedAtUtc = DateTime.UtcNow };

            await service.PublishSampleAsync(sample, CancellationToken.None);

            clients.Verify(c => c.Group(LiveHubMethods.LiveViewGroup(sample.SessionId)), Times.Once);
            clients.Verify(c => c.All, Times.Never);
        }

        [Fact]
        public async Task PublishSample_PayloadIsDtoNotEntity()
        {
            (LiveViewTelemetryBroadcastService service, _, Mock<IClientProxy> group) = Build();
            var sample = new LiveViewTelemetrySample { Id = Guid.NewGuid(), SessionId = Guid.NewGuid(), CapturedAtUtc = DateTime.UtcNow, FractionLost = 12 };
            object? sentPayload = null;
            string? sentMethod = null;
            group.Setup(p => p.SendCoreAsync(It.IsAny<string>(), It.IsAny<object?[]>(), It.IsAny<CancellationToken>()))
                .Callback<string, object?[], CancellationToken>((method, args, _) =>
                {
                    sentMethod = method;
                    sentPayload = args[0];
                })
                .Returns(Task.CompletedTask);

            await service.PublishSampleAsync(sample, CancellationToken.None);

            Assert.Equal(LiveHubMethods.LiveViewTelemetry, sentMethod);
            LiveViewTelemetrySampleDto dto = Assert.IsType<LiveViewTelemetrySampleDto>(sentPayload);
            Assert.Equal(sample.Id, dto.Id);
            Assert.Equal(sample.SessionId, dto.SessionId);
            Assert.Equal((byte?)12, dto.FractionLost);
        }
    }
}
