using Microsoft.Extensions.Logging.Abstractions;

using SIPSorcery.Net;

using VideoForensics.Providers.Common.Contracts;
using VideoForensics.WebApp.Services;

using Xunit;

namespace VideoForensics.WebApp.Tests
{
    /// <summary>
    /// Exercises <see cref="BrowserLiveViewBridge"/> with a real <see cref="RTCPeerConnection"/> (SIPSorcery's
    /// peer type is concrete and not mockable) and a hand-written <see cref="ILiveViewConnection"/> fake, whose
    /// <c>OnVideoRtpPayload</c> subscriber list can be inspected directly.
    /// </summary>
    public class BrowserLiveViewBridgeTests
    {
        private const string ConnectionId = "conn-bridge";
        private static readonly Guid SessionId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        /// <summary>
        /// Minimal ILiveViewConnection that records how many RTP subscribers are attached to it.
        /// </summary>
        private sealed class FakeLiveViewConnection : ILiveViewConnection
        {
            public event Action<RTCPReceiverReportSampleDto>? OnReceiverReport { add { } remove { } }
            public event Action<long>? OnBitrateSampleBps { add { } remove { } }
            public event Action<LiveViewConnectionStateDto>? OnConnectionStateChange { add { } remove { } }
            public event Action<byte[], uint, int>? OnVideoRtpPayload;

            public int VideoRtpSubscriberCount => OnVideoRtpPayload?.GetInvocationList().Length ?? 0;

            public Task CloseAsync(CancellationToken ct) => Task.CompletedTask;

            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }

        private static BrowserLiveViewBridge BuildBridge() =>
            new(
                Microsoft.Extensions.Logging.Abstractions.NullLogger<BrowserLiveViewBridge>.Instance,
                Microsoft.Extensions.Options.Options.Create(new BrowserLiveViewOptions()));

        [Fact]
        public async Task CreateBrowserLegAsync_NullConnection_ThrowsArgumentNullException()
        {
            BrowserLiveViewBridge bridge = BuildBridge();

            await Assert.ThrowsAsync<ArgumentNullException>(
                () => bridge.CreateBrowserLegAsync(ConnectionId, SessionId, null!));
        }

        [Fact]
        public async Task CreateBrowserLegAsync_NewKey_SubscribesOneRtpHandlerToProviderConnection()
        {
            BrowserLiveViewBridge bridge = BuildBridge();
            var ringConnection = new FakeLiveViewConnection();

            RTCPeerConnection pc = await bridge.CreateBrowserLegAsync(ConnectionId, SessionId, ringConnection);

            try
            {
                Assert.NotNull(pc);
                Assert.Equal(1, ringConnection.VideoRtpSubscriberCount);
                Assert.Same(pc, await bridge.GetBrowserLegAsync(ConnectionId, SessionId));
            }
            finally
            {
                await bridge.DisposeAllForConnectionAsync(ConnectionId);
            }
        }

        [Fact]
        public async Task CreateBrowserLegAsync_CalledTwiceForSameKey_ReusesExistingPeer()
        {
            BrowserLiveViewBridge bridge = BuildBridge();
            var ringConnection = new FakeLiveViewConnection();

            RTCPeerConnection first = await bridge.CreateBrowserLegAsync(ConnectionId, SessionId, ringConnection);

            try
            {
                RTCPeerConnection second = await bridge.CreateBrowserLegAsync(ConnectionId, SessionId, ringConnection);

                Assert.Same(first, second);
                Assert.Same(first, await bridge.GetBrowserLegAsync(ConnectionId, SessionId));
            }
            finally
            {
                await bridge.DisposeAllForConnectionAsync(ConnectionId);
            }
        }

        [Fact]
        public async Task CreateBrowserLegAsync_CalledTwiceForSameKey_DoesNotLeakSecondRtpSubscription()
        {
            BrowserLiveViewBridge bridge = BuildBridge();
            var ringConnection = new FakeLiveViewConnection();

            await bridge.CreateBrowserLegAsync(ConnectionId, SessionId, ringConnection);

            try
            {
                await bridge.CreateBrowserLegAsync(ConnectionId, SessionId, ringConnection);

                Assert.Equal(1, ringConnection.VideoRtpSubscriberCount);
            }
            finally
            {
                await bridge.DisposeAllForConnectionAsync(ConnectionId);
            }
        }

        [Fact]
        public async Task GetBrowserLegAsync_UnknownKey_ReturnsNull()
        {
            BrowserLiveViewBridge bridge = BuildBridge();

            RTCPeerConnection? pc = await bridge.GetBrowserLegAsync(ConnectionId, SessionId);

            Assert.Null(pc);
        }

        [Fact]
        public async Task DisposeBrowserLegAsync_ExistingPeer_RemovesRtpSubscription()
        {
            BrowserLiveViewBridge bridge = BuildBridge();
            var ringConnection = new FakeLiveViewConnection();
            await bridge.CreateBrowserLegAsync(ConnectionId, SessionId, ringConnection);

            await bridge.DisposeBrowserLegAsync(ConnectionId, SessionId);

            Assert.Equal(0, ringConnection.VideoRtpSubscriberCount);
        }

        [Fact]
        public async Task DisposeBrowserLegAsync_ExistingPeer_ClosesPeerConnectionAndRemovesEntry()
        {
            BrowserLiveViewBridge bridge = BuildBridge();
            var ringConnection = new FakeLiveViewConnection();
            RTCPeerConnection pc = await bridge.CreateBrowserLegAsync(ConnectionId, SessionId, ringConnection);

            await bridge.DisposeBrowserLegAsync(ConnectionId, SessionId);

            Assert.Equal(RTCPeerConnectionState.closed, pc.connectionState);
            Assert.Null(await bridge.GetBrowserLegAsync(ConnectionId, SessionId));
        }

        [Fact]
        public async Task DisposeBrowserLegAsync_UnknownKey_DoesNotThrow()
        {
            BrowserLiveViewBridge bridge = BuildBridge();

            await bridge.DisposeBrowserLegAsync(ConnectionId, SessionId);
        }

        [Fact]
        public async Task DisposeAllForConnectionAsync_SameConnection_DisposesOnlyThatConnectionsPeers()
        {
            BrowserLiveViewBridge bridge = BuildBridge();
            var ringConnection = new FakeLiveViewConnection();
            var otherSessionId = Guid.Parse("22222222-2222-2222-2222-222222222222");
            const string otherConnectionId = "conn-other";

            await bridge.CreateBrowserLegAsync(ConnectionId, SessionId, ringConnection);
            await bridge.CreateBrowserLegAsync(ConnectionId, otherSessionId, ringConnection);
            RTCPeerConnection otherConnectionPeer = await bridge.CreateBrowserLegAsync(otherConnectionId, SessionId, ringConnection);

            try
            {
                await bridge.DisposeAllForConnectionAsync(ConnectionId);

                Assert.Null(await bridge.GetBrowserLegAsync(ConnectionId, SessionId));
                Assert.Null(await bridge.GetBrowserLegAsync(ConnectionId, otherSessionId));
                Assert.Same(otherConnectionPeer, await bridge.GetBrowserLegAsync(otherConnectionId, SessionId));
                Assert.Equal(1, ringConnection.VideoRtpSubscriberCount);
            }
            finally
            {
                await bridge.DisposeAllForConnectionAsync(otherConnectionId);
            }
        }
    }
}
