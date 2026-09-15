using System.Net;
using System.Text.Json;
using VideoForensics.Api.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Hosting.Remote;
using Xunit;

namespace VideoForensics.Hosting.Tests;

public class RemoteLegalHoldRepositoryTests
{
    private static class TestData
    {
        public static LegalHoldDto CreateDto(Guid? id = null, Guid? mediaItemId = null, bool isReleased = false)
        {
            return new LegalHoldDto(
                Id: id ?? Guid.NewGuid(),
                MediaItemId: mediaItemId ?? Guid.NewGuid(),
                Reason: "pending investigation",
                CreatedBy: "officer-smith",
                CreatedAtUtc: DateTime.UtcNow.AddDays(-1),
                ReleasedBy: isReleased ? "officer-jones" : null,
                ReleasedAtUtc: isReleased ? DateTime.UtcNow : null,
                ReleaseReason: isReleased ? "investigation concluded" : null
            );
        }

        public static LegalHold CreateDomain(Guid? id = null, Guid? mediaItemId = null, bool isReleased = false)
        {
            return new LegalHold
            {
                Id = id ?? Guid.NewGuid(),
                MediaItemId = mediaItemId ?? Guid.NewGuid(),
                Reason = "pending investigation",
                CreatedBy = "officer-smith",
                CreatedAtUtc = DateTime.UtcNow.AddDays(-1),
                ReleasedBy = isReleased ? "officer-jones" : null,
                ReleasedAtUtc = isReleased ? DateTime.UtcNow : null,
                ReleaseReason = isReleased ? "investigation concluded" : null
            };
        }
    }

    private class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _handleRequest;

