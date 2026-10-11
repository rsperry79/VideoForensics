using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging.Abstractions;

using Moq;

using SIPSorcery.Net;
using SIPSorceryMedia.Abstractions;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Providers.Common.Contracts;
using VideoForensics.WebApp.Hubs;
using VideoForensics.WebApp.Services;

using Xunit;

namespace VideoForensics.WebApp.Tests
{
    /// <summary>
    /// Exercises <see cref="LiveViewSignalingHub"/> signaling methods against a real
    /// <see cref="BrowserLiveViewBridge"/> and real SIPSorcery peers. <see cref="ILiveViewSessionService"/>
    /// and the SignalR caller proxy are mocked.
    /// </summary>
    public class LiveViewSignalingHubTests
    {
        private const string ConnectionId = "conn-signaling";
        private static readonly Guid SessionId = Guid.Parse("33333333-3333-3333-3333-333333333333");

        /// <summary>
        /// Minimal ILiveViewConnection; the hub only needs it as an opaque handle for the bridge.
        /// </summary>
        private sealed class FakeLiveViewConnection : ILiveViewConnection
        {
            public event Action<RTCPReceiverReportSampleDto>? OnReceiverReport;
            public event Action<long>? OnBitrateSampleBps;
            public event Action<LiveViewConnectionStateDto>? OnConnectionStateChange;
            public event Action<byte[], uint, int>? OnVideoRtpPayload;

            public Task CloseAsync(CancellationToken ct) => Task.CompletedTask;

            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }

        private sealed class HubFixture
        {
            public required LiveViewSignalingHub Hub { get; init; }
            public required BrowserLiveViewBridge Bridge { get; init; }
            public required Mock<ILiveViewSessionService> SessionService { get; init; }
        }

        private static HubFixture BuildHub(ILiveViewConnection? connectionForSession)
        {
            var sessionService = new Mock<ILiveViewSessionService>();
            sessionService
                .Setup(s => s.GetConnectionForSessionAsync(SessionId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(connectionForSession);
            sessionService
                .Setup(s => s.GetConnectionForSessionAsync(It.Is<Guid>(id => id != SessionId), It.IsAny<CancellationToken>()))
                .ReturnsAsync((ILiveViewConnection?)null);

            var bridge = new BrowserLiveViewBridge(Microsoft.Extensions.Logging.Abstractions.NullLogger<BrowserLiveViewBridge>.Instance);

            var caller = new Mock<ISingleClientProxy>();
            var clients = new Mock<IHubCallerClients>();
            clients.Setup(c => c.Caller).Returns(caller.Object);

            var context = new Mock<HubCallerContext>();
            context.Setup(c => c.ConnectionId).Returns(ConnectionId);

            var hub = new LiveViewSignalingHub(
                sessionService.Object,
                bridge,
                Microsoft.Extensions.Logging.Abstractions.NullLogger<LiveViewSignalingHub>.Instance)
            {
                Clients = clients.Object,
                Context = context.Object,
            };

            return new HubFixture { Hub = hub, Bridge = bridge, SessionService = sessionService };
        }

        /// <summary>
        /// Builds a genuine SDP answer by letting a second SIPSorcery peer answer the given offer.
        /// </summary>
        private static async Task<string> CreateRealAnswerAsync(string offerSdp)
        {
            using var answerer = new RTCPeerConnection(new RTCConfiguration());
            answerer.addTrack(new MediaStreamTrack(
                [new VideoFormat(VideoCodecsEnum.H264, 96, 90000, "packetization-mode=1")],
                MediaStreamStatusEnum.SendRecv));
            answerer.addTrack(new MediaStreamTrack(
                [new AudioFormat(AudioCodecsEnum.OPUS, 111, 48000, 2, string.Empty)],
                MediaStreamStatusEnum.SendRecv));

            answerer.setRemoteDescription(new RTCSessionDescriptionInit
            {
                type = RTCSdpType.offer,
                sdp = offerSdp
            });
            RTCSessionDescriptionInit answer = answerer.createAnswer(null);
            await answerer.setLocalDescription(answer);
            return answer.sdp;
        }

        [Fact]
        public async Task RequestOffer_UnknownSession_ThrowsInvalidOperationException()
        {
            HubFixture fixture = BuildHub(connectionForSession: null);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => fixture.Hub.RequestOffer(SessionId));

            Assert.Null(await fixture.Bridge.GetBrowserLegAsync(ConnectionId, SessionId));
        }

        [Fact]
        public async Task RequestOffer_ValidSession_ReturnsSdpOfferStartingWithVersionLine()
        {
            HubFixture fixture = BuildHub(new FakeLiveViewConnection());

            try
            {
                string offer = await fixture.Hub.RequestOffer(SessionId);

                Assert.False(string.IsNullOrWhiteSpace(offer));
                Assert.StartsWith("v=0", offer);
            }
            finally
            {
                await fixture.Bridge.DisposeAllForConnectionAsync(ConnectionId);
            }
        }

        [Fact]
        public async Task RequestOffer_ValidSession_CreatesBrowserLegForCallerConnection()
        {
            HubFixture fixture = BuildHub(new FakeLiveViewConnection());

            try
            {
                await fixture.Hub.RequestOffer(SessionId);

                Assert.NotNull(await fixture.Bridge.GetBrowserLegAsync(ConnectionId, SessionId));
            }
            finally
            {
                await fixture.Bridge.DisposeAllForConnectionAsync(ConnectionId);
            }
        }

        [Fact]
        public async Task SubmitAnswer_NoBrowserLeg_ThrowsInvalidOperationException()
        {
            HubFixture fixture = BuildHub(new FakeLiveViewConnection());

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => fixture.Hub.SubmitAnswer(SessionId, "v=0"));
        }

