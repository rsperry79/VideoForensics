using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;

using VideoForensics.Providers.Ring;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using VideoForensics.Providers.Ring.Tests.Mocks;

namespace VideoForensics.Providers.Ring.Tests
{
    /// <summary>
    /// Confirms every API call surfaces through ApiRawLogger.OnRawResponse - the same channel the
    /// RingVideos app subscribes to for its api_raw_responses.jsonl diagnostic log. This locks in the
    /// fix for a gap where SendRequestWithExpectedStatusOutcome (used by every device-control/setter
    /// method) never raised the event, so all of those calls were silently missing from the raw log.
    /// </summary>
    [TestClass]
    public class RawApiLoggingTests
    {
        private MockSessionHelper _mockHelper = null!;
        private Session _mockSession = null!;
        private List<RawApiCall> _captured = null!;
        private Action<RawApiCall> _handler = null!;

        [TestInitialize]
        public void Setup()
        {
            _mockHelper = new MockSessionHelper();
            _mockSession = _mockHelper.CreateSessionWithMockHandler();
            _captured = new List<RawApiCall>();
            _handler = call => { lock (_captured) { _captured.Add(call); } };
            ApiRawLogger.OnRawResponse += _handler;
        }

        [TestCleanup]
        public void Cleanup()
        {
            ApiRawLogger.OnRawResponse -= _handler;
        }

        [TestMethod]
        public async Task SetLight_RaisesApiRawLoggerEvent()
        {
            var mockHandler = _mockHelper.GetMockHandler();
            mockHandler.SetupResponse("api.ring.com/clients_api/doorbots/123456/floodlight_light_on", System.Net.HttpStatusCode.OK, "");
            await _mockSession.Authenticate();

            await _mockSession.SetLight(123456, true);

            lock (_captured)
            {
                var call = _captured.Find(c => c.Url.Contains("floodlight_light_on"));
                Assert.IsNotNull(call, "SetLight should raise a raw API log event - it previously did not");
                Assert.AreEqual("PUT", call.Method);
                Assert.AreEqual(200, call.StatusCode);
            }
        }

        [TestMethod]
        public async Task SetVolume_RaisesApiRawLoggerEventWithRequestAndResponseBody()
        {
            var mockHandler = _mockHelper.GetMockHandler();
            mockHandler.SetupResponse("api.ring.com/clients_api/doorbots/123456", System.Net.HttpStatusCode.OK, "");
            await _mockSession.Authenticate();

            await _mockSession.SetVolume(123456, 7);

            lock (_captured)
            {
                var call = _captured.Find(c => c.Url.EndsWith("doorbots/123456") && c.Method == "PUT");
                Assert.IsNotNull(call, "SetVolume should raise a raw API log event");
                Assert.IsTrue(call.Body.Contains("doorbell_volume"), "Expected the request body to be captured in the log entry");
            }
        }

        [TestMethod]
        public async Task ArmAway_RaisesWsSendAndWsRecvEvents()
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
                    transport.Enqueue(@"{ ""channel"": ""message"", ""msg"": ""DeviceInfoDocGetList"", ""body"": [ { ""zid"": ""panel-1"", ""deviceType"": ""security-panel"" } ] }");
                }
                else if (msgType == "security-panel.switch-mode")
                {
                    transport.Enqueue(@"{ ""channel"": ""message"", ""msg"": ""security-panel.switch-mode"", ""body"": {} }");
                }
            };

            var socket = await _mockSession.ConnectAssetSocket(locationId, transport);
            await socket.ArmAway();
            await socket.CloseAsync();

            lock (_captured)
            {
                Assert.IsTrue(_captured.Exists(c => c.Method == "WS-SEND" && c.Body.Contains("security-panel.switch-mode")),
                    "Expected a WS-SEND raw log entry for the arm command");
                Assert.IsTrue(_captured.Exists(c => c.Method == "WS-RECV" && c.Body.Contains("DeviceInfoDocGetList")),
                    "Expected a WS-RECV raw log entry for the device discovery response");
            }
        }
    }
}
