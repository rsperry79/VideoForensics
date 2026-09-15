using System.Net;
using System.Text.Json;
using VideoForensics.Api.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Hosting.Remote;
using Xunit;

namespace VideoForensics.Hosting.Tests;

public class RemoteIntegrityRecordRepositoryTests
{
    private static class TestData
    {
        public static IntegrityRecordDto CreateDto(Guid? id = null, Guid? mediaItemId = null)
        {
            return new IntegrityRecordDto(
                Id: id ?? Guid.NewGuid(),
                MediaItemId: mediaItemId ?? Guid.NewGuid(),
                Sha256Hash: "abc123def456abc123def456abc123def456abc123def456abc123def456abc1",
                VerifiedAtUtc: DateTime.UtcNow,
                Passed: true,
                FailureReason: null,
                VerifiedBy: "system-verifier"
            );
        }

        public static IntegrityRecord CreateDomain(Guid? id = null, Guid? mediaItemId = null)
        {
            return new IntegrityRecord
            {
                Id = id ?? Guid.NewGuid(),
                MediaItemId = mediaItemId ?? Guid.NewGuid(),
                Sha256Hash = "abc123def456abc123def456abc123def456abc123def456abc123def456abc1",
                VerifiedAtUtc = DateTime.UtcNow,
                Passed = true,
                FailureReason = null,
                VerifiedBy = "system-verifier"
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
    public async Task GetLatestByMediaItemIdsAsync_WithValidIds_CallsCorrectEndpointAndReturnsMappedRecords()
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
        var repo = new RemoteIntegrityRecordRepository(httpClient);

        var result = await repo.GetLatestByMediaItemIdsAsync(mediaItemIds, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        Assert.Contains(result, r => r.MediaItemId == mediaItemId1);
        Assert.Contains(result, r => r.MediaItemId == mediaItemId2);
        Assert.NotNull(capturedRequest);
        Assert.Equal(HttpMethod.Get, capturedRequest.Method);
        Assert.Contains("/api/v1/integrity-records?mediaItemIds=", capturedRequest.RequestUri?.PathAndQuery);
    }

    [Fact]
    public async Task GetLatestByMediaItemIdsAsync_WithEmptyResponse_ReturnsEmptyList()
    {
        var mediaItemIds = new[] { Guid.NewGuid() };
        var jsonContent = JsonSerializer.Serialize(new List<IntegrityRecordDto>());

        var handler = new MockHttpMessageHandler(async request =>
        {
            return new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json")
            };
        });

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test") };
        var repo = new RemoteIntegrityRecordRepository(httpClient);

        var result = await repo.GetLatestByMediaItemIdsAsync(mediaItemIds, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetLatestByMediaItemIdsAsync_WithServerError_Throws()
    {
        var mediaItemIds = new[] { Guid.NewGuid() };

        var handler = new MockHttpMessageHandler(async request =>
        {
            return new HttpResponseMessage { StatusCode = HttpStatusCode.InternalServerError };
        });

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test") };
        var repo = new RemoteIntegrityRecordRepository(httpClient);

        await Assert.ThrowsAsync<HttpRequestException>(() => repo.GetLatestByMediaItemIdsAsync(mediaItemIds, CancellationToken.None));
    }

    [Fact]
    public async Task AddAsync_WithValidRecord_ThrowsNotSupportedException()
    {
        var record = TestData.CreateDomain();

        var handler = new MockHttpMessageHandler(async request =>
        {
            return new HttpResponseMessage { StatusCode = HttpStatusCode.OK };
        });

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test") };
        var repo = new RemoteIntegrityRecordRepository(httpClient);

        var ex = await Assert.ThrowsAsync<NotSupportedException>(() => repo.AddAsync(record, CancellationToken.None));
        Assert.Contains("append-only", ex.Message);
        Assert.Contains("server-owned", ex.Message);
    }
}
