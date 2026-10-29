using System.Text.Json;
using System.Threading;

using SIPSorcery.Net;

using VideoForensics.Providers.Ring.Core.Tests.Mocks;
using VideoForensics.Providers.Ring.Sockets;
using VideoForensics.Providers.Ring.Streaming;

namespace VideoForensics.Providers.Ring.Core.Tests
{
    /// <summary>
    /// Real Ring API check for live view, using the refresh token saved in the database (see RealSessionHelper).
    /// It requests a signaling ticket, opens Ring's signaling websocket and negotiates WebRTC, so it calls Ring and uses
    /// provider budget. It skips when no credentials are saved and is meant for manual runs, to debug a refused handshake
    /// without the browser: <c>dotnet test --filter "FullyQualifiedName~RealLiveView"</c>.
    /// </summary>
    public class RealLiveViewIntegrationTests : IAsyncLifetime
    {
        private static readonly TimeSpan StartTimeout = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan MediaTimeout = TimeSpan.FromSeconds(25);

        private bool _credentialsAvailable;

        public ValueTask InitializeAsync()
        {
            _credentialsAvailable = RealSessionHelper.CredentialsAvailable();
            return ValueTask.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }

        [Fact]
        [Trait("Description", "Real API: starts a live view on a doorbot with the saved credentials and waits for the peer connection or media")]
        public async Task RealLiveView_StartsAndConnects()
        {
            Mocks.RealSessionHelper.RequireLive();
            if (!_credentialsAvailable)
            {
                Assert.Skip(RealSessionHelper.GetSetupInstructions());
                return;
            }

            Session session = await RealSessionHelper.CreateAuthenticatedSessionAsync();
            Entities.Devices devices = await session.GetRingDevices();
            // Doorbots and stickup cams are separate lists in Ring's response, and both can stream live view.
            var cameras = devices.Doorbots.Select(d => (Name: d.Description, Id: (long)d.Id))
                .Concat(devices.StickupCams.Where(c => c.Id is not null).Select(c => (Name: c.Description, Id: c.Id!.Value)))
                .ToList();
            string wantedName = Environment.GetEnvironmentVariable("RING_TEST_DOORBOT") ?? "Front Door";
            var camera = cameras.FirstOrDefault(c => string.Equals(c.Name, wantedName, StringComparison.OrdinalIgnoreCase));
            if (camera.Id == 0)
            {
                string available = string.Join(", ", cameras.Select(c => c.Name));
                Assert.Skip($"No camera named '{wantedName}' on this account (set RING_TEST_DOORBOT). Available: {available}");
                return;
            }

            // Act: the ticket request, signaling websocket and offer all happen inside StartLiveView. A refused handshake
            // surfaces here with the HTTP status and correlation id.
            using var startCts = new CancellationTokenSource(StartTimeout);
            var transport = new RecordingTransport(new ClientWebSocketTransport());
            RingLiveViewSession liveView = await session.StartLiveView(camera.Id, transport, startCts.Token);
            using (liveView)
            {
                var up = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
                RTCPeerConnectionState lastState = RTCPeerConnectionState.@new;
                liveView.OnConnectionStateChange += state =>
                {
                    lastState = state;
                    if (state == RTCPeerConnectionState.connected)
                    {
                        _ = up.TrySetResult("peer connection connected");
                    }
                };
                liveView.OnRtpPacketReceived += (_, _, _) => up.TrySetResult("media packet received");

                Task finished = await Task.WhenAny(up.Task, Task.Delay(MediaTimeout));

                // Assert
                Assert.True(
                    finished == up.Task,
                    $"Live view started but never connected within {MediaTimeout.TotalSeconds:0}s (last state: {lastState}). "
                    + $"Signaling messages received (method only): [{string.Join(", ", transport.ReceivedMethods)}]. "
                    + $"Sent methods in order: [{string.Join(", ", transport.SentMethods)}]. "
                    + $"Offer media lines: [{string.Join(" | ", transport.SentOfferLines)}].");
                await liveView.CloseAsync();
            }
        }

