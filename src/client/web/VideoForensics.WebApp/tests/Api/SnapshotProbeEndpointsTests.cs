using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Moq;

using VideoForensics.Providers.Ring.Entities;
using VideoForensics.Providers.Ring.Exceptions;
using VideoForensics.Providers.Ring.Services;
using VideoForensics.WebApp.Api;

using Xunit;

namespace VideoForensics.WebApp.Tests.Api
{
    /// <summary>
    /// Pipeline tests for the probe-only snapshot endpoints under /api/v1/probe. Requests run through an
    /// in-memory TestServer; the backend that stands in for the Ring Session is mocked.
    /// </summary>
    public class SnapshotProbeEndpointsTests
    {
        private const string Token = "probe-test-token";
        private static readonly Guid AccountId = Guid.Parse("6f1c2b7e-3d4a-4e5f-8a9b-0c1d2e3f4a5b");
        private const long DoorbotId = 123456789L;

        private static readonly byte[] SnapshotBytes = Encoding.UTF8.GetBytes("probe-test-stream-bytes");

        private static string Url(string suffix = "latest-snapshot") =>
            $"/api/v1/probe/ring/{AccountId:D}/doorbots/{DoorbotId}/{suffix}";

        private static Dictionary<string, string?> EnabledConfig(string token = Token) => new()
        {
            ["SnapshotProbe:Enabled"] = "true",
            ["SnapshotProbe:Token"] = token,
        };

        private static async Task<WebApplication> BuildAsync(Dictionary<string, string?> config, ISnapshotProbeBackend backend)
        {
            WebApplicationBuilder builder = WebApplication.CreateBuilder();
            builder.Configuration.AddInMemoryCollection(config);
            _ = builder.WebHost.UseTestServer();
            _ = builder.Services.AddSingleton(backend);

            WebApplication app = builder.Build();
            app.MapSnapshotProbeEndpoints();
            await app.StartAsync();
            return app;
        }

        private static HttpRequestMessage Request(HttpMethod method, string url, string? token)
        {
            var request = new HttpRequestMessage(method, url);
            if (token != null)
            {
                request.Headers.Add("X-Probe-Token", token);
            }
            return request;
        }

