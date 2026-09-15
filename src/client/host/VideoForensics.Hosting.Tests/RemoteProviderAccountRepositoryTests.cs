using System.Net;
using System.Text.Json;

using VideoForensics.Api.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Hosting.Remote;

using Xunit;
namespace VideoForensics.Hosting.Tests
{
    public class RemoteProviderAccountRepositoryTests
    {
        private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
        private sealed class FakeHttpMessageHandler : HttpMessageHandler
        {
            public HttpRequestMessage CapturedRequest { get; private set; }
            private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _factory;
            public FakeHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> factory)
            {
                _factory = factory;
            }

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) { CapturedRequest = request; return await _factory(request); }
        }
        [Fact]
        public async Task GetAsync_WithValidAccountId_ReturnsProviderAccount()
        {
            var accountId = Guid.NewGuid();
            var dto = new ProviderAccountDto(accountId, Guid.NewGuid(), "Ring", DateTime.UtcNow.AddDays(-30), DateTime.UtcNow.AddHours(-1), true, DateTime.UtcNow.AddHours(-2));
            string json = JsonSerializer.Serialize(dto, JsonOptions);
            var handler = new FakeHttpMessageHandler(async _ => { await Task.Yield(); return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json") }; });
            var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test") };
            var repo = new RemoteProviderAccountRepository(httpClient);
            ProviderAccount? result = await repo.GetAsync(accountId, CancellationToken.None);
            Assert.NotNull(handler.CapturedRequest);
            Assert.Equal(HttpMethod.Get, handler.CapturedRequest.Method);
            Assert.NotNull(result);
            Assert.Equal(accountId, result.Id);
        }
        [Fact]
        public async Task GetAsync_WithNotFoundResponse_ReturnsNull()
        {
            var handler = new FakeHttpMessageHandler(async _ => { await Task.Yield(); return new HttpResponseMessage(HttpStatusCode.NotFound); });
            var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test") };
            var repo = new RemoteProviderAccountRepository(httpClient);
            ProviderAccount? result = await repo.GetAsync(Guid.NewGuid(), CancellationToken.None);
            Assert.Null(result);
        }
        [Fact]
        public async Task ListAsync_CallsListEndpoint_ReturnsAllAccounts()
        {
            var dtoList = new List<ProviderAccountDto> { new(Guid.NewGuid(), Guid.NewGuid(), "Ring", DateTime.UtcNow.AddDays(-30), DateTime.UtcNow.AddHours(-1), true, DateTime.UtcNow.AddHours(-2)) };
            string json = JsonSerializer.Serialize(dtoList, JsonOptions);
            var handler = new FakeHttpMessageHandler(async _ => { await Task.Yield(); return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json") }; });
            var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test") };
            var repo = new RemoteProviderAccountRepository(httpClient);
            IReadOnlyList<ProviderAccount> result = await repo.ListAsync(CancellationToken.None);
            Assert.NotNull(handler.CapturedRequest);
            Assert.Equal(HttpMethod.Get, handler.CapturedRequest.Method);
            _ = Assert.Single(result);
        }
        [Fact]
        public async Task AddAsync_WithProviderAccount_PostsToEndpoint()
        {
            var account = new ProviderAccount { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), ProviderName = "Ring", LinkedUtc = DateTime.UtcNow, IsActive = true };
            var handler = new FakeHttpMessageHandler(async _ => { await Task.Yield(); return new HttpResponseMessage(HttpStatusCode.Created); });
            var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test") };
            var repo = new RemoteProviderAccountRepository(httpClient);
            await repo.AddAsync(account, CancellationToken.None);
            Assert.NotNull(handler.CapturedRequest);
            Assert.Equal(HttpMethod.Post, handler.CapturedRequest.Method);
        }
    }
}