        [Fact]
        public async Task SubmitAnswer_ValidAnswer_SetsRemoteAnswerOnBrowserLeg()
        {
            HubFixture fixture = BuildHub(new FakeLiveViewConnection());

            try
            {
                string offer = await fixture.Hub.RequestOffer(SessionId);
                string answer = await CreateRealAnswerAsync(offer);

                await fixture.Hub.SubmitAnswer(SessionId, answer);

                RTCPeerConnection? pc = await fixture.Bridge.GetBrowserLegAsync(ConnectionId, SessionId);
                Assert.NotNull(pc);
                Assert.NotNull(pc!.remoteDescription);
                Assert.Equal(RTCSdpType.answer, pc.remoteDescription!.type);
            }
            finally
            {
                await fixture.Bridge.DisposeAllForConnectionAsync(ConnectionId);
            }
        }

        [Fact]
        public async Task SubmitIceCandidate_NoBrowserLeg_ThrowsInvalidOperationException()
        {
            HubFixture fixture = BuildHub(new FakeLiveViewConnection());

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => fixture.Hub.SubmitIceCandidate(SessionId, "candidate:1 1 UDP 2122260223 192.168.1.10 54321 typ host", 0));
        }

        [Fact]
        public async Task SubmitIceCandidate_ValidCandidate_AddsCandidateToBrowserLegWithoutError()
        {
            HubFixture fixture = BuildHub(new FakeLiveViewConnection());

            try
            {
                string offer = await fixture.Hub.RequestOffer(SessionId);
                string answer = await CreateRealAnswerAsync(offer);
                await fixture.Hub.SubmitAnswer(SessionId, answer);

                // Forwarding into RTCPeerConnection.addIceCandidate cannot be observed directly because the
                // peer type is concrete; this asserts the hub reaches the peer without throwing.
                await fixture.Hub.SubmitIceCandidate(
                    SessionId, "candidate:1 1 UDP 2122260223 192.168.1.10 54321 typ host", 0);
            }
            finally
            {
                await fixture.Bridge.DisposeAllForConnectionAsync(ConnectionId);
            }
        }
    }
}
