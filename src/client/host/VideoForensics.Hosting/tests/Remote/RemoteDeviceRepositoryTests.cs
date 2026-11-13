using System.Net;
using System.Text.Json;

using VideoForensics.Client.Hosting.Remote;
using VideoForensics.Core.Api.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Common.Contracts;

namespace VideoForensics.Client.Hosting.Tests.Remote
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
                LastSuccessfulPullAtUtc: DateTime.UtcNow.AddHours(-1),
                LastPullAttemptAtUtc: DateTime.UtcNow,
                TimeZoneId: "America/New_York",
                LastSyncedUtc: DateTime.UtcNow.AddHours(-1),
                SyncStatus: SyncStatusDto.Synced
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
            DeviceDto device1 = CreateDeviceDto();
            DeviceDto device2 = CreateDeviceDto();
            var dtoList = new List<DeviceDto> { device1, device2 };
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
            var repo = new RemoteDeviceRepository(httpClient);

            IReadOnlyList<DeviceDto> result = await repo.ListAsync(CancellationToken.None);

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
            string json = JsonSerializer.Serialize(new List<DeviceDto>(), JsonOptions);
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

            HttpClient httpClient = CreateHttpClientWithHandler(handler);
            var repo = new RemoteDeviceRepository(httpClient);

            IReadOnlyList<DeviceDto> result = await repo.ListAsync(cts.Token);

            _ = Assert.NotNull(capturedToken);
            Assert.Equal(cts.Token, capturedToken.Value);
        }

        [Fact]
        public async Task ListAsync_EmptyResponse_ReturnsEmptyList()
        {
            string json = JsonSerializer.Serialize(new List<DeviceDto>(), JsonOptions);

            FakeHttpMessageHandler handler = new(async _ =>
            {
                await Task.Yield();
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
                };
            });

            HttpClient httpClient = CreateHttpClientWithHandler(handler);
            var repo = new RemoteDeviceRepository(httpClient);

            IReadOnlyList<DeviceDto> result = await repo.ListAsync(CancellationToken.None);

            Assert.Empty(result);
        }

        [Fact]
        public async Task GetAsync_CallsListAsyncAndFilters_ByDeviceId()
        {
            var deviceId = Guid.NewGuid();
            DeviceDto device1 = CreateDeviceDto(id: deviceId);
            DeviceDto device2 = CreateDeviceDto();
            var dtoList = new List<DeviceDto> { device1, device2 };
            string json = JsonSerializer.Serialize(dtoList, JsonOptions);

            FakeHttpMessageHandler handler = new(async _ =>
            {
                await Task.Yield();
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
                };
            });

            HttpClient httpClient = CreateHttpClientWithHandler(handler);
            var repo = new RemoteDeviceRepository(httpClient);

            DeviceDto? result = await repo.GetAsync(deviceId, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal(deviceId, result.Id);
            Assert.Equal("/api/v1/devices", handler.CapturedRequest?.RequestUri?.PathAndQuery);
        }

        [Fact]
        public async Task GetAsync_DeviceNotFound_ReturnsNull()
        {
            var nonExistentId = Guid.NewGuid();
            DeviceDto device1 = CreateDeviceDto();
            var dtoList = new List<DeviceDto> { device1 };
            string json = JsonSerializer.Serialize(dtoList, JsonOptions);

            FakeHttpMessageHandler handler = new(async _ =>
            {
                await Task.Yield();
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
                };
            });

            HttpClient httpClient = CreateHttpClientWithHandler(handler);
            var repo = new RemoteDeviceRepository(httpClient);

            DeviceDto? result = await repo.GetAsync(nonExistentId, CancellationToken.None);

            Assert.Null(result);
        }

        [Fact]
        public async Task GetByLocationIdAsync_CallsListAsyncAndFilters_ByLocationId()
        {
            var locationId = Guid.NewGuid();
            DeviceDto atLocation = CreateDeviceDto(locationId: locationId);
            DeviceDto elsewhere = CreateDeviceDto();
            string json = JsonSerializer.Serialize(new List<DeviceDto> { atLocation, elsewhere }, JsonOptions);

            FakeHttpMessageHandler handler = new(async _ =>
            {
                await Task.Yield();
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
                };
            });

            HttpClient httpClient = CreateHttpClientWithHandler(handler);
            var repo = new RemoteDeviceRepository(httpClient);

            IReadOnlyList<DeviceDto> result = await repo.GetByLocationIdAsync(locationId, CancellationToken.None);

            Assert.Single(result);
            Assert.Equal(atLocation.Id, result[0].Id);
            Assert.Equal("/api/v1/devices", handler.CapturedRequest?.RequestUri?.PathAndQuery);
        }

        [Fact]
        public async Task GetByProviderDeviceIdAsync_CallsListAsyncAndFilters_ByLocationAndProviderId()
        {
            var locationId = Guid.NewGuid();
            DeviceDto match = CreateDeviceDto(locationId: locationId);
            string json = JsonSerializer.Serialize(new List<DeviceDto> { match }, JsonOptions);

            FakeHttpMessageHandler handler = new(async _ =>
            {
                await Task.Yield();
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
                };
            });

            HttpClient httpClient = CreateHttpClientWithHandler(handler);
            var repo = new RemoteDeviceRepository(httpClient);

            DeviceDto? found = await repo.GetByProviderDeviceIdAsync(locationId, "ring-device-123", CancellationToken.None);
            DeviceDto? missing = await repo.GetByProviderDeviceIdAsync(locationId, "no-such-device", CancellationToken.None);

            Assert.NotNull(found);
            Assert.Equal(match.Id, found.Id);
            Assert.Null(missing);
        }

        [Fact]
        public void RemoteDeviceRepository_IsNotAWriteRepository()
        {
            Assert.False(typeof(IDeviceWriteRepository).IsAssignableFrom(typeof(RemoteDeviceRepository)));
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
                LastSuccessfulPullAtUtc: new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc),
                LastPullAttemptAtUtc: new DateTime(2024, 1, 2, 12, 0, 0, DateTimeKind.Utc),
                TimeZoneId: "America/Los_Angeles",
                LastSyncedUtc: new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc),
                SyncStatus: SyncStatusDto.Stale
            );

            string json = JsonSerializer.Serialize(new List<DeviceDto> { deviceDto }, JsonOptions);

            FakeHttpMessageHandler handler = new(async _ =>
            {
                await Task.Yield();
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
                };
            });

            HttpClient httpClient = CreateHttpClientWithHandler(handler);
            var repo = new RemoteDeviceRepository(httpClient);

            IReadOnlyList<DeviceDto> result = await repo.ListAsync(CancellationToken.None);

            DeviceDto device = result[0];
            Assert.Equal(deviceDto.Id, device.Id);
            Assert.Equal(deviceDto.LocationId, device.LocationId);
            Assert.Equal(deviceDto.ProviderDeviceId, device.ProviderDeviceId);
            Assert.Equal(deviceDto.Name, device.Name);
            Assert.Equal(deviceDto.Type, device.Type);
            Assert.Equal(deviceDto.IsOnline, device.IsOnline);
            Assert.Equal(deviceDto.LastSuccessfulPullAtUtc, device.LastSuccessfulPullAtUtc);
            Assert.Equal(deviceDto.LastPullAttemptAtUtc, device.LastPullAttemptAtUtc);
            Assert.Equal(deviceDto.TimeZoneId, device.TimeZoneId);
            Assert.Equal(deviceDto.LastSyncedUtc, device.LastSyncedUtc);
        }
    }
}
