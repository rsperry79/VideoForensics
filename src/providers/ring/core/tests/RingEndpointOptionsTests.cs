using System.Net.Http;

using VideoForensics.Providers.Ring.Core.Tests.Mocks;

namespace VideoForensics.Providers.Ring.Core.Tests
{
    /// <summary>
    /// Tests for <see cref="RingEndpointOptions"/> and its use by <see cref="Session"/>.
    /// </summary>
    public class RingEndpointOptionsTests
    {
        public RingEndpointOptionsTests()
        {
            RingTestHardBanIsolation.ResetForTest();
        }

        [Fact]
        public void RingEndpointOptions_Defaults_MatchProductionHosts()
        {
            var o = new RingEndpointOptions();

            Assert.Equal("https://api.ring.com/clients_api/", o.ClientsApiBaseUrl.ToString());
            Assert.Equal("https://api.ring.com/devices/v1/", o.DevicesApiBaseUrl.ToString());
            Assert.Equal("https://prd-api-us.prd.rings.solutions/api/v1/", o.AppApiBaseUrl.ToString());
            Assert.Equal("https://api.ring.com/commands/v1/", o.CommandsApiBaseUrl.ToString());
            Assert.Equal("https://api.ring.com/groups/v1/", o.GroupsApiBaseUrl.ToString());
            Assert.Equal("https://prd-api-us.prd.rings.solutions/api/v1/mode/", o.ModeApiBaseUrl.ToString());
            Assert.Equal("https://prd-api-us.prd.rings.solutions/api/v1/rs/monitoring/accounts/", o.MonitoringApiBaseUrl.ToString());
            Assert.Equal("https://api.ring.com/evm/v2/history/locations/", o.EvmApiBaseUrl.ToString());
            Assert.Equal("https://api.ring.com/integrations/", o.IntegrationsApiBaseUrl.ToString());
            Assert.Equal("https://api.ring.com/recordings/public/", o.RecordingsApiBaseUrl.ToString());
            Assert.Equal("https://app-snaps.ring.com/", o.AppSnapsApiBaseUrl.ToString());
        }

        [Fact]
        public async Task Session_CustomModeApiBaseUrl_SendsLocationModeGetToCustomHost()
        {
            var handler = new MockHttpMessageHandler();
            var locationId = Guid.NewGuid();
            handler.SetupResponse($"custom.example.test/mode/location/{locationId:D}", System.Net.HttpStatusCode.OK, @"{ ""mode"": ""home"" }");
            var options = new RingEndpointOptions { ModeApiBaseUrl = new Uri("https://custom.example.test/mode/") };
            var session = new Session("u@example.com", "pw", handler, null, options);
            _ = await session.Authenticate();

            Entities.LocationMode result = await session.GetLocationMode(locationId);

            Assert.Equal("home", result.Mode);
            Assert.Contains(handler.RequestLog, r => r.Method == HttpMethod.Get && r.Url == $"https://custom.example.test/mode/location/{locationId:D}");
        }

        [Fact]
        public async Task Session_NoOptions_SendsLocationModeGetToDefaultHost()
        {
            var handler = new MockHttpMessageHandler();
            var locationId = Guid.NewGuid();
            handler.SetupResponse($"prd-api-us.prd.rings.solutions/api/v1/mode/location/{locationId:D}", System.Net.HttpStatusCode.OK, @"{ ""mode"": ""away"" }");
            var session = new Session("u@example.com", "pw", handler);
            _ = await session.Authenticate();

            Entities.LocationMode result = await session.GetLocationMode(locationId);

            Assert.Equal("away", result.Mode);
            Assert.Contains(handler.RequestLog, r => r.Method == HttpMethod.Get && r.Url == $"https://prd-api-us.prd.rings.solutions/api/v1/mode/location/{locationId:D}");
        }
    }
}
