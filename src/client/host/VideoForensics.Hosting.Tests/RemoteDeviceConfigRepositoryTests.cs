using System.Net;
using System.Text.Json;

using VideoForensics.Api.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Hosting.Remote;

using Xunit;

namespace VideoForensics.Hosting.Tests;

public class RemoteDeviceConfigRepositoryTests
{
    private static class TestData
    {
        public static DeviceConfigSnapshotDto CreateDto(Guid? id = null, Guid? deviceId = null)
        {
            return new DeviceConfigSnapshotDto(
                Id: id ?? Guid.NewGuid(),
                DeviceId: deviceId ?? Guid.NewGuid(),
                MotionDetectionEnabled: true,
                MotionSensitivity: "high",
                RecordingMode: "continuous",
                CustomSettingsJson: """{"vendor": "test"}""",
                CapturedAtUtc: DateTime.UtcNow,
                Source: "Fetched"
            );
        }

        public static DeviceConfigSnapshot CreateDomain(Guid? id = null, Guid? deviceId = null)
        {
            return new DeviceConfigSnapshot
            {
                Id = id ?? Guid.NewGuid(),
                DeviceId = deviceId ?? Guid.NewGuid(),
                MotionDetectionEnabled = true,
                MotionSensitivity = "high",
                RecordingMode = "continuous",
                CustomSettingsJson = """{"vendor": "test"}""",
                CapturedAtUtc = DateTime.UtcNow,
                Source = DeviceConfigSource.Fetched
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
    public async Task GetAsync_WithValidId_CallsCorrectEndpointAndReturnsMappedSnapshot()
    {
        var snapshotId = Guid.NewGuid();
        DeviceConfigSnapshotDto dto = TestData.CreateDto(id: snapshotId);
        string jsonContent = JsonSerializer.Serialize(dto);
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
        var repo = new RemoteDeviceConfigRepository(httpClient);
        CancellationToken ct = CancellationToken.None;

        DeviceConfigSnapshot? result = await repo.GetAsync(snapshotId, ct);

        Assert.NotNull(result);
        Assert.Equal(snapshotId, result!.Id);
        Assert.Equal(dto.DeviceId, result.DeviceId);
        Assert.Equal(dto.MotionDetectionEnabled, result.MotionDetectionEnabled);
        Assert.NotNull(capturedRequest);
        Assert.Equal(HttpMethod.Get, capturedRequest.Method);
        Assert.Equal($"/api/v1/device-config/{snapshotId}", capturedRequest.RequestUri?.PathAndQuery);
    }

    [Fact]
    public async Task GetAsync_WithNotFoundResponse_ReturnsNull()
    {
        var snapshotId = Guid.NewGuid();

        var handler = new MockHttpMessageHandler(async request =>
        {
            return new HttpResponseMessage { StatusCode = HttpStatusCode.NotFound };
        });

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test") };
        var repo = new RemoteDeviceConfigRepository(httpClient);

        DeviceConfigSnapshot? result = await repo.GetAsync(snapshotId, CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task AppendSnapshotAsync_WithValidSnapshot_CallsPostEndpointAndReturnsMappedResult()
    {
        var deviceId = Guid.NewGuid();
        DeviceConfigSnapshot snapshot = TestData.CreateDomain(deviceId: deviceId);
        DeviceConfigSnapshotDto responseDto = TestData.CreateDto(deviceId: deviceId);
        string jsonContent = JsonSerializer.Serialize(responseDto);
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
        var repo = new RemoteDeviceConfigRepository(httpClient);

        DeviceConfigSnapshot result = await repo.AppendSnapshotAsync(snapshot, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(deviceId, result.DeviceId);
        Assert.Equal(snapshot.MotionDetectionEnabled, result.MotionDetectionEnabled);
        Assert.NotNull(capturedRequest);
        Assert.Equal(HttpMethod.Post, capturedRequest.Method);
        Assert.Equal("/api/v1/device-config", capturedRequest.RequestUri?.PathAndQuery);
    }

    [Fact]
    public async Task AppendSnapshotAsync_WithServerError_Throws()
    {
        DeviceConfigSnapshot snapshot = TestData.CreateDomain();

        var handler = new MockHttpMessageHandler(async request =>
        {
            return new HttpResponseMessage { StatusCode = HttpStatusCode.InternalServerError };
        });

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test") };
        var repo = new RemoteDeviceConfigRepository(httpClient);

        _ = await Assert.ThrowsAsync<HttpRequestException>(() => repo.AppendSnapshotAsync(snapshot, CancellationToken.None));
    }

    [Fact]
    public async Task GetLatestAsync_WithValidDeviceId_CallsCorrectEndpointAndReturnsMappedSnapshot()
    {
        var deviceId = Guid.NewGuid();
        DeviceConfigSnapshotDto dto = TestData.CreateDto(deviceId: deviceId);
        string jsonContent = JsonSerializer.Serialize(dto);
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
        var repo = new RemoteDeviceConfigRepository(httpClient);

        DeviceConfigSnapshot? result = await repo.GetLatestAsync(deviceId, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(deviceId, result!.DeviceId);
        Assert.NotNull(capturedRequest);
        Assert.Equal(HttpMethod.Get, capturedRequest.Method);
        Assert.Equal($"/api/v1/device-config/by-device/{deviceId}", capturedRequest.RequestUri?.PathAndQuery);
    }

    [Fact]
    public async Task GetLatestAsync_WithNotFoundResponse_ReturnsNull()
    {
        var deviceId = Guid.NewGuid();

        var handler = new MockHttpMessageHandler(async request =>
        {
            return new HttpResponseMessage { StatusCode = HttpStatusCode.NotFound };
        });

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test") };
        var repo = new RemoteDeviceConfigRepository(httpClient);

        DeviceConfigSnapshot? result = await repo.GetLatestAsync(deviceId, CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetHistoryAsync_WithValidDeviceId_CallsCorrectEndpointAndReturnsMappedList()
    {
        var deviceId = Guid.NewGuid();
        DeviceConfigSnapshotDto[] dtos = new[]
        {
            TestData.CreateDto(deviceId: deviceId),
            TestData.CreateDto(deviceId: deviceId)
        };
        string jsonContent = JsonSerializer.Serialize(dtos);
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
        var repo = new RemoteDeviceConfigRepository(httpClient);

        IReadOnlyList<DeviceConfigSnapshot> result = await repo.GetHistoryAsync(deviceId, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        Assert.All(result, item => Assert.Equal(deviceId, item.DeviceId));
        Assert.NotNull(capturedRequest);
        Assert.Equal(HttpMethod.Get, capturedRequest.Method);
        Assert.Equal($"/api/v1/device-config/history/{deviceId}", capturedRequest.RequestUri?.PathAndQuery);
    }

    [Fact]
    public async Task GetHistoryAsync_WithEmptyResponse_ReturnsEmptyList()
    {
        var deviceId = Guid.NewGuid();
        string jsonContent = JsonSerializer.Serialize(new List<DeviceConfigSnapshotDto>());

        var handler = new MockHttpMessageHandler(async request =>
        {
            return new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json")
            };
        });

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test") };
        var repo = new RemoteDeviceConfigRepository(httpClient);

        IReadOnlyList<DeviceConfigSnapshot> result = await repo.GetHistoryAsync(deviceId, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task ListAsync_WithValidRequest_CallsCorrectEndpointAndReturnsMappedList()
    {
        DeviceConfigSnapshotDto[] dtos = new[]
        {
            TestData.CreateDto(),
            TestData.CreateDto()
        };
        string jsonContent = JsonSerializer.Serialize(dtos);
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
        var repo = new RemoteDeviceConfigRepository(httpClient);

        IReadOnlyList<DeviceConfigSnapshot> result = await repo.ListAsync(CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        Assert.NotNull(capturedRequest);
        Assert.Equal(HttpMethod.Get, capturedRequest.Method);
        Assert.Equal("/api/v1/device-config/", capturedRequest.RequestUri?.PathAndQuery);
    }

    [Fact]
    public async Task ListAsync_WithEmptyResponse_ReturnsEmptyList()
    {
        string jsonContent = JsonSerializer.Serialize(new List<DeviceConfigSnapshotDto>());

        var handler = new MockHttpMessageHandler(async request =>
        {
            return new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json")
            };
        });

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test") };
        var repo = new RemoteDeviceConfigRepository(httpClient);

        IReadOnlyList<DeviceConfigSnapshot> result = await repo.ListAsync(CancellationToken.None);

        Assert.NotNull(result);
        Assert.Empty(result);
    }
}
