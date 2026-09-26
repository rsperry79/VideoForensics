using System.Net;
using System.Text.Json;

using VideoForensics.Api.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Hosting.Remote;

using Xunit;

namespace VideoForensics.Hosting.Tests
{
    public class RemoteCaseRepositoryTests
    {
        private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

        private static ForensicCaseDto CreateForensicCaseDto(Guid? id = null)
        {
            return new ForensicCaseDto(
                Id: id ?? Guid.NewGuid(),
                CaseNumber: "CASE-001",
                Title: "Test Case",
                Description: "A test forensic case",
                Status: "Open",
                LeadOperatorId: Guid.NewGuid(),
                CreatedBy: "admin",
                CreatedAtUtc: DateTime.UtcNow.AddDays(-1),
                UpdatedAtUtc: DateTime.UtcNow,
                ClosedBy: null,
                ClosedAtUtc: null,
                ScopeFromUtc: DateTime.UtcNow.AddDays(-7),
                ScopeToUtc: DateTime.UtcNow,
                DeviceIds: new List<Guid> { Guid.NewGuid() }
            );
        }

        private static CaseItemDto CreateCaseItemDto(Guid? id = null, Guid? caseId = null)
        {
            return new CaseItemDto(
                Id: id ?? Guid.NewGuid(),
                CaseId: caseId ?? Guid.NewGuid(),
                Kind: "Media",
                TargetId: Guid.NewGuid(),
                Reason: "Relevant to investigation",
                AddedBy: "admin",
                AddedAtUtc: DateTime.UtcNow,
                MediaSha256AtAdd: "abc123",
                RemovedBy: null,
                RemovedAtUtc: null,
                RemovalReason: null
            );
        }

        private sealed class FakeHttpMessageHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _responseFactory;
            public HttpRequestMessage? CapturedRequest { get; private set; }

