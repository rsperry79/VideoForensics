using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

using VideoForensics.Providers.Ring;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using VideoForensics.Providers.Ring.Tests.Mocks;

namespace VideoForensics.Providers.Ring.Tests
{
    /// <summary>
    /// Mock-based tests for Ring Alarm control (RingAssetSocket) and WebRTC live view
    /// (RingLiveViewSession). The REST "ticket" leg of each flow is verified against
    /// MockHttpMessageHandler exactly like the rest of the mock test suite. The websocket portion is
    /// verified against a FakeWebSocketTransport - this covers message envelope construction (seq
    /// numbering, dst addressing, JSON shape) but NOT real ICE negotiation or DataUpdate push, which
    /// can't be meaningfully faked and remain unverified until run against real hardware.
    /// </summary>
    [TestClass]
    public class AlarmAndLiveViewTests
    {
        private MockSessionHelper _mockHelper = null!;
        private Session _mockSession = null!;

        [TestInitialize]
        public void Setup()
        {
            _mockHelper = new MockSessionHelper();
            _mockSession = _mockHelper.CreateSessionWithMockHandler();
        }

        [TestMethod]
        public async Task ConnectAssetSocket_RequestsTicketAndConnectsWebSocket()
        {
            var locationId = Guid.NewGuid();
            var mockHandler = _mockHelper.GetMockHandler();
            mockHandler.SetupResponse(
                $"api.ring.com/api/v1/clap/tickets?locationid={locationId:D}".ToLower(),
                System.Net.HttpStatusCode.OK,
                @"{ ""assets"": [""asset-1""], ""ticket"": ""ticket-abc"", ""host"": ""asset-host.ring.com"" }");
            await _mockSession.Authenticate();

            var transport = new FakeWebSocketTransport();
            var socket = await _mockSession.ConnectAssetSocket(locationId, transport);

            var call = mockHandler.RequestLog.LastOrDefault(r => r.Url.Contains("clap/tickets"));
            Assert.IsNotNull(call.Url, "Expected a request to the clap/tickets endpoint");
            Assert.IsTrue(call.Url.Contains($"locationID={locationId:D}"));

            Assert.IsNotNull(transport.ConnectedUri);
            Assert.AreEqual("asset-host.ring.com", transport.ConnectedUri.Host);
            Assert.AreEqual("ticket-abc", System.Web.HttpUtility.ParseQueryString(transport.ConnectedUri.Query)["authcode"]);

            await socket.CloseAsync();
        }

