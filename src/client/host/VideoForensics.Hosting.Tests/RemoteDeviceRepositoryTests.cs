using System.Net;
using System.Text.Json;

using VideoForensics.Api.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Hosting.Remote;

using Xunit;

namespace VideoForensics.Hosting.Tests
{
    public class RemoteDeviceRepositoryTests
    {
        private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

        private static DeviceDto CreateDeviceDto(Guid? id = null, Guid? locationId = null)
        {
            return new DeviceDto(
                Id: id ?? Guid.NewGuid(),
                LocationId: locationId ?? Guid.NewGuid(),
                ProviderDeviceId: "ring-device-123",
                Name: "Front Door Camera",
                Type: "camera",
                IsOnline: true,
                MetadataJson: null,
                LastSuccessfulPullAtUtc: DateTime.UtcNow.AddHours(-1),
                LastPullAttemptAtUtc: DateTime.UtcNow,
                TimeZoneId: "America/New_York",
                LastSyncedUtc: DateTime.UtcNow.AddHours(-1),
                SyncStatus: SyncStatusDto.Synced,
                ApiResponseHash: null
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

        [Fact]
        public async Task ListAsync_CallsGetDevicesEndpoint_AndReturnsDevices()
        {
            var device1 = CreateDeviceDto();
            var device2 = CreateDeviceDto();
            var dtoList = new List<DeviceDto> { device1, device2 };
            var json = JsonSerializer.Serialize(dtoList, JsonOptions);

            FakeHttpMessageHandler handler = new(async req =>
            {
                await Task.Yield();
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
                };
            });

            var httpClient = CreateHttpClientWithHandler(handler);
            var repo = new RemoteDeviceRepository(httpClient);

            var result = await repo.ListAsync(CancellationToken.None);

            Assert.NotNull(handler.CapturedRequest);
            Assert.Equal(HttpMethod.Get, handler.CapturedRequest.Method);
            Assert.Equal("/api/v1/devices", handler.CapturedRequest.RequestUri?.PathAndQuery);
            Assert.Equal(2, result.Count);
            Assert.Equal(device1.Id, result[0].Id);
            Assert.Equal(device2.Id, result[1].Id);
        }

        [Fact]
        public async Task ListAsync_WithCancellationToken_PassesToHttpClient()
        {
            var cts = new CancellationTokenSource();
            var json = JsonSerializer.Serialize(new List<DeviceDto>(), JsonOptions);
            CancellationToken? capturedToken = null;

            FakeHttpMessageHandler handler = new(async req =>
            {
                capturedToken = cts.Token;
                await Task.Yield();
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
                };
            });

            var httpClient = CreateHttpClientWithHandler(handler);
            var repo = new RemoteDeviceRepository(httpClient);

            var result = await repo.ListAsync(cts.Token);

            Assert.NotNull(capturedToken);
            Assert.Equal(cts.Token, capturedToken.Value);
        }

        [Fact]
        public async Task ListAsync_EmptyResponse_ReturnsEmptyList()
        {
            var json = JsonSerializer.Serialize(new List<DeviceDto>(), JsonOptions);

            FakeHttpMessageHandler handler = new(async _ =>
            {
                await Task.Yield();
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
                };
            });

            var httpClient = CreateHttpClientWithHandler(handler);
            var repo = new RemoteDeviceRepository(httpClient);

            var result = await repo.ListAsync(CancellationToken.None);

            Assert.Empty(result);
        }

        [Fact]
        public async Task GetAsync_CallsListAsyncAndFilters_ByDeviceId()
        {
            var deviceId = Guid.NewGuid();
            var device1 = CreateDeviceDto(id: deviceId);
            var device2 = CreateDeviceDto();
            var dtoList = new List<DeviceDto> { device1, device2 };
            var json = JsonSerializer.Serialize(dtoList, JsonOptions);

            FakeHttpMessageHandler handler = new(async _ =>
            {
                await Task.Yield();
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
                };
            });

