using System.Net.Http;
using System.Text.Json;

using VideoForensics.Providers.Ring.Alarm;
using VideoForensics.Providers.Ring.Streaming;
using VideoForensics.Providers.Ring.Core.Tests.Mocks;

namespace VideoForensics.Providers.Ring.Core.Tests
{
    /// <summary>
    /// Mock-based tests for Ring Alarm control (RingAssetSocket) and WebRTC live view
    /// (RingLiveViewSession). The REST "ticket" leg of each flow is verified against
    /// MockHttpMessageHandler exactly like the rest of the mock test suite. The websocket portion is
    /// verified against a FakeWebSocketTransport - this covers message envelope construction (seq
    /// numbering, dst addressing, JSON shape) but NOT real ICE negotiation or DataUpdate push, which
    /// can't be meaningfully faked and remain unverified until run against real hardware.
    /// </summary>
    public class AlarmAndLiveViewTests
    {
        private readonly MockSessionHelper _mockHelper = null!;
        private readonly Session _mockSession = null!;

        public AlarmAndLiveViewTests()
        {
            _mockHelper = new MockSessionHelper();
            _mockSession = _mockHelper.CreateSessionWithMockHandler();
        }

        [Fact]
        public async Task ConnectAssetSocket_RequestsTicketAndConnectsWebSocket()
        {
            var locationId = Guid.NewGuid();
            MockHttpMessageHandler mockHandler = _mockHelper.GetMockHandler();
            mockHandler.SetupResponse(
                $"api.ring.com/api/v1/clap/tickets?locationid={locationId:D}".ToLower(),
                System.Net.HttpStatusCode.OK,
                @"{ ""assets"": [""asset-1""], ""ticket"": ""ticket-abc"", ""host"": ""asset-host.ring.com"" }");
            _ = await _mockSession.Authenticate();

            var transport = new FakeWebSocketTransport();
            RingAssetSocket socket = await _mockSession.ConnectAssetSocket(locationId, transport);

            (HttpMethod Method, string Url) = mockHandler.RequestLog.LastOrDefault(r => r.Url.Contains("clap/tickets"));
            Assert.NotNull(Url);
            Assert.True(Url.Contains($"locationID={locationId:D}"));

            Assert.NotNull(transport.ConnectedUri);
            Assert.Equal("asset-host.ring.com", transport.ConnectedUri.Host);
            Assert.Equal("ticket-abc", System.Web.HttpUtility.ParseQueryString(transport.ConnectedUri.Query)["authcode"]);

            await socket.CloseAsync();
        }

        [Fact]
        public async Task ArmAway_DiscoversSecurityPanelThenSendsSwitchModeAll()
        {
            var locationId = Guid.NewGuid();
            MockHttpMessageHandler mockHandler = _mockHelper.GetMockHandler();
            mockHandler.SetupResponse(
                $"api.ring.com/api/v1/clap/tickets?locationid={locationId:D}".ToLower(),
                System.Net.HttpStatusCode.OK,
                @"{ ""assets"": [""asset-1""], ""ticket"": ""ticket-abc"", ""host"": ""asset-host.ring.com"" }");
            _ = await _mockSession.Authenticate();

            var transport = new FakeWebSocketTransport();
            transport.OnMessageSent = sent =>
            {
                JsonElement root = JsonDocument.Parse(sent).RootElement;
                var msgType = root.GetProperty("msg").GetProperty("msg").GetString();

                if (msgType == "DeviceInfoDocGetList")
                {
                    transport.Enqueue(@"{
                        ""channel"": ""message"",
                        ""msg"": ""DeviceInfoDocGetList"",
                        ""body"": [ { ""zid"": ""panel-1"", ""deviceType"": ""security-panel"", ""name"": ""Alarm Panel"" } ]
                    }");
                }
                else if (msgType == "security-panel.switch-mode")
                {
                    transport.Enqueue(@"{ ""channel"": ""message"", ""msg"": ""security-panel.switch-mode"", ""body"": {} }");
                }
            };

            RingAssetSocket socket = await _mockSession.ConnectAssetSocket(locationId, transport);
            await socket.ArmAway();

            JsonElement switchModeMessage = transport.SentMessages
                .Select(m => JsonDocument.Parse(m).RootElement)
                .First(el => el.GetProperty("msg").GetProperty("msg").GetString() == "security-panel.switch-mode");

            JsonElement msg = switchModeMessage.GetProperty("msg");
            Assert.Equal("panel-1", msg.GetProperty("dst").GetString());
            Assert.Equal("all", msg.GetProperty("body").GetProperty("mode").GetString());

            await socket.CloseAsync();
        }