        [TestMethod]
        public async Task ArmAway_DiscoversSecurityPanelThenSendsSwitchModeAll()
        {
            var locationId = Guid.NewGuid();
            var mockHandler = _mockHelper.GetMockHandler();
            mockHandler.SetupResponse(
                $"api.ring.com/api/v1/clap/tickets?locationid={locationId:D}".ToLower(),
                System.Net.HttpStatusCode.OK,
                @"{ ""assets"": [""asset-1""], ""ticket"": ""ticket-abc"", ""host"": ""asset-host.ring.com"" }");
            await _mockSession.Authenticate();

            var transport = new FakeWebSocketTransport();
            transport.OnMessageSent = sent =>
            {
                var root = JsonDocument.Parse(sent).RootElement;
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

            var socket = await _mockSession.ConnectAssetSocket(locationId, transport);
            await socket.ArmAway();

            var switchModeMessage = transport.SentMessages
                .Select(m => JsonDocument.Parse(m).RootElement)
                .First(el => el.GetProperty("msg").GetProperty("msg").GetString() == "security-panel.switch-mode");

            var msg = switchModeMessage.GetProperty("msg");
            Assert.AreEqual("panel-1", msg.GetProperty("dst").GetString(), "Expected the command to address the panel's own zid");
            Assert.AreEqual("all", msg.GetProperty("body").GetProperty("mode").GetString());

            await socket.CloseAsync();
        }

        [TestMethod]
        public async Task Disarm_SendsSwitchModeNone()
        {
            var locationId = Guid.NewGuid();
            var mockHandler = _mockHelper.GetMockHandler();
            mockHandler.SetupResponse(
                $"api.ring.com/api/v1/clap/tickets?locationid={locationId:D}".ToLower(),
                System.Net.HttpStatusCode.OK,
                @"{ ""assets"": [""asset-1""], ""ticket"": ""ticket-abc"", ""host"": ""asset-host.ring.com"" }");
            await _mockSession.Authenticate();

            var transport = new FakeWebSocketTransport();
            transport.OnMessageSent = sent =>
            {
                var root = JsonDocument.Parse(sent).RootElement;
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

            var socket = await _mockSession.ConnectAssetSocket(locationId, transport);
            await socket.Disarm();

            var switchModeMessage = transport.SentMessages
                .Select(m => JsonDocument.Parse(m).RootElement)
                .First(el => el.GetProperty("msg").GetProperty("msg").GetString() == "security-panel.switch-mode");

            Assert.AreEqual("none", switchModeMessage.GetProperty("msg").GetProperty("body").GetProperty("mode").GetString());

            await socket.CloseAsync();
        }

        [TestMethod]
        public async Task GetDevices_ThrowsWhenNoSecurityPanelFoundOnArm()
        {
            var locationId = Guid.NewGuid();
            var mockHandler = _mockHelper.GetMockHandler();
            mockHandler.SetupResponse(
                $"api.ring.com/api/v1/clap/tickets?locationid={locationId:D}".ToLower(),
                System.Net.HttpStatusCode.OK,
                @"{ ""assets"": [""asset-1""], ""ticket"": ""ticket-abc"", ""host"": ""asset-host.ring.com"" }");
            await _mockSession.Authenticate();

            var transport = new FakeWebSocketTransport();
            transport.OnMessageSent = sent =>
            {
                var root = JsonDocument.Parse(sent).RootElement;
                if (root.GetProperty("msg").GetProperty("msg").GetString() == "DeviceInfoDocGetList")
                {
                    transport.Enqueue(@"{ ""channel"": ""message"", ""msg"": ""DeviceInfoDocGetList"", ""body"": [] }");
                }
            };

            var socket = await _mockSession.ConnectAssetSocket(locationId, transport);

            try
            {
                await socket.Disarm();
                Assert.Fail("Should have thrown InvalidOperationException when no security panel exists");
            }
            catch (InvalidOperationException) { }

            await socket.CloseAsync();
        }

        [TestMethod]
        public async Task ConnectAssetSocket_ThrowsWhenNotAuthenticated()
        {
            var session = _mockHelper.CreateSessionWithMockHandler();

            try
            {
                await session.ConnectAssetSocket(Guid.NewGuid(), new FakeWebSocketTransport());
                Assert.Fail("Should have thrown SessionNotAuthenticatedException");
            }
            catch (VideoForensics.Providers.Ring.Exceptions.SessionNotAuthenticatedException) { }
        }

        [TestMethod]
        public async Task StartLiveView_RequestsTicketAndSendsLiveViewOffer()
        {
            var mockHandler = _mockHelper.GetMockHandler();
            mockHandler.SetupResponse(
                "api.ring.com/api/v1/clap/ticket/request/signalsocket",
                System.Net.HttpStatusCode.OK,
                @"{ ""ticket"": ""signal-ticket-abc"" }");
            await _mockSession.Authenticate();

            var transport = new FakeWebSocketTransport();
            var liveView = await _mockSession.StartLiveView(123456, transport);

            var call = mockHandler.RequestLog.LastOrDefault(r => r.Url.Contains("clap/ticket/request/signalsocket"));
            Assert.IsNotNull(call.Url, "Expected a request to the signaling ticket endpoint");
            Assert.AreEqual(System.Net.Http.HttpMethod.Post, call.Method);

            Assert.IsNotNull(transport.ConnectedUri);
            Assert.AreEqual("api.prod.signalling.ring.devices.a2z.com", transport.ConnectedUri.Host);
            Assert.AreEqual("signal-ticket-abc", System.Web.HttpUtility.ParseQueryString(transport.ConnectedUri.Query)["token"]);

            // Locally gathered ICE candidates fire asynchronously as separate "ice" messages, and can
            // race the offer send itself - only the presence and content of the offer is asserted here.
            var offerMessage = transport.SentMessages
                .Select(m => JsonDocument.Parse(m).RootElement)
                .FirstOrDefault(el => el.GetProperty("method").GetString() == "live_view");
            Assert.AreNotEqual(JsonValueKind.Undefined, offerMessage.ValueKind, "Expected a live_view offer message to have been sent");
            Assert.AreEqual(123456, offerMessage.GetProperty("body").GetProperty("doorbot_id").GetInt64());
            Assert.IsFalse(string.IsNullOrEmpty(offerMessage.GetProperty("body").GetProperty("sdp").GetString()), "Expected a non-empty SDP offer");

            await liveView.CloseAsync();
        }

        [TestMethod]
        public async Task StartLiveView_ThrowsWhenNotAuthenticated()
        {
            var session = _mockHelper.CreateSessionWithMockHandler();

            try
            {
                await session.StartLiveView(123456, new FakeWebSocketTransport());
                Assert.Fail("Should have thrown SessionNotAuthenticatedException");
            }
            catch (VideoForensics.Providers.Ring.Exceptions.SessionNotAuthenticatedException) { }
        }
    }
}