            public FakeHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> responseFactory)
            {
                _responseFactory = responseFactory;
            }

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                CapturedRequest = request;
                return await _responseFactory(request);
            }
        }

        private static HttpClient CreateHttpClientWithHandler(FakeHttpMessageHandler handler)
        {
            return new HttpClient(handler) { BaseAddress = new Uri("https://example.test") };
        }

        #region CreateAsync Tests

        [Fact]
        public async Task CreateAsync_ReturnsCreatedCase()
        {
            var caseDto = CreateForensicCaseDto();
            string json = JsonSerializer.Serialize(caseDto, JsonOptions);

            FakeHttpMessageHandler handler = new(async req =>
            {
                await Task.Yield();
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
                };
            });

            HttpClient httpClient = CreateHttpClientWithHandler(handler);
            var repo = new RemoteCaseRepository(httpClient);

            ForensicCase result = await repo.CreateAsync(
                caseDto.CaseNumber, caseDto.Title, caseDto.Description,
                caseDto.LeadOperatorId, caseDto.ScopeFromUtc, caseDto.ScopeToUtc,
                caseDto.DeviceIds, "admin", CancellationToken.None);

            Assert.NotNull(handler.CapturedRequest);
            Assert.Equal(HttpMethod.Post, handler.CapturedRequest.Method);
            Assert.Equal("/api/v1/cases", handler.CapturedRequest.RequestUri?.PathAndQuery);
            Assert.Equal(caseDto.Id, result.Id);
            Assert.Equal(caseDto.CaseNumber, result.CaseNumber);
        }

        [Fact]
        public async Task CreateAsync_WithCancellationToken_PassesToHttpClient()
        {
            var cts = new CancellationTokenSource();
            var caseDto = CreateForensicCaseDto();
            string json = JsonSerializer.Serialize(caseDto, JsonOptions);

            FakeHttpMessageHandler handler = new(async req =>
            {
                await Task.Yield();
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
                };
            });

            HttpClient httpClient = CreateHttpClientWithHandler(handler);
            var repo = new RemoteCaseRepository(httpClient);

            await repo.CreateAsync(
                caseDto.CaseNumber, caseDto.Title, caseDto.Description,
                caseDto.LeadOperatorId, caseDto.ScopeFromUtc, caseDto.ScopeToUtc,
                caseDto.DeviceIds, "admin", cts.Token);

            Assert.NotNull(handler.CapturedRequest);
        }

        #endregion

        #region GetAsync Tests

        [Fact]
        public async Task GetAsync_Found_ReturnsCase()
        {
            var caseDto = CreateForensicCaseDto();
            string json = JsonSerializer.Serialize(caseDto, JsonOptions);
            var id = caseDto.Id;

            FakeHttpMessageHandler handler = new(async req =>
            {
                await Task.Yield();
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
                };
            });

            HttpClient httpClient = CreateHttpClientWithHandler(handler);
            var repo = new RemoteCaseRepository(httpClient);

            ForensicCase? result = await repo.GetAsync(id, CancellationToken.None);

            Assert.NotNull(handler.CapturedRequest);
            Assert.Equal(HttpMethod.Get, handler.CapturedRequest.Method);
            Assert.Equal($"/api/v1/cases/{id}", handler.CapturedRequest.RequestUri?.PathAndQuery);
            Assert.NotNull(result);
            Assert.Equal(id, result.Id);
        }

        [Fact]
        public async Task GetAsync_NotFound_ReturnsNull()
        {
            var id = Guid.NewGuid();

            FakeHttpMessageHandler handler = new(async req =>
            {
                await Task.Yield();
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            });

            HttpClient httpClient = CreateHttpClientWithHandler(handler);
            var repo = new RemoteCaseRepository(httpClient);

            ForensicCase? result = await repo.GetAsync(id, CancellationToken.None);

            Assert.Null(result);
        }

        #endregion

        #region GetByNumberAsync Tests

        [Fact]
        public async Task GetByNumberAsync_Found_ReturnsCase()
        {
            var caseDto = CreateForensicCaseDto();
            string json = JsonSerializer.Serialize(caseDto, JsonOptions);

            FakeHttpMessageHandler handler = new(async req =>
            {
                await Task.Yield();
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
                };
            });

            HttpClient httpClient = CreateHttpClientWithHandler(handler);
            var repo = new RemoteCaseRepository(httpClient);

            ForensicCase? result = await repo.GetByNumberAsync(caseDto.CaseNumber, CancellationToken.None);

            Assert.NotNull(handler.CapturedRequest);
            Assert.Equal(HttpMethod.Get, handler.CapturedRequest.Method);
            Assert.Contains($"/api/v1/cases/by-number/", handler.CapturedRequest.RequestUri?.PathAndQuery);
            Assert.NotNull(result);
            Assert.Equal(caseDto.CaseNumber, result.CaseNumber);
        }

        [Fact]
        public async Task GetByNumberAsync_NotFound_ReturnsNull()
        {
            FakeHttpMessageHandler handler = new(async req =>
            {
                await Task.Yield();
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            });

            HttpClient httpClient = CreateHttpClientWithHandler(handler);
            var repo = new RemoteCaseRepository(httpClient);

            ForensicCase? result = await repo.GetByNumberAsync("INVALID", CancellationToken.None);

            Assert.Null(result);
        }

        #endregion

        #region ListAsync Tests

        [Fact]
        public async Task ListAsync_WithoutStatus_ReturnsAllCases()
        {
            var case1 = CreateForensicCaseDto();
            var case2 = CreateForensicCaseDto();
            var dtoList = new List<ForensicCaseDto> { case1, case2 };
            string json = JsonSerializer.Serialize(dtoList, JsonOptions);

            FakeHttpMessageHandler handler = new(async req =>
            {
                await Task.Yield();
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
                };
            });

            HttpClient httpClient = CreateHttpClientWithHandler(handler);
            var repo = new RemoteCaseRepository(httpClient);

            IReadOnlyList<ForensicCase> result = await repo.ListAsync(null, CancellationToken.None);

            Assert.NotNull(handler.CapturedRequest);
            Assert.Equal(HttpMethod.Get, handler.CapturedRequest.Method);
            Assert.Equal("/api/v1/cases", handler.CapturedRequest.RequestUri?.PathAndQuery);
            Assert.Equal(2, result.Count);
        }

        [Fact]
        public async Task ListAsync_WithStatus_FiltersByStatus()
        {
            var case1 = CreateForensicCaseDto();
            var dtoList = new List<ForensicCaseDto> { case1 };
            string json = JsonSerializer.Serialize(dtoList, JsonOptions);

            FakeHttpMessageHandler handler = new(async req =>
            {
                await Task.Yield();
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
                };
            });

            HttpClient httpClient = CreateHttpClientWithHandler(handler);
            var repo = new RemoteCaseRepository(httpClient);

            IReadOnlyList<ForensicCase> result = await repo.ListAsync(CaseStatus.Open, CancellationToken.None);

            Assert.NotNull(handler.CapturedRequest);
            Assert.Contains("status=Open", handler.CapturedRequest.RequestUri?.PathAndQuery);
            Assert.Single(result);
        }

        #endregion

        #region UpdateDetailsAsync Tests

        [Fact]
        public async Task UpdateDetailsAsync_Success_CallsUpdateEndpoint()
        {
            var id = Guid.NewGuid();

            FakeHttpMessageHandler handler = new(async req =>
            {
                await Task.Yield();
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            });

            HttpClient httpClient = CreateHttpClientWithHandler(handler);
            var repo = new RemoteCaseRepository(httpClient);

            await repo.UpdateDetailsAsync(id, "New Title", "New Description", null, "admin", CancellationToken.None);

            Assert.NotNull(handler.CapturedRequest);
            Assert.Equal(HttpMethod.Put, handler.CapturedRequest.Method);
            Assert.Equal($"/api/v1/cases/{id}", handler.CapturedRequest.RequestUri?.PathAndQuery);
        }

        [Fact]
        public async Task UpdateDetailsAsync_Conflict_ThrowsInvalidOperationException()
        {
            var id = Guid.NewGuid();

            FakeHttpMessageHandler handler = new(async req =>
            {
                await Task.Yield();
                return new HttpResponseMessage(HttpStatusCode.Conflict)
                {
                    Content = new StringContent("Case is closed", System.Text.Encoding.UTF8, "text/plain")
                };
            });

            HttpClient httpClient = CreateHttpClientWithHandler(handler);
            var repo = new RemoteCaseRepository(httpClient);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => repo.UpdateDetailsAsync(id, "New Title", null, null, "admin", CancellationToken.None));
        }

        #endregion

        #region SetScopeAsync Tests

        [Fact]
        public async Task SetScopeAsync_Success_CallsSetScopeEndpoint()
        {
            var id = Guid.NewGuid();
            var deviceIds = new List<Guid> { Guid.NewGuid() };

            FakeHttpMessageHandler handler = new(async req =>
            {
                await Task.Yield();
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            });

            HttpClient httpClient = CreateHttpClientWithHandler(handler);
            var repo = new RemoteCaseRepository(httpClient);

            await repo.SetScopeAsync(id, DateTime.UtcNow, DateTime.UtcNow.AddDays(1), deviceIds, "admin", CancellationToken.None);

            Assert.NotNull(handler.CapturedRequest);
            Assert.Equal(HttpMethod.Put, handler.CapturedRequest.Method);
            Assert.Equal($"/api/v1/cases/{id}/scope", handler.CapturedRequest.RequestUri?.PathAndQuery);
        }

        #endregion

        #region GetDeviceIdsAsync Tests

        [Fact]
        public async Task GetDeviceIdsAsync_ReturnsDeviceIds()
        {
            var caseId = Guid.NewGuid();
            var deviceIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };
            string json = JsonSerializer.Serialize(deviceIds, JsonOptions);

            FakeHttpMessageHandler handler = new(async req =>
            {
                await Task.Yield();
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
                };
            });

            HttpClient httpClient = CreateHttpClientWithHandler(handler);
            var repo = new RemoteCaseRepository(httpClient);

            IReadOnlyList<Guid> result = await repo.GetDeviceIdsAsync(caseId, CancellationToken.None);

            Assert.NotNull(handler.CapturedRequest);
            Assert.Equal(HttpMethod.Get, handler.CapturedRequest.Method);
            Assert.Equal($"/api/v1/cases/{caseId}/devices", handler.CapturedRequest.RequestUri?.PathAndQuery);
            Assert.Equal(2, result.Count);
        }

        #endregion

        #region CloseAsync Tests

        [Fact]
        public async Task CloseAsync_Success_CallsCloseEndpoint()
        {
            var id = Guid.NewGuid();

            FakeHttpMessageHandler handler = new(async req =>
            {
                await Task.Yield();
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            });

            HttpClient httpClient = CreateHttpClientWithHandler(handler);
            var repo = new RemoteCaseRepository(httpClient);

            await repo.CloseAsync(id, "admin", CancellationToken.None);

            Assert.NotNull(handler.CapturedRequest);
            Assert.Equal(HttpMethod.Post, handler.CapturedRequest.Method);
            Assert.Equal($"/api/v1/cases/{id}/close", handler.CapturedRequest.RequestUri?.PathAndQuery);
        }

        #endregion

        #region ReopenAsync Tests

        [Fact]
        public async Task ReopenAsync_Success_CallsReopenEndpoint()
        {
            var id = Guid.NewGuid();

            FakeHttpMessageHandler handler = new(async req =>
            {
                await Task.Yield();
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            });

            HttpClient httpClient = CreateHttpClientWithHandler(handler);
            var repo = new RemoteCaseRepository(httpClient);

            await repo.ReopenAsync(id, "admin", CancellationToken.None);

            Assert.NotNull(handler.CapturedRequest);
            Assert.Equal(HttpMethod.Post, handler.CapturedRequest.Method);
            Assert.Equal($"/api/v1/cases/{id}/reopen", handler.CapturedRequest.RequestUri?.PathAndQuery);
        }

        #endregion

        #region AddItemAsync Tests

        [Fact]
        public async Task AddItemAsync_Success_ReturnsCreatedItem()
        {
            var caseId = Guid.NewGuid();
            var itemDto = CreateCaseItemDto(caseId: caseId);
            string json = JsonSerializer.Serialize(itemDto, JsonOptions);

            FakeHttpMessageHandler handler = new(async req =>
            {
                await Task.Yield();
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
                };
            });

            HttpClient httpClient = CreateHttpClientWithHandler(handler);
            var repo = new RemoteCaseRepository(httpClient);

            CaseItem result = await repo.AddItemAsync(caseId, CaseItemKind.Media, itemDto.TargetId, "Relevant", "admin", CancellationToken.None);

            Assert.NotNull(handler.CapturedRequest);
            Assert.Equal(HttpMethod.Post, handler.CapturedRequest.Method);
            Assert.Equal($"/api/v1/cases/{caseId}/items", handler.CapturedRequest.RequestUri?.PathAndQuery);
            Assert.Equal(itemDto.Id, result.Id);
        }

        [Fact]
        public async Task AddItemAsync_Conflict_ThrowsInvalidOperationException()
        {
            var caseId = Guid.NewGuid();

            FakeHttpMessageHandler handler = new(async req =>
            {
                await Task.Yield();
                return new HttpResponseMessage(HttpStatusCode.Conflict)
                {
                    Content = new StringContent("Item already pinned to case", System.Text.Encoding.UTF8, "text/plain")
                };
            });

            HttpClient httpClient = CreateHttpClientWithHandler(handler);
            var repo = new RemoteCaseRepository(httpClient);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => repo.AddItemAsync(caseId, CaseItemKind.Media, Guid.NewGuid(), "Relevant", "admin", CancellationToken.None));
        }

        #endregion

        #region RemoveItemAsync Tests

        [Fact]
        public async Task RemoveItemAsync_Success_CallsRemoveEndpoint()
        {
            var itemId = Guid.NewGuid();

            FakeHttpMessageHandler handler = new(async req =>
            {
                await Task.Yield();
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            });

            HttpClient httpClient = CreateHttpClientWithHandler(handler);
            var repo = new RemoteCaseRepository(httpClient);

            await repo.RemoveItemAsync(itemId, "admin", "No longer relevant", CancellationToken.None);

            Assert.NotNull(handler.CapturedRequest);
            Assert.Equal(HttpMethod.Post, handler.CapturedRequest.Method);
            Assert.Equal($"/api/v1/cases/items/{itemId}/remove", handler.CapturedRequest.RequestUri?.PathAndQuery);
        }

        #endregion

        #region ListItemsAsync Tests

        [Fact]
        public async Task ListItemsAsync_WithIncludeRemoved_ReturnsAllItems()
        {
            var caseId = Guid.NewGuid();
            var item1 = CreateCaseItemDto(caseId: caseId);
            var item2 = CreateCaseItemDto(caseId: caseId);
            var dtoList = new List<CaseItemDto> { item1, item2 };
            string json = JsonSerializer.Serialize(dtoList, JsonOptions);

            FakeHttpMessageHandler handler = new(async req =>
            {
                await Task.Yield();
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
                };
            });

            HttpClient httpClient = CreateHttpClientWithHandler(handler);
            var repo = new RemoteCaseRepository(httpClient);

            IReadOnlyList<CaseItem> result = await repo.ListItemsAsync(caseId, true, CancellationToken.None);

            Assert.NotNull(handler.CapturedRequest);
            Assert.Equal(HttpMethod.Get, handler.CapturedRequest.Method);
            Assert.Contains("includeRemoved=True", handler.CapturedRequest.RequestUri?.PathAndQuery);
            Assert.Equal(2, result.Count);
        }

        #endregion

        #region ListCasesContainingAsync Tests

        [Fact]
        public async Task ListCasesContainingAsync_ReturnsCases()
        {
            var case1 = CreateForensicCaseDto();
            var dtoList = new List<ForensicCaseDto> { case1 };
            string json = JsonSerializer.Serialize(dtoList, JsonOptions);
            var targetId = Guid.NewGuid();

            FakeHttpMessageHandler handler = new(async req =>
            {
                await Task.Yield();
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
                };
            });

            HttpClient httpClient = CreateHttpClientWithHandler(handler);
            var repo = new RemoteCaseRepository(httpClient);

            IReadOnlyList<ForensicCase> result = await repo.ListCasesContainingAsync(CaseItemKind.Media, targetId, CancellationToken.None);

            Assert.NotNull(handler.CapturedRequest);
            Assert.Equal(HttpMethod.Get, handler.CapturedRequest.Method);
            Assert.Contains("kind=Media", handler.CapturedRequest.RequestUri?.PathAndQuery);
            Assert.Single(result);
        }

        #endregion
    }
}