        [Fact]
        public async Task Disarm_SendsSwitchModeNone()
        {
            var locationId = Guid.NewGuid();
            MockHttpMessageHandler mockHandler = _mockHelper.GetMockHandler();
            mockHandler.SetupResponse(
                $"api.ring.com/api/v1/clap/tickets?locationid={locationId:D}".ToLower(),
                System.Net.HttpStatusCode.OK,
                @"{ ""assets"": [""asset-1""], ""ticket"": ""ticket-abc"", ""host"": ""asset-host.ring.com"" }");
            _ = await _mockSession.Authenticate();

            var transport = new FakeWebSocketTransport();
            transport.OnMessageSent = sent =>
            {
                JsonElement root = JsonDocument.Parse(sent).RootElement;
                var msgType = root.GetProperty("msg").GetProperty("msg").GetString();

                if (msgType == "DeviceInfoDocGetList")
                {
                    transport.Enqueue(@"{
                        ""channel"": ""message"",
                        ""msg"": ""DeviceInfoDocGetList"",
                        ""body"": [ { ""zid"": ""panel-1"", ""deviceType"": ""security-panel"" } ]
                    }");
                }
                else if (msgType == "security-panel.switch-mode")
                {
                    transport.Enqueue(@"{ ""channel"": ""message"", ""msg"": ""security-panel.switch-mode"", ""body"": {} }");
                }
            };

            RingAssetSocket socket = await _mockSession.ConnectAssetSocket(locationId, transport);
            await socket.Disarm();

            JsonElement switchModeMessage = transport.SentMessages
                .Select(m => JsonDocument.Parse(m).RootElement)
                .First(el => el.GetProperty("msg").GetProperty("msg").GetString() == "security-panel.switch-mode");

            Assert.Equal("none", switchModeMessage.GetProperty("msg").GetProperty("body").GetProperty("mode").GetString());

            await socket.CloseAsync();
        }

        [Fact]
        public async Task GetDevices_ThrowsWhenNoSecurityPanelFoundOnArm()
        {
            var locationId = Guid.NewGuid();
            MockHttpMessageHandler mockHandler = _mockHelper.GetMockHandler();
            mockHandler.SetupResponse(
                $"api.ring.com/api/v1/clap/tickets?locationid={locationId:D}".ToLower(),
                System.Net.HttpStatusCode.OK,
                @"{ ""assets"": [""asset-1""], ""ticket"": ""ticket-abc"", ""host"": ""asset-host.ring.com"" }");
            _ = await _mockSession.Authenticate();

            var transport = new FakeWebSocketTransport();
            transport.OnMessageSent = sent =>
            {
                JsonElement root = JsonDocument.Parse(sent).RootElement;
                if (root.GetProperty("msg").GetProperty("msg").GetString() == "DeviceInfoDocGetList")
                {
                    transport.Enqueue(@"{ ""channel"": ""message"", ""msg"": ""DeviceInfoDocGetList"", ""body"": [] }");
                }
            };

            RingAssetSocket socket = await _mockSession.ConnectAssetSocket(locationId, transport);

            try
            {
                await socket.Disarm();
                Assert.Fail("Should have thrown InvalidOperationException when no security panel exists");
            }
            catch (InvalidOperationException) { }

            await socket.CloseAsync();
        }

        [Fact]
        public async Task ConnectAssetSocket_ThrowsWhenNotAuthenticated()
        {
            Session session = _mockHelper.CreateSessionWithMockHandler();

            try
            {
                _ = await session.ConnectAssetSocket(Guid.NewGuid(), new FakeWebSocketTransport());
                Assert.Fail("Should have thrown SessionNotAuthenticatedException");
            }
            catch (Exceptions.SessionNotAuthenticatedException) { }
        }

        [Fact]
        public async Task StartLiveView_RequestsTicketAndSendsLiveViewOffer()
        {
            MockHttpMessageHandler mockHandler = _mockHelper.GetMockHandler();
            mockHandler.SetupResponse(
                "api.ring.com/api/v1/clap/ticket/request/signalsocket",
                System.Net.HttpStatusCode.OK,
                @"{ ""ticket"": ""signal-ticket-abc"" }");
            _ = await _mockSession.Authenticate();

            var transport = new FakeWebSocketTransport();
            RingLiveViewSession liveView = await _mockSession.StartLiveView(123456, transport);

            (HttpMethod Method, string Url) = mockHandler.RequestLog.LastOrDefault(r => r.Url.Contains("clap/ticket/request/signalsocket"));
            Assert.NotNull(Url);
            Assert.Equal(System.Net.Http.HttpMethod.Post, Method);

            Assert.NotNull(transport.ConnectedUri);
            Assert.Equal("api.prod.signalling.ring.devices.a2z.com", transport.ConnectedUri.Host);
            Assert.Equal("signal-ticket-abc", System.Web.HttpUtility.ParseQueryString(transport.ConnectedUri.Query)["token"]);

            // Locally gathered ICE candidates fire asynchronously as separate "ice" messages, and can
            // race the offer send itself - only the presence and content of the offer is asserted here.
            JsonElement offerMessage = transport.SentMessages
                .Select(m => JsonDocument.Parse(m).RootElement)
                .FirstOrDefault(el => el.GetProperty("method").GetString() == "live_view");
            Assert.NotEqual(JsonValueKind.Undefined, offerMessage.ValueKind);
            Assert.Equal(123456, offerMessage.GetProperty("body").GetProperty("doorbot_id").GetInt64());
            Assert.False(string.IsNullOrEmpty(offerMessage.GetProperty("body").GetProperty("sdp").GetString()), "Expected a non-empty SDP offer");

            await liveView.CloseAsync();
        }

        [Fact]
        public async Task StartLiveView_ThrowsWhenNotAuthenticated()
        {
            Session session = _mockHelper.CreateSessionWithMockHandler();

            try
            {
                _ = await session.StartLiveView(123456, new FakeWebSocketTransport());
                Assert.Fail("Should have thrown SessionNotAuthenticatedException");
            }
            catch (Exceptions.SessionNotAuthenticatedException) { }
        }
    }
}