        public MockHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handleRequest)
        {
            _handleRequest = handleRequest;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return _handleRequest(request);
        }
    }

    [Fact]
    public async Task PlaceAsync_WithValidMediaItemAndReason_CallsCorrectEndpointAndReturnsMappedLegalHold()
    {
        var mediaItemId = Guid.NewGuid();
        var reason = "pending investigation";
        var createdBy = "officer-smith";
        var responseDto = TestData.CreateDto(mediaItemId: mediaItemId);
        var jsonContent = JsonSerializer.Serialize(responseDto);
        HttpRequestMessage? capturedRequest = null;

        var handler = new MockHttpMessageHandler(async request =>
        {
            capturedRequest = request;
            return new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json")
            };
        });

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test") };
        var repo = new RemoteLegalHoldRepository(httpClient);

        var result = await repo.PlaceAsync(mediaItemId, reason, createdBy, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(mediaItemId, result.MediaItemId);
        Assert.Equal(reason, result.Reason);
        Assert.NotNull(capturedRequest);
        Assert.Equal(HttpMethod.Post, capturedRequest.Method);
        Assert.Equal("/api/v1/legal-holds/", capturedRequest.RequestUri?.PathAndQuery);
    }

    [Fact]
    public async Task PlaceAsync_WithServerError_Throws()
    {
        var mediaItemId = Guid.NewGuid();

        var handler = new MockHttpMessageHandler(async request =>
        {
            return new HttpResponseMessage { StatusCode = HttpStatusCode.InternalServerError };
        });

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test") };
        var repo = new RemoteLegalHoldRepository(httpClient);

        await Assert.ThrowsAsync<HttpRequestException>(() => repo.PlaceAsync(mediaItemId, "reason", "officer", CancellationToken.None));
    }

    [Fact]
    public async Task PlaceAsync_WithNullResponse_Throws()
    {
        var mediaItemId = Guid.NewGuid();
        var jsonContent = JsonSerializer.Serialize((LegalHoldDto?)null);

        var handler = new MockHttpMessageHandler(async request =>
        {
            return new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json")
            };
        });

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test") };
        var repo = new RemoteLegalHoldRepository(httpClient);

        await Assert.ThrowsAsync<InvalidOperationException>(() => repo.PlaceAsync(mediaItemId, "reason", "officer", CancellationToken.None));
    }

    [Fact]
    public async Task ReleaseAsync_WithValidLegalHoldId_CallsCorrectEndpoint()
    {
        var legalHoldId = Guid.NewGuid();
        var releaseReason = "investigation concluded";
        var releasedBy = "officer-jones";
        HttpRequestMessage? capturedRequest = null;

        var handler = new MockHttpMessageHandler(async request =>
        {
            capturedRequest = request;
            return new HttpResponseMessage { StatusCode = HttpStatusCode.OK };
        });

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test") };
        var repo = new RemoteLegalHoldRepository(httpClient);

        await repo.ReleaseAsync(legalHoldId, releasedBy, releaseReason, CancellationToken.None);

        Assert.NotNull(capturedRequest);
        Assert.Equal(HttpMethod.Post, capturedRequest.Method);
        Assert.Equal("/api/v1/legal-holds/release", capturedRequest.RequestUri?.PathAndQuery);
    }

    [Fact]
    public async Task ReleaseAsync_WithServerError_Throws()
    {
        var legalHoldId = Guid.NewGuid();

        var handler = new MockHttpMessageHandler(async request =>
        {
            return new HttpResponseMessage { StatusCode = HttpStatusCode.InternalServerError };
        });

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test") };
        var repo = new RemoteLegalHoldRepository(httpClient);

        await Assert.ThrowsAsync<HttpRequestException>(() => repo.ReleaseAsync(legalHoldId, "officer", "reason", CancellationToken.None));
    }

    [Fact]
    public async Task GetActiveByMediaItemIdsAsync_WithValidIds_CallsCorrectEndpointAndReturnsMappedHolds()
    {
        var mediaItemId1 = Guid.NewGuid();
        var mediaItemId2 = Guid.NewGuid();
        var mediaItemIds = new[] { mediaItemId1, mediaItemId2 };

        var dtos = new[]
        {
            TestData.CreateDto(mediaItemId: mediaItemId1),
            TestData.CreateDto(mediaItemId: mediaItemId2)
        };
        var jsonContent = JsonSerializer.Serialize(dtos);
        HttpRequestMessage? capturedRequest = null;

        var handler = new MockHttpMessageHandler(async request =>
        {
            capturedRequest = request;
            return new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json")
            };
        });

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test") };
        var repo = new RemoteLegalHoldRepository(httpClient);

        var result = await repo.GetActiveByMediaItemIdsAsync(mediaItemIds, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        Assert.Contains(result, h => h.MediaItemId == mediaItemId1);
        Assert.Contains(result, h => h.MediaItemId == mediaItemId2);
        Assert.NotNull(capturedRequest);
        Assert.Equal(HttpMethod.Get, capturedRequest.Method);
        Assert.Contains("/api/v1/legal-holds/?mediaItemIds=", capturedRequest.RequestUri?.PathAndQuery);
    }

    [Fact]
    public async Task GetActiveByMediaItemIdsAsync_WithEmptyResponse_ReturnsEmptyList()
    {
        var mediaItemIds = new[] { Guid.NewGuid() };
        var jsonContent = JsonSerializer.Serialize(new List<LegalHoldDto>());

        var handler = new MockHttpMessageHandler(async request =>
        {
            return new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json")
            };
        });

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test") };
        var repo = new RemoteLegalHoldRepository(httpClient);

        var result = await repo.GetActiveByMediaItemIdsAsync(mediaItemIds, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetActiveByMediaItemIdsAsync_WithServerError_Throws()
    {
        var mediaItemIds = new[] { Guid.NewGuid() };

        var handler = new MockHttpMessageHandler(async request =>
        {
            return new HttpResponseMessage { StatusCode = HttpStatusCode.InternalServerError };
        });

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test") };
        var repo = new RemoteLegalHoldRepository(httpClient);

        await Assert.ThrowsAsync<HttpRequestException>(() => repo.GetActiveByMediaItemIdsAsync(mediaItemIds, CancellationToken.None));
    }
}