        private static Mock<ISnapshotProbeBackend> BackendReturningSnapshot()
        {
            var backend = new Mock<ISnapshotProbeBackend>();
            _ = backend.Setup(b => b.GetLatestSnapshotAsync(AccountId, DoorbotId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(SnapshotBytes);
            return backend;
        }

        [Fact]
        public async Task SnapshotProbeEndpoints_DisabledByConfig_RoutesNotMapped()
        {
            var config = new Dictionary<string, string?> { ["SnapshotProbe:Enabled"] = "false", ["SnapshotProbe:Token"] = Token };
            Mock<ISnapshotProbeBackend> backend = BackendReturningSnapshot();

            await using WebApplication app = await BuildAsync(config, backend.Object);

            var probeRoutes = app.Services.GetRequiredService<EndpointDataSource>().Endpoints
                .OfType<RouteEndpoint>()
                .Where(e => e.RoutePattern.RawText != null && e.RoutePattern.RawText.StartsWith("/api/v1/probe", StringComparison.Ordinal))
                .ToList();
            Assert.Empty(probeRoutes);

            using HttpClient client = app.GetTestClient();
            using HttpResponseMessage response = await client.SendAsync(Request(HttpMethod.Get, Url(), Token));
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            backend.Verify(b => b.GetLatestSnapshotAsync(It.IsAny<Guid>(), It.IsAny<long>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task SnapshotProbeEndpoints_EmptyToken_RoutesNotMapped()
        {
            Mock<ISnapshotProbeBackend> backend = BackendReturningSnapshot();

            await using WebApplication app = await BuildAsync(EnabledConfig(token: ""), backend.Object);

            var probeRoutes = app.Services.GetRequiredService<EndpointDataSource>().Endpoints
                .OfType<RouteEndpoint>()
                .Where(e => e.RoutePattern.RawText != null && e.RoutePattern.RawText.StartsWith("/api/v1/probe", StringComparison.Ordinal))
                .ToList();
            Assert.Empty(probeRoutes);
        }

        [Fact]
        public async Task SnapshotProbeEndpoints_MissingToken_Returns404()
        {
            Mock<ISnapshotProbeBackend> backend = BackendReturningSnapshot();

            await using WebApplication app = await BuildAsync(EnabledConfig(), backend.Object);
            using HttpClient client = app.GetTestClient();
            using HttpResponseMessage response = await client.SendAsync(Request(HttpMethod.Get, Url(), token: null));

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            backend.Verify(b => b.GetLatestSnapshotAsync(It.IsAny<Guid>(), It.IsAny<long>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task SnapshotProbeEndpoints_WrongToken_Returns404()
        {
            Mock<ISnapshotProbeBackend> backend = BackendReturningSnapshot();

            await using WebApplication app = await BuildAsync(EnabledConfig(), backend.Object);
            using HttpClient client = app.GetTestClient();
            using HttpResponseMessage response = await client.SendAsync(Request(HttpMethod.Get, Url(), "not-the-token"));

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            backend.Verify(b => b.GetLatestSnapshotAsync(It.IsAny<Guid>(), It.IsAny<long>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task LatestSnapshot_CorrectToken_ReturnsBytesAndShaPrefix()
        {
            Mock<ISnapshotProbeBackend> backend = BackendReturningSnapshot();

            await using WebApplication app = await BuildAsync(EnabledConfig(), backend.Object);
            using HttpClient client = app.GetTestClient();
            using HttpResponseMessage response = await client.SendAsync(Request(HttpMethod.Get, Url(), Token));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using JsonDocument json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            JsonElement root = json.RootElement;

            string expectedPrefix = Convert.ToHexString(SHA256.HashData(SnapshotBytes)).ToLowerInvariant()[..12];
            Assert.Equal(200, root.GetProperty("httpStatus").GetInt32());
            Assert.Equal(SnapshotBytes.Length, root.GetProperty("bytes").GetInt32());
            Assert.Equal(expectedPrefix, root.GetProperty("sha256Prefix").GetString());
            Assert.True(DateTimeOffset.TryParse(root.GetProperty("utc").GetString(), out _));
        }

        [Fact]
        public async Task LatestSnapshot_ThrottledWithRetryAfter_Returns429WithRetryAfterHeader()
        {
            var backend = new Mock<ISnapshotProbeBackend>();
            _ = backend.Setup(b => b.GetLatestSnapshotAsync(AccountId, DoorbotId, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new ThrottledException("Ring throttled") { RetryAfter = TimeSpan.FromSeconds(42) });

            await using WebApplication app = await BuildAsync(EnabledConfig(), backend.Object);
            using HttpClient client = app.GetTestClient();
            using HttpResponseMessage response = await client.SendAsync(Request(HttpMethod.Get, Url(), Token));

            Assert.Equal((HttpStatusCode)429, response.StatusCode);
            Assert.True(response.Headers.TryGetValues("Retry-After", out IEnumerable<string>? values));
            Assert.Equal("42", Assert.Single(values!));
        }

        [Fact]
        public async Task LatestSnapshot_ThrottledWithoutRetryAfter_Returns429WithoutRetryAfterHeader()
        {
            var backend = new Mock<ISnapshotProbeBackend>();
            _ = backend.Setup(b => b.GetLatestSnapshotAsync(AccountId, DoorbotId, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new ThrottledException("Ring throttled"));

            await using WebApplication app = await BuildAsync(EnabledConfig(), backend.Object);
            using HttpClient client = app.GetTestClient();
            using HttpResponseMessage response = await client.SendAsync(Request(HttpMethod.Get, Url(), Token));

            Assert.Equal((HttpStatusCode)429, response.StatusCode);
            Assert.False(response.Headers.Contains("Retry-After"));
        }

        [Fact]
        public async Task LatestSnapshot_NoSession_Returns503()
        {
            var backend = new Mock<ISnapshotProbeBackend>();
            _ = backend.Setup(b => b.GetLatestSnapshotAsync(AccountId, DoorbotId, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new SnapshotProbeSessionUnavailableException());

            await using WebApplication app = await BuildAsync(EnabledConfig(), backend.Object);
            using HttpClient client = app.GetTestClient();
            using HttpResponseMessage response = await client.SendAsync(Request(HttpMethod.Get, Url(), Token));

            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        }

        [Fact]
        public async Task LatestSnapshot_OtherException_Returns502WithoutEchoingExceptionText()
        {
            var backend = new Mock<ISnapshotProbeBackend>();
            _ = backend.Setup(b => b.GetLatestSnapshotAsync(AccountId, DoorbotId, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("refresh_token=SECRET-VALUE"));

            await using WebApplication app = await BuildAsync(EnabledConfig(), backend.Object);
            using HttpClient client = app.GetTestClient();
            using HttpResponseMessage response = await client.SendAsync(Request(HttpMethod.Get, Url(), Token));

            Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
            string body = await response.Content.ReadAsStringAsync();
            Assert.DoesNotContain("SECRET-VALUE", body);
        }

        [Fact]
        public async Task FreshSnapshot_CorrectToken_Returns204AndCallsUpdateOnce()
        {
            var backend = new Mock<ISnapshotProbeBackend>();
            _ = backend.Setup(b => b.UpdateSnapshotAsync(AccountId, DoorbotId, It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            await using WebApplication app = await BuildAsync(EnabledConfig(), backend.Object);
            using HttpClient client = app.GetTestClient();
            using HttpResponseMessage response = await client.SendAsync(Request(HttpMethod.Post, Url("fresh-snapshot"), Token));

            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
            backend.Verify(b => b.UpdateSnapshotAsync(AccountId, DoorbotId, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Battery_CorrectToken_ReturnsBatteryFieldsWithNullsForMissing()
        {
            var backend = new Mock<ISnapshotProbeBackend>();
            _ = backend.Setup(b => b.GetBatteryAsync(AccountId, DoorbotId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new SnapshotProbeBattery(BatteryLife: 88, BatteryVoltage: null));

            await using WebApplication app = await BuildAsync(EnabledConfig(), backend.Object);
            using HttpClient client = app.GetTestClient();
            using HttpResponseMessage response = await client.SendAsync(Request(HttpMethod.Get, Url("battery"), Token));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using JsonDocument json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            JsonElement root = json.RootElement;
            Assert.Equal(88, root.GetProperty("battery_life").GetInt32());
            Assert.Equal(JsonValueKind.Null, root.GetProperty("battery_voltage").ValueKind);
            Assert.Equal(JsonValueKind.Null, root.GetProperty("batteries").ValueKind);
            Assert.False(root.TryGetProperty("description", out _));
        }

        [Fact]
        public async Task SessionSnapshotProbeBackend_NoSession_ThrowsUnavailable()
        {
            var sessions = new Mock<ISessionProvider>();
            _ = sessions.Setup(s => s.GetSession(AccountId)).Returns((VideoForensics.Providers.Ring.Session?)null);
            var backend = new SessionSnapshotProbeBackend(sessions.Object);

            _ = await Assert.ThrowsAsync<SnapshotProbeSessionUnavailableException>(
                () => backend.GetLatestSnapshotAsync(AccountId, DoorbotId, CancellationToken.None));
        }

        [Fact]
        public void SessionSnapshotProbeBackend_DoorbotWithBatteryLife_MapsLifeAndLeavesVoltageNull()
        {
            var devices = new Devices
            {
                Doorbots = new List<Doorbot> { new Doorbot { Id = (int)DoorbotId, BatteryLife = 77 } },
                AuthorizedDoorbots = new List<Doorbot>(),
                StickupCams = new List<StickupCam>(),
                Chimes = new List<Chime>(),
            };

            SnapshotProbeBattery battery = SessionSnapshotProbeBackend.FindBattery(devices, DoorbotId);

            Assert.Equal(77, battery.BatteryLife);
            Assert.Null(battery.BatteryVoltage);
        }

        [Fact]
        public void SessionSnapshotProbeBackend_StickupCamWithVoltage_MapsLifeAndVoltage()
        {
            var devices = new Devices
            {
                Doorbots = new List<Doorbot>(),
                AuthorizedDoorbots = new List<Doorbot>(),
                StickupCams = new List<StickupCam>
                {
                    new StickupCam { Id = DoorbotId, BatteryLife = 65, BatteryVoltage = 12.5m },
                },
                Chimes = new List<Chime>(),
            };

            SnapshotProbeBattery battery = SessionSnapshotProbeBackend.FindBattery(devices, DoorbotId);

            Assert.Equal(65, battery.BatteryLife);
            Assert.Equal(12.5m, battery.BatteryVoltage);
        }

        [Fact]
        public void SessionSnapshotProbeBackend_UnknownDoorbot_ReturnsAllNulls()
        {
            var devices = new Devices
            {
                Doorbots = new List<Doorbot>(),
                AuthorizedDoorbots = new List<Doorbot>(),
                StickupCams = new List<StickupCam>(),
                Chimes = new List<Chime>(),
            };

            SnapshotProbeBattery battery = SessionSnapshotProbeBackend.FindBattery(devices, DoorbotId);

            Assert.Null(battery.BatteryLife);
            Assert.Null(battery.BatteryVoltage);
        }
    }
}
