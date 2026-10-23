using System.Net;
using System.Text;

using VideoForensics.Hosting;

using Xunit;

namespace VideoForensics.Hosting.Tests
{
    public class CloudflareApiClientTests
    {
        private const string Token = "secret-api-token";

        private sealed class ScriptedHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;

            public ScriptedHandler(Func<HttpRequestMessage, HttpResponseMessage> respond)
            {
                _respond = respond;
            }

            public List<HttpRequestMessage> Requests { get; } = new();

            // The client disposes each request after sending it, so bodies are captured here.
            public List<string?> Bodies { get; } = new();

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Requests.Add(request);
                Bodies.Add(request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken));
                return _respond(request);
            }
        }

        private static HttpResponseMessage Json(HttpStatusCode status, string body)
            => new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

        private static (CloudflareApiClient Client, ScriptedHandler Handler) Create(Func<HttpRequestMessage, HttpResponseMessage> respond)
        {
            var handler = new ScriptedHandler(respond);
            var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.cloudflare.com/") };
            return (new CloudflareApiClient(http), handler);
        }

        [Fact]
        public async Task ValidateTokenAndListZonesAsync_SuccessfulResponse_ReturnsZonesWithAccounts()
        {
            var (client, handler) = Create(_ => Json(HttpStatusCode.OK,
                """{"success":true,"result":[{"id":"z2","name":"b.example.com","account":{"id":"acct"}},{"id":"z1","name":"a.example.com","account":{"id":"acct"}}]}"""));

            CloudflareZoneResult result = await client.ValidateTokenAndListZonesAsync(Token, CancellationToken.None);

            Assert.True(result.IsValid);
            Assert.Equal(new[] { "a.example.com", "b.example.com" }, result.Zones.Select(z => z.Name));
            Assert.Equal("acct", result.Zones[0].AccountId);
            Assert.Equal("Bearer", handler.Requests[0].Headers.Authorization?.Scheme);
            Assert.Equal(Token, handler.Requests[0].Headers.Authorization?.Parameter);
        }

        [Fact]
        public async Task ValidateTokenAndListZonesAsync_InvalidToken_ReturnsCloudflareErrorMessage()
        {
            var (client, _) = Create(_ => Json(HttpStatusCode.Forbidden,
                """{"success":false,"errors":[{"message":"Invalid API Token"}]}"""));

            CloudflareZoneResult result = await client.ValidateTokenAndListZonesAsync(Token, CancellationToken.None);

            Assert.False(result.IsValid);
            Assert.Equal("Invalid API Token", result.ErrorMessage);
            Assert.Empty(result.Zones);
        }

        [Fact]
        public async Task ValidateTokenAndListZonesAsync_ApiErrorWithoutMessage_ReturnsStatusCode()
        {
            var (client, _) = Create(_ => Json(HttpStatusCode.InternalServerError, """{"success":false}"""));

            CloudflareZoneResult result = await client.ValidateTokenAndListZonesAsync(Token, CancellationToken.None);

            Assert.False(result.IsValid);
            Assert.Contains("500", result.ErrorMessage);
        }

        [Fact]
        public async Task ValidateTokenAndListZonesAsync_MalformedZones_DropsEntriesWithoutIdOrName()
        {
            var (client, _) = Create(_ => Json(HttpStatusCode.OK,
                """{"success":true,"result":[{"id":"z1"},{"name":"no-id.example.com"},{"id":"z3","name":"ok.example.com"}]}"""));

            CloudflareZoneResult result = await client.ValidateTokenAndListZonesAsync(Token, CancellationToken.None);

            Assert.True(result.IsValid);
            Assert.Equal(new[] { "ok.example.com" }, result.Zones.Select(z => z.Name));
        }

        [Fact]
        public async Task ValidateTokenAndListZonesAsync_UnreadableBody_ReturnsErrorInsteadOfThrowing()
        {
            var (client, _) = Create(_ => Json(HttpStatusCode.OK, "<html>maintenance</html>"));

            CloudflareZoneResult result = await client.ValidateTokenAndListZonesAsync(Token, CancellationToken.None);

            Assert.False(result.IsValid);
            Assert.Equal("Cloudflare returned an unreadable response.", result.ErrorMessage);
        }

        [Fact]
        public async Task ValidateTokenAndListZonesAsync_EmptyToken_DoesNotCallApi()
        {
            var (client, handler) = Create(_ => throw new InvalidOperationException("should not be called"));

            CloudflareZoneResult result = await client.ValidateTokenAndListZonesAsync(" ", CancellationToken.None);

            Assert.False(result.IsValid);
            Assert.Empty(handler.Requests);
        }

        [Fact]
        public async Task CreateTunnelAsync_Success_ReturnsIdAndPermanentToken()
        {
            var (client, handler) = Create(request => request.Method == HttpMethod.Post
                ? Json(HttpStatusCode.OK, """{"success":true,"result":{"id":"tun-1"}}""")
                : Json(HttpStatusCode.OK, """{"success":true,"result":"permanent-token"}"""));

            CloudflareTunnelResult result = await client.CreateTunnelAsync(Token, "acct", "videoforensics-video", CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Equal("tun-1", result.TunnelId);
            Assert.Equal("permanent-token", result.TunnelToken);
            Assert.EndsWith("/accounts/acct/cfd_tunnel", handler.Requests[0].RequestUri!.AbsolutePath);
            Assert.EndsWith("/accounts/acct/cfd_tunnel/tun-1/token", handler.Requests[1].RequestUri!.AbsolutePath);
        }

        [Fact]
        public async Task CreateTunnelAsync_CreateFails_ReturnsErrorAndNoToken()
        {
            var (client, handler) = Create(_ => Json(HttpStatusCode.BadRequest, """{"success":false,"errors":[{"message":"name taken"}]}"""));

            CloudflareTunnelResult result = await client.CreateTunnelAsync(Token, "acct", "dup", CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Equal("name taken", result.ErrorMessage);
            Assert.Null(result.TunnelToken);
            Assert.Single(handler.Requests);
        }

        [Fact]
        public async Task ConfigureIngressAsync_SendsHostnameRuleAndCatchAll()
        {
            var (client, handler) = Create(_ => Json(HttpStatusCode.OK, """{"success":true,"result":{}}"""));

            CloudflareOperationResult result = await client.ConfigureIngressAsync(Token, "acct", "tun-1", "video.example.com", "http://localhost:5162", CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Equal(HttpMethod.Put, handler.Requests[0].Method);
            string body = handler.Bodies[0]!;
            Assert.Contains("\"hostname\":\"video.example.com\"", body);
            Assert.Contains("\"service\":\"http://localhost:5162\"", body);
            Assert.Contains("http_status:404", body);
        }

        [Fact]
        public async Task UpsertTunnelDnsRecordAsync_NoExistingRecord_CreatesProxiedCname()
        {
            var (client, handler) = Create(request => request.Method == HttpMethod.Get
                ? Json(HttpStatusCode.OK, """{"success":true,"result":[]}""")
                : Json(HttpStatusCode.OK, """{"success":true,"result":{}}"""));

            CloudflareOperationResult result = await client.UpsertTunnelDnsRecordAsync(Token, "zone-1", "video.example.com", "tun-1", CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Equal(HttpMethod.Post, handler.Requests[1].Method);
            string body = handler.Bodies[1]!;
            Assert.Contains("\"content\":\"tun-1.cfargotunnel.com\"", body);
            Assert.Contains("\"proxied\":true", body);
        }

        [Fact]
        public async Task UpsertTunnelDnsRecordAsync_ExistingRecord_UpdatesInPlace()
        {
            var (client, handler) = Create(request => request.Method == HttpMethod.Get
                ? Json(HttpStatusCode.OK, """{"success":true,"result":[{"id":"rec-9"}]}""")
                : Json(HttpStatusCode.OK, """{"success":true,"result":{}}"""));

            CloudflareOperationResult result = await client.UpsertTunnelDnsRecordAsync(Token, "zone-1", "video.example.com", "tun-1", CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Equal(HttpMethod.Put, handler.Requests[1].Method);
            Assert.EndsWith("/dns_records/rec-9", handler.Requests[1].RequestUri!.AbsolutePath);
        }

        [Fact]
        public async Task CreateTxtRecordAsync_Success_ReturnsRecordIdAndSendsTxtType()
        {
            var (client, handler) = Create(_ => Json(HttpStatusCode.OK, """{"success":true,"result":{"id":"txt-1"}}"""));

            CloudflareRecordResult result = await client.CreateTxtRecordAsync(Token, "zone-1", "_acme-challenge.video.example.com", "challenge-value", CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Equal("txt-1", result.RecordId);
            Assert.Equal(HttpMethod.Post, handler.Requests[0].Method);
            Assert.Contains("\"type\":\"TXT\"", handler.Bodies[0]);
            Assert.Contains("\"content\":\"challenge-value\"", handler.Bodies[0]);
        }

        [Fact]
        public async Task CreateTxtRecordAsync_ApiError_ReturnsErrorWithoutRecordId()
        {
            var (client, _) = Create(_ => Json(HttpStatusCode.BadRequest, """{"success":false,"errors":[{"message":"record exists"}]}"""));

            CloudflareRecordResult result = await client.CreateTxtRecordAsync(Token, "zone-1", "_acme-challenge.x.example.com", "v", CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Null(result.RecordId);
            Assert.Equal("record exists", result.ErrorMessage);
        }

        [Fact]
        public async Task DeleteDnsRecordAsync_Success_SendsDeleteForRecord()
        {
            var (client, handler) = Create(_ => Json(HttpStatusCode.OK, """{"success":true,"result":{"id":"txt-1"}}"""));

            CloudflareOperationResult result = await client.DeleteDnsRecordAsync(Token, "zone-1", "txt-1", CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Equal(HttpMethod.Delete, handler.Requests[0].Method);
            Assert.EndsWith("/zones/zone-1/dns_records/txt-1", handler.Requests[0].RequestUri!.AbsolutePath);
        }
    }
}
