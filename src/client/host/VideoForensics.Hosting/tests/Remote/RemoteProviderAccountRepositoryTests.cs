using System.Net;
using System.Text.Json;

using VideoForensics.Core.Api.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Client.Hosting.Remote;
namespace VideoForensics.Client.Hosting.Tests.Remote
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
            var dto = new ProviderAccountDto(accountId, Guid.NewGuid(), "Ring", DateTime.UtcNow.AddDays(-30), DateTime.UtcNow.AddHours(-1), true, DateTime.UtcNow.AddHours(-2), null, null);
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
            var dtoList = new List<ProviderAccountDto> { new(Guid.NewGuid(), Guid.NewGuid(), "Ring", DateTime.UtcNow.AddDays(-30), DateTime.UtcNow.AddHours(-1), true, DateTime.UtcNow.AddHours(-2), null, null) };
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
        public async Task RemoteProviderAccountRepository_RetireAsync_PostsToRetireRoute()
        {
            var accountId = Guid.NewGuid();
            var handler = new FakeHttpMessageHandler(async _ => { await Task.Yield(); return new HttpResponseMessage(HttpStatusCode.OK); });
            var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test") };
            var repo = new RemoteProviderAccountRepository(httpClient);
            await repo.RetireAsync(accountId, DateTime.UtcNow, CancellationToken.None);
            Assert.Equal(HttpMethod.Post, handler.CapturedRequest.Method);
            Assert.Equal($"/api/v1/accounts/provider-accounts/{accountId}/retire", handler.CapturedRequest.RequestUri!.AbsolutePath);
        }
        [Fact]
        public async Task RemoteProviderAccountRepository_RetireAsync_SendsStepUpTokenHeader()
        {
            var accountId = Guid.NewGuid();
            var handler = new FakeHttpMessageHandler(async _ => { await Task.Yield(); return new HttpResponseMessage(HttpStatusCode.OK); });
            var repo = new RemoteProviderAccountRepository(new HttpClient(handler) { BaseAddress = new Uri("https://example.test") });
            await repo.RetireAsync(accountId, DateTime.UtcNow, CancellationToken.None, "step-token");
            Assert.True(handler.CapturedRequest.Headers.TryGetValues("X-StepUp-Token", out IEnumerable<string>? values));
            Assert.Equal("step-token", Assert.Single(values!));
        }
        [Fact]
        public async Task RemoteProviderAccountRepository_RetireAsync_NoToken_OmitsHeader()
        {
            var handler = new FakeHttpMessageHandler(async _ => { await Task.Yield(); return new HttpResponseMessage(HttpStatusCode.OK); });
            var repo = new RemoteProviderAccountRepository(new HttpClient(handler) { BaseAddress = new Uri("https://example.test") });
            await repo.RetireAsync(Guid.NewGuid(), DateTime.UtcNow, CancellationToken.None);
            Assert.False(handler.CapturedRequest.Headers.Contains("X-StepUp-Token"));
        }
        [Fact]
        public async Task RemoteProviderAccountRepository_DeleteAsync_SendsStepUpTokenHeader()
        {
            var accountId = Guid.NewGuid();
            var handler = new FakeHttpMessageHandler(async _ => { await Task.Yield(); return new HttpResponseMessage(HttpStatusCode.NoContent); });
            var repo = new RemoteProviderAccountRepository(new HttpClient(handler) { BaseAddress = new Uri("https://example.test") });
            await repo.DeleteAsync(accountId, CancellationToken.None, "step-token");
            Assert.Equal(HttpMethod.Delete, handler.CapturedRequest.Method);
            Assert.True(handler.CapturedRequest.Headers.TryGetValues("X-StepUp-Token", out IEnumerable<string>? values));
            Assert.Equal("step-token", Assert.Single(values!));
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
