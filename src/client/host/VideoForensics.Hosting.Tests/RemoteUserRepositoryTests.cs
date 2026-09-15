using System.Net;
using System.Text.Json;

using VideoForensics.Api.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Hosting.Remote;

using Xunit;
namespace VideoForensics.Hosting.Tests
{
    public class RemoteUserRepositoryTests
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
        public async Task GetAsync_WithValidUserId_ReturnsUser()
        {
            var userId = Guid.NewGuid();
            var dto = new UserDto(userId, "provider-key", "Test User", "test@example.com", DateTime.UtcNow.AddDays(-30));
            string json = JsonSerializer.Serialize(dto, JsonOptions);
            var handler = new FakeHttpMessageHandler(async _ => { await Task.Yield(); return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json") }; });
            var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test") };
            var repo = new RemoteUserRepository(httpClient);
            User? result = await repo.GetAsync(userId, CancellationToken.None);
            Assert.NotNull(handler.CapturedRequest);
            Assert.Equal(HttpMethod.Get, handler.CapturedRequest.Method);
            Assert.NotNull(result);
            Assert.Equal(userId, result.Id);
        }
        [Fact]
        public async Task GetAsync_WithNotFoundResponse_ReturnsNull()
        {
            var handler = new FakeHttpMessageHandler(async _ => { await Task.Yield(); return new HttpResponseMessage(HttpStatusCode.NotFound); });
            var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test") };
            var repo = new RemoteUserRepository(httpClient);
            User? result = await repo.GetAsync(Guid.NewGuid(), CancellationToken.None);
            Assert.Null(result);
        }
        [Fact]
        public async Task ListAsync_CallsListEndpoint_ReturnsAllUsers()
        {
            var userDto = new UserDto(Guid.NewGuid(), "key", "User1", "user@example.com", DateTime.UtcNow.AddDays(-10));
            var dtoList = new List<UserDto> { userDto };
            string json = JsonSerializer.Serialize(dtoList, JsonOptions);
            var handler = new FakeHttpMessageHandler(async _ => { await Task.Yield(); return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json") }; });
            var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test") };
            var repo = new RemoteUserRepository(httpClient);
            IReadOnlyList<User> result = await repo.ListAsync(CancellationToken.None);
            Assert.NotNull(handler.CapturedRequest);
            Assert.Equal(HttpMethod.Get, handler.CapturedRequest.Method);
            _ = Assert.Single(result);
        }
        [Fact]
        public async Task AddAsync_WithUser_PostsToEndpoint()
        {
            var user = new User { Id = Guid.NewGuid(), ProviderUserKey = "key", DisplayName = "New User", Email = "new@example.com", CreatedUtc = DateTime.UtcNow };
            var handler = new FakeHttpMessageHandler(async _ => { await Task.Yield(); return new HttpResponseMessage(HttpStatusCode.Created); });
            var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test") };
            var repo = new RemoteUserRepository(httpClient);
            await repo.AddAsync(user, CancellationToken.None);
            Assert.NotNull(handler.CapturedRequest);
            Assert.Equal(HttpMethod.Post, handler.CapturedRequest.Method);
        }
    }
}