        /// <summary>
        /// Passes every call to the real websocket and records the <c>method</c> of each message Ring sends back, so a
        /// failure shows whether Ring answered. Message bodies (SDP, ICE, tokens) are never recorded.
        /// </summary>
        private sealed class RecordingTransport : IWebSocketTransport
        {
            private readonly IWebSocketTransport _inner;

            public RecordingTransport(IWebSocketTransport inner)
            {
                _inner = inner;
            }

            public List<string> ReceivedMethods { get; } = new();

            public Task ConnectAsync(Uri uri, CancellationToken cancellationToken) => _inner.ConnectAsync(uri, cancellationToken);

            public Task SendAsync(string message, CancellationToken cancellationToken)
            {
                SentMethods.Add(SentMethodOf(message));
                if (message.Contains("\"live_view\"", StringComparison.Ordinal))
                {
                    SentOfferLines = OfferMediaLines(message);
                }

                return _inner.SendAsync(message, cancellationToken);
            }

            /// <summary>The method of each message we sent, in order.</summary>
            public List<string> SentMethods { get; } = new();

            /// <summary>The media description lines of the offer we sent, without ICE credentials or fingerprints.</summary>
            public List<string> SentOfferLines { get; private set; } = new();

            private static string SentMethodOf(string message)
            {
                using JsonDocument document = JsonDocument.Parse(message);
                return document.RootElement.TryGetProperty("method", out JsonElement method) ? method.GetString() ?? "null" : "no-method";
            }

            private static List<string> OfferMediaLines(string message)
            {
                using JsonDocument document = JsonDocument.Parse(message);
                string sdp = document.RootElement.GetProperty("body").GetProperty("sdp").GetString() ?? string.Empty;
                return sdp.Split("\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Where(line => line.StartsWith("m=", StringComparison.Ordinal)
                        || line.StartsWith("a=rtpmap", StringComparison.Ordinal)
                        || line.StartsWith("a=fmtp", StringComparison.Ordinal)
                        || line.StartsWith("a=sendrecv", StringComparison.Ordinal)
                        || line.StartsWith("a=recvonly", StringComparison.Ordinal)
                        || line.StartsWith("a=sendonly", StringComparison.Ordinal)
                        || line.StartsWith("a=setup", StringComparison.Ordinal)
                        || line.StartsWith("a=mid", StringComparison.Ordinal))
                    .ToList();
            }

            public async Task<string?> ReceiveAsync(CancellationToken cancellationToken)
            {
                string? message = await _inner.ReceiveAsync(cancellationToken);
                if (message is not null)
                {
                    ReceivedMethods.Add(MethodOf(message));
                }

                return message;
            }

            public Task CloseAsync(CancellationToken cancellationToken) => _inner.CloseAsync(cancellationToken);

            public void Dispose() => _inner.Dispose();

            private static string MethodOf(string message)
            {
                try
                {
                    using JsonDocument document = JsonDocument.Parse(message);
                    if (!document.RootElement.TryGetProperty("method", out JsonElement method))
                    {
                        return "no-method";
                    }

                    string name = method.GetString() ?? "null";

                    // A close carries Ring's reason. Record the body's field names and the reason JSON only, never ids or tokens.
                    if (name == "close" && document.RootElement.TryGetProperty("body", out JsonElement body) && body.ValueKind == JsonValueKind.Object)
                    {
                        string fields = string.Join(",", body.EnumerateObject().Select(p => p.Name));
                        string reason = body.TryGetProperty("reason", out JsonElement r) ? r.GetRawText() : string.Empty;
                        return $"close{{fields={fields}, reason={(reason.Length > 300 ? reason[..300] : reason)}}}";
                    }

                    return name;
                }
                catch (JsonException)
                {
                    return "not-json";
                }
            }
        }
    }
}