            var httpClient = CreateHttpClientWithHandler(handler);
            var repo = new RemoteDeviceRepository(httpClient);

            var result = await repo.GetAsync(deviceId, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal(deviceId, result.Id);
            Assert.Equal("/api/v1/devices", handler.CapturedRequest?.RequestUri?.PathAndQuery);
        }

        [Fact]
        public async Task GetAsync_DeviceNotFound_ReturnsNull()
        {
            var nonExistentId = Guid.NewGuid();
            var device1 = CreateDeviceDto();
            var dtoList = new List<DeviceDto> { device1 };
            var json = JsonSerializer.Serialize(dtoList, JsonOptions);

            FakeHttpMessageHandler handler = new(async _ =>
            {
                await Task.Yield();
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
                };
            });

            var httpClient = CreateHttpClientWithHandler(handler);
            var repo = new RemoteDeviceRepository(httpClient);

            var result = await repo.GetAsync(nonExistentId, CancellationToken.None);

            Assert.Null(result);
        }

        [Fact]
        public async Task GetByLocationIdAsync_Throws_NotSupportedException()
        {
            var handler = new FakeHttpMessageHandler(async _ =>
            {
                await Task.Yield();
                return new HttpResponseMessage(HttpStatusCode.OK);
            });

            var httpClient = CreateHttpClientWithHandler(handler);
            var repo = new RemoteDeviceRepository(httpClient);

            var ex = await Assert.ThrowsAsync<NotSupportedException>(
                () => repo.GetByLocationIdAsync(Guid.NewGuid(), CancellationToken.None));

            Assert.Contains("Not supported on a remote", ex.Message);
        }

        [Fact]
        public async Task GetByProviderDeviceIdAsync_Throws_NotSupportedException()
        {
            var handler = new FakeHttpMessageHandler(async _ =>
            {
                await Task.Yield();
                return new HttpResponseMessage(HttpStatusCode.OK);
            });

            var httpClient = CreateHttpClientWithHandler(handler);
            var repo = new RemoteDeviceRepository(httpClient);

            var ex = await Assert.ThrowsAsync<NotSupportedException>(
                () => repo.GetByProviderDeviceIdAsync(Guid.NewGuid(), "provider-123", CancellationToken.None));

            Assert.Contains("Not supported on a remote", ex.Message);
        }

        [Fact]
        public async Task GetByApiHashAsync_Throws_NotSupportedException()
        {
            var handler = new FakeHttpMessageHandler(async _ =>
            {
                await Task.Yield();
                return new HttpResponseMessage(HttpStatusCode.OK);
            });

            var httpClient = CreateHttpClientWithHandler(handler);
            var repo = new RemoteDeviceRepository(httpClient);

            var ex = await Assert.ThrowsAsync<NotSupportedException>(
                () => repo.GetByApiHashAsync("hash123", CancellationToken.None));

            Assert.Contains("Remote API doesn't yet support", ex.Message);
        }

        [Fact]
        public async Task AddAsync_Throws_NotSupportedException()
        {
            var handler = new FakeHttpMessageHandler(async _ =>
            {
                await Task.Yield();
                return new HttpResponseMessage(HttpStatusCode.OK);
            });

            var httpClient = CreateHttpClientWithHandler(handler);
            var repo = new RemoteDeviceRepository(httpClient);

            var device = new Device { Id = Guid.NewGuid(), LocationId = Guid.NewGuid(), Name = "Test", ProviderDeviceId = "test-device", Type = "camera" };

            var ex = await Assert.ThrowsAsync<NotSupportedException>(
                () => repo.AddAsync(device, CancellationToken.None));

            Assert.Contains("Not supported on a remote", ex.Message);
        }

        [Fact]
        public async Task UpdateAsync_Throws_NotSupportedException()
        {
            var handler = new FakeHttpMessageHandler(async _ =>
            {
                await Task.Yield();
                return new HttpResponseMessage(HttpStatusCode.OK);
            });

            var httpClient = CreateHttpClientWithHandler(handler);
            var repo = new RemoteDeviceRepository(httpClient);

            var device = new Device { Id = Guid.NewGuid(), LocationId = Guid.NewGuid(), Name = "Test", ProviderDeviceId = "test-device", Type = "camera" };

            var ex = await Assert.ThrowsAsync<NotSupportedException>(
                () => repo.UpdateAsync(device, CancellationToken.None));

            Assert.Contains("Not supported on a remote", ex.Message);
        }

        [Fact]
        public async Task UpdateLastSuccessfulPullAsync_Throws_NotSupportedException()
        {
            var handler = new FakeHttpMessageHandler(async _ =>
            {
                await Task.Yield();
                return new HttpResponseMessage(HttpStatusCode.OK);
            });

            var httpClient = CreateHttpClientWithHandler(handler);
            var repo = new RemoteDeviceRepository(httpClient);

            var ex = await Assert.ThrowsAsync<NotSupportedException>(
                () => repo.UpdateLastSuccessfulPullAsync(Guid.NewGuid(), DateTime.UtcNow, CancellationToken.None));

            Assert.Contains("Not supported on a remote", ex.Message);
        }

        [Fact]
        public async Task DeleteAsync_Throws_NotSupportedException()
        {
            var handler = new FakeHttpMessageHandler(async _ =>
            {
                await Task.Yield();
                return new HttpResponseMessage(HttpStatusCode.OK);
            });

            var httpClient = CreateHttpClientWithHandler(handler);
            var repo = new RemoteDeviceRepository(httpClient);

            var ex = await Assert.ThrowsAsync<NotSupportedException>(
                () => repo.DeleteAsync(Guid.NewGuid(), CancellationToken.None));

            Assert.Contains("Not supported on a remote", ex.Message);
        }

        [Fact]
        public async Task ListAsync_DtoMappingCorrect_PropertiesMatchDomain()
        {
            var deviceDto = new DeviceDto(
                Id: Guid.NewGuid(),
                LocationId: Guid.NewGuid(),
                ProviderDeviceId: "ring-456",
                Name: "Backyard Camera",
                Type: "camera",
                IsOnline: false,
                MetadataJson: "{\"key\":\"value\"}",
                LastSuccessfulPullAtUtc: new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc),
                LastPullAttemptAtUtc: new DateTime(2024, 1, 2, 12, 0, 0, DateTimeKind.Utc),
                TimeZoneId: "America/Los_Angeles",
                LastSyncedUtc: new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc),
                SyncStatus: SyncStatusDto.Stale,
                ApiResponseHash: "hash-123"
            );

            var json = JsonSerializer.Serialize(new List<DeviceDto> { deviceDto }, JsonOptions);

            FakeHttpMessageHandler handler = new(async _ =>
            {
                await Task.Yield();
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
                };
            });

            var httpClient = CreateHttpClientWithHandler(handler);
            var repo = new RemoteDeviceRepository(httpClient);

            var result = await repo.ListAsync(CancellationToken.None);

            var device = result[0];
            Assert.Equal(deviceDto.Id, device.Id);
            Assert.Equal(deviceDto.LocationId, device.LocationId);
            Assert.Equal(deviceDto.ProviderDeviceId, device.ProviderDeviceId);
            Assert.Equal(deviceDto.Name, device.Name);
            Assert.Equal(deviceDto.Type, device.Type);
            Assert.Equal(deviceDto.IsOnline, device.IsOnline);
            Assert.Equal(deviceDto.MetadataJson, device.MetadataJson);
            Assert.Equal(deviceDto.LastSuccessfulPullAtUtc, device.LastSuccessfulPullAtUtc);
            Assert.Equal(deviceDto.LastPullAttemptAtUtc, device.LastPullAttemptAtUtc);
            Assert.Equal(deviceDto.TimeZoneId, device.TimeZoneId);
            Assert.Equal(deviceDto.LastSyncedUtc, device.LastSyncedUtc);
            Assert.Equal(deviceDto.ApiResponseHash, device.ApiResponseHash);
        }
    }
}
