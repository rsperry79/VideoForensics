using System.Collections.Concurrent;
using System.Reflection;

using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Moq;

using SIPSorcery.Net;
using SIPSorceryMedia.Abstractions;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Core.Providers.Common.Contracts;
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
        private static readonly TimeSpan AwaitTimeout = TimeSpan.FromSeconds(10);

        /// <summary>
        /// Minimal ILiveViewConnection; the hub only needs it as an opaque handle for the bridge.
        /// </summary>
        private sealed class FakeLiveViewConnection : ILiveViewConnection
        {
            public event Action<RTCPReceiverReportSampleDto>? OnReceiverReport { add { } remove { } }
            public event Action<long>? OnBitrateSampleBps { add { } remove { } }
            public event Action<LiveViewConnectionStateDto>? OnConnectionStateChange { add { } remove { } }
            public event Action<byte[], uint, int>? OnVideoRtpPayload { add { } remove { } }
            public event Action<byte[], uint, int, int>? OnAudioRtpPayload { add { } remove { } }

            public Task CloseAsync(CancellationToken ct) => Task.CompletedTask;

            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }

        /// <summary>
        /// Connection whose first <c>OnVideoRtpPayload</c> subscription blocks until released. The bridge
        /// subscribes from inside <c>CreateBrowserLegAsync</c> before it publishes the leg, so holding that
        /// call open lets a second <c>RequestOffer</c> run in the window the race needs. Also counts live
        /// subscriptions so a test can prove only one leg subscribed to RTP.
        /// </summary>
        private sealed class GatedLiveViewConnection : ILiveViewConnection
        {
            private readonly object _gate = new();
            private readonly TaskCompletionSource _firstSubscriptionEntered =
                new(TaskCreationOptions.RunContinuationsAsynchronously);
            private readonly TaskCompletionSource _releaseFirstSubscription =
                new(TaskCreationOptions.RunContinuationsAsynchronously);
            private int _subscribeAttempts;
            private Action<byte[], uint, int>? _videoRtp;

            /// <summary>Completes when the first subscriber has entered and is blocked.</summary>
            public Task FirstSubscriptionEntered => _firstSubscriptionEntered.Task;

            /// <summary>Count of subscriptions currently attached to the video RTP event.</summary>
            public int ActiveVideoSubscriptions
            {
                get { lock (_gate) { return _videoRtp?.GetInvocationList().Length ?? 0; } }
            }

            public void ReleaseFirstSubscription() => _releaseFirstSubscription.TrySetResult();

            public event Action<RTCPReceiverReportSampleDto>? OnReceiverReport { add { } remove { } }
            public event Action<long>? OnBitrateSampleBps { add { } remove { } }
            public event Action<LiveViewConnectionStateDto>? OnConnectionStateChange { add { } remove { } }

            public event Action<byte[], uint, int, int>? OnAudioRtpPayload { add { } remove { } }

            public event Action<byte[], uint, int>? OnVideoRtpPayload
            {
                add
                {
                    if (Interlocked.Increment(ref _subscribeAttempts) == 1)
                    {
                        _firstSubscriptionEntered.TrySetResult();
                        _releaseFirstSubscription.Task.Wait();
                    }

                    lock (_gate)
                    {
                        _videoRtp = (Action<byte[], uint, int>?)Delegate.Combine(_videoRtp, value);
                    }
                }
                remove
                {
                    lock (_gate)
                    {
                        _videoRtp = (Action<byte[], uint, int>?)Delegate.Remove(_videoRtp, value);
                    }
                }
            }

            public Task CloseAsync(CancellationToken ct) => Task.CompletedTask;

            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }

        /// <summary>
        /// Logger that records the most recent error-level exception so a test can await it
        /// instead of sleeping.
        /// </summary>
        private sealed class CapturingLogger<T> : ILogger<T>
        {
            public TaskCompletionSource<Exception?> ErrorLogged { get; } =
                new(TaskCreationOptions.RunContinuationsAsynchronously);

            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                if (logLevel == LogLevel.Error)
                {
                    ErrorLogged.TrySetResult(exception);
                }
            }
        }

        private sealed class HubFixture
        {
            public required LiveViewSignalingHub Hub { get; init; }
            public required BrowserLiveViewBridge Bridge { get; init; }
            public required Mock<ILiveViewSessionService> SessionService { get; init; }
            public required Mock<ISingleClientProxy> Caller { get; init; }
        }

        private static HubFixture BuildHub(
            ILiveViewConnection? connectionForSession,
            ILogger<LiveViewSignalingHub>? logger = null,
            CancellationToken connectionAborted = default,
            BrowserLiveViewOptions? options = null)
        {
            IOptions<BrowserLiveViewOptions> resolvedOptions = Options.Create(options ?? new BrowserLiveViewOptions());

            var sessionService = new Mock<ILiveViewSessionService>();
            sessionService
                .Setup(s => s.GetConnectionForSessionAsync(SessionId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(connectionForSession);
            sessionService
                .Setup(s => s.GetConnectionForSessionAsync(It.Is<Guid>(id => id != SessionId), It.IsAny<CancellationToken>()))
                .ReturnsAsync((ILiveViewConnection?)null);

            var bridge = new BrowserLiveViewBridge(
                Microsoft.Extensions.Logging.Abstractions.NullLogger<BrowserLiveViewBridge>.Instance,
                resolvedOptions);

            var caller = new Mock<ISingleClientProxy>();
            var clients = new Mock<IHubCallerClients>();
            clients.Setup(c => c.Caller).Returns(caller.Object);

            var context = new Mock<HubCallerContext>();
            context.Setup(c => c.ConnectionId).Returns(ConnectionId);
            context.Setup(c => c.ConnectionAborted).Returns(connectionAborted);

            var hub = new LiveViewSignalingHub(
                sessionService.Object,
                bridge,
                resolvedOptions,
                logger ??Microsoft.Extensions.Logging.Abstractions.NullLogger<LiveViewSignalingHub>.Instance)
            {
                Clients = clients.Object,
                Context = context.Object,
            };

            return new HubFixture { Hub = hub, Bridge = bridge, SessionService = sessionService, Caller = caller };
        }

        /// <summary>
        /// Returns how many handlers are subscribed to the peer's <c>onicecandidate</c> event.
        /// SIPSorcery exposes no subscriber count, so this reads the event's backing delegate.
        /// Throws if the field is missing or not a delegate, so a SIPSorcery rename fails loudly
        /// instead of returning 0.
        /// </summary>
        private static int CountIceCandidateHandlers(RTCPeerConnection pc)
        {
            const string FieldName = "_onIceCandidate";
            FieldInfo field = typeof(RTCPeerConnection).GetField(
                FieldName, BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException(
                    $"SIPSorcery RTCPeerConnection field '{FieldName}' not found. The test helper relies on it; SIPSorcery 10.0.17 may have renamed it.");

            if (!typeof(Delegate).IsAssignableFrom(field.FieldType))
            {
                throw new InvalidOperationException(
                    $"SIPSorcery RTCPeerConnection field '{FieldName}' is {field.FieldType.FullName}, not a delegate. The helper expects SIPSorcery 10.0.17 layout.");
            }

            // A null delegate means no subscribers, which is a valid zero count.
            object? value = field.GetValue(pc);
            if (value is null)
            {
                return 0;
            }

            if (value is not Delegate handlers)
            {
                throw new InvalidOperationException(
                    $"SIPSorcery RTCPeerConnection field '{FieldName}' holds {value.GetType().FullName}, not a delegate. The helper expects SIPSorcery 10.0.17 layout.");
            }

            return handlers.GetInvocationList().Length;
        }

        /// <summary>
        /// Early warning for a SIPSorcery rename: if the private backing field disappears or changes type,
        /// the helper throws here instead of silently returning a wrong handler count.
        /// </summary>
        [Fact]
        public void CountIceCandidateHandlers_SipSorceryField_IsPresent()
        {
            using var pc = new RTCPeerConnection(new RTCConfiguration());

            int count = CountIceCandidateHandlers(pc);

            Assert.Equal(0, count);
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
        public async Task RequestOffer_ValidSession_OfferContainsServerCandidates()
        {
            HubFixture fixture = BuildHub(new FakeLiveViewConnection());

            try
            {
                string offer = await fixture.Hub.RequestOffer(SessionId);

                // Non-trickle: the offer must carry its candidates, since the browser gets no later candidate push.
                Assert.Contains(
                    offer.Split('\n'),
                    line => line.TrimEnd('\r').StartsWith("a=candidate:", StringComparison.Ordinal));
            }
            finally
            {
                await fixture.Bridge.DisposeAllForConnectionAsync(ConnectionId);
            }
        }

        [Fact]
        public async Task RequestOffer_ValidSession_DoesNotPushIceCandidates()
        {
            HubFixture fixture = BuildHub(new FakeLiveViewConnection());
            var pushedMethods = new ConcurrentQueue<string>();
            fixture.Caller
                .Setup(c => c.SendCoreAsync(It.IsAny<string>(), It.IsAny<object?[]>(), It.IsAny<CancellationToken>()))
                .Callback<string, object?[], CancellationToken>((method, _, _) => pushedMethods.Enqueue(method))
                .Returns(Task.CompletedTask);

            try
            {
                await fixture.Hub.RequestOffer(SessionId);

                // A trickle push would run on SIPSorcery's gathering thread after RequestOffer returns.
                // Wait a bounded window so such a push has time to appear before the assertion.
                await Task.Delay(TimeSpan.FromSeconds(2));

                Assert.DoesNotContain("ReceiveIceCandidate", pushedMethods);
            }
            finally
            {
                await fixture.Bridge.DisposeAllForConnectionAsync(ConnectionId);
            }
        }

        [Fact]
        public async Task RequestOffer_ConcurrentCallsSameSession_CreatesOneBrowserLeg()
        {
            var connection = new GatedLiveViewConnection();
            HubFixture fixture = BuildHub(connection);

            try
            {
                // First call stalls inside the bridge's RTP subscription, before its leg is published.
                Task<string> first = Task.Run(() => fixture.Hub.RequestOffer(SessionId));
                await connection.FirstSubscriptionEntered.WaitAsync(AwaitTimeout);

                // Second call runs while the first is mid-creation, then the first is released.
                Task<string> second = Task.Run(() => fixture.Hub.RequestOffer(SessionId));
                connection.ReleaseFirstSubscription();

                string[] offers = await Task.WhenAll(first, second).WaitAsync(AwaitTimeout);

                // One leg means one offer and one RTP subscription. The offer is non-trickle, so the peer
                // must have no ICE push handler; a second leg would have created one.
                Assert.Equal(offers[0], offers[1]);
                Assert.Equal(1, connection.ActiveVideoSubscriptions);

                RTCPeerConnection? pc = await fixture.Bridge.GetBrowserLegAsync(ConnectionId, SessionId);
                Assert.NotNull(pc);
                Assert.Equal(0, CountIceCandidateHandlers(pc!));
            }
            finally
            {
                connection.ReleaseFirstSubscription();
                await fixture.Bridge.DisposeAllForConnectionAsync(ConnectionId);
            }
        }

        /// <summary>
        /// Options pointing at a STUN server that never answers (TEST-NET-1, RFC 5737), with a short
        /// gathering timeout so the timeout path runs quickly.
        /// </summary>
        private static BrowserLiveViewOptions UnreachableStunOptions() => new()
        {
            IceServerUrls = ["stun:192.0.2.1:3478"],
            OfferGatheringTimeout = TimeSpan.FromSeconds(1),
        };

        /// <summary>
        /// Awaits a RequestOffer that must fail. The test deadline makes a hang fail the test rather than block the run.
        /// </summary>
        private static async Task<InvalidOperationException> RequestOfferExpectingFailureAsync(LiveViewSignalingHub hub)
        {
            Task<string> offer = hub.RequestOffer(SessionId);
            Task finished = await Task.WhenAny(offer, Task.Delay(AwaitTimeout));
            Assert.Same(offer, finished);

            return await Assert.ThrowsAsync<InvalidOperationException>(() => offer);
        }

        [Fact]
        public async Task RequestOffer_StunUnreachable_ThrowsWithinTimeout()
        {
            HubFixture fixture = BuildHub(new FakeLiveViewConnection(), options: UnreachableStunOptions());

            try
            {
                InvalidOperationException error = await RequestOfferExpectingFailureAsync(fixture.Hub);

                Assert.Contains("timed out", error.Message);
            }
            finally
            {
                await fixture.Bridge.DisposeAllForConnectionAsync(ConnectionId);
            }
        }

        [Fact]
        public async Task RequestOffer_GatheringTimedOut_RemovesBrowserLeg()
        {
            HubFixture fixture = BuildHub(new FakeLiveViewConnection(), options: UnreachableStunOptions());

            try
            {
                await RequestOfferExpectingFailureAsync(fixture.Hub);

                Assert.Null(await fixture.Bridge.GetBrowserLegAsync(ConnectionId, SessionId));
            }
            finally
            {
                await fixture.Bridge.DisposeAllForConnectionAsync(ConnectionId);
            }
        }

        [Fact]
        public async Task RequestOffer_GatheringTimedOut_RetryAllowedAfterFailure()
        {
            HubFixture fixture = BuildHub(new FakeLiveViewConnection(), options: UnreachableStunOptions());

            try
            {
                await RequestOfferExpectingFailureAsync(fixture.Hub);

                // A leaked gate would block this call forever; it must fail the same way as the first.
                InvalidOperationException second = await RequestOfferExpectingFailureAsync(fixture.Hub);

                Assert.Contains("timed out", second.Message);
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
        public async Task SubmitAnswer_RemoteDescriptionRejected_ThrowsInvalidOperationException()
        {
            HubFixture fixture = BuildHub(new FakeLiveViewConnection());

            try
            {
                await fixture.Hub.RequestOffer(SessionId);

                // Malformed SDP: SIPSorcery cannot apply it as an answer to the local offer.
                await Assert.ThrowsAsync<InvalidOperationException>(
                    () => fixture.Hub.SubmitAnswer(SessionId, "v=0\r\nnot-a-valid-sdp"));
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

        [Theory]
        [InlineData(-1)]
        [InlineData(ushort.MaxValue + 1)]
        public async Task SubmitIceCandidate_OutOfRangeMLineIndex_ThrowsArgumentOutOfRangeException(int sdpMLineIndex)
        {
            HubFixture fixture = BuildHub(new FakeLiveViewConnection());

            try
            {
                await fixture.Hub.RequestOffer(SessionId);

                // Without the range check, (ushort)-1 silently becomes 65535.
                await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
                    () => fixture.Hub.SubmitIceCandidate(
                        SessionId, "candidate:1 1 UDP 2122260223 192.168.1.10 54321 typ host", sdpMLineIndex));
            }
            finally
            {
                await fixture.Bridge.DisposeAllForConnectionAsync(ConnectionId);
            }
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
