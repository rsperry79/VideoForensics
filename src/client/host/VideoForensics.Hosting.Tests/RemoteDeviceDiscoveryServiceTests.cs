using System.Net;
using System.Text.Json;

using VideoForensics.Api.Contracts;
using VideoForensics.Hosting.Remote;
using VideoForensics.Providers.Common.Contracts;

using Xunit;
namespace VideoForensics.Hosting.Tests
{
    public class RemoteDeviceDiscoveryServiceTests
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
        public async Task GetLocationsAsync_CallsLocationsEndpoint_ReturnsLocations()
        {
            var location1 = new LocationDto("loc-1", "Home", "123 Main St", new Dictionary<string, string> { { "region", "us-east-1" } });
            var location2 = new LocationDto("loc-2", "Office", "456 Oak Ave", null);
            var dtoList = new List<LocationDto> { location1, location2 };
            string json = JsonSerializer.Serialize(dtoList, JsonOptions);
            var handler = new FakeHttpMessageHandler(async _ => { await Task.Yield(); return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json") }; });
            var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test") };
            var service = new RemoteDeviceDiscoveryService(httpClient);
            IReadOnlyList<Location> result = await service.GetLocationsAsync(CancellationToken.None);
            Assert.NotNull(handler.CapturedRequest);
            Assert.Equal(HttpMethod.Get, handler.CapturedRequest.Method);
            Assert.Equal(2, result.Count);
        }
        [Fact]
        public async Task GetDevicesAsync_WithLocationId_CallsDevicesEndpoint()
        {
            string locationId = "loc-123";
            var device1 = new DiscoveryDeviceDto("dev-1", "Front Camera", "camera", locationId, true, new Dictionary<string, string> { { "model", "ring-cam" } });
            var dtoList = new List<DiscoveryDeviceDto> { device1 };
            string json = JsonSerializer.Serialize(dtoList, JsonOptions);
            var handler = new FakeHttpMessageHandler(async _ => { await Task.Yield(); return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json") }; });
            var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test") };
            var service = new RemoteDeviceDiscoveryService(httpClient);
            IReadOnlyList<Device> result = await service.GetDevicesAsync(locationId, CancellationToken.None);
            Assert.NotNull(handler.CapturedRequest);
            Assert.Equal(HttpMethod.Get, handler.CapturedRequest.Method);
            _ = Assert.Single(result);
        }
        [Fact]
        public async Task GetDeviceAsync_WithValidDeviceId_ReturnsDevice()
        {
            string deviceId = "dev-789";
            var dto = new DiscoveryDeviceDto(deviceId, "Doorbell", "doorbell", "loc-front", true, new Dictionary<string, string> { { "battery", "100%" } });
            string json = JsonSerializer.Serialize(dto, JsonOptions);
            var handler = new FakeHttpMessageHandler(async _ => { await Task.Yield(); return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json") }; });
            var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test") };
            var service = new RemoteDeviceDiscoveryService(httpClient);
            Device? result = await service.GetDeviceAsync(deviceId, CancellationToken.None);
            Assert.NotNull(handler.CapturedRequest);
            Assert.Equal(HttpMethod.Get, handler.CapturedRequest.Method);
            Assert.NotNull(result);
            Assert.Equal(deviceId, result.Id);
        }
        [Fact]
        public async Task GetDeviceAsync_WithNotFoundResponse_ReturnsNull()
        {
            var handler = new FakeHttpMessageHandler(async _ => { await Task.Yield(); return new HttpResponseMessage(HttpStatusCode.NotFound); });
            var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test") };
            var service = new RemoteDeviceDiscoveryService(httpClient);
            Device? result = await service.GetDeviceAsync("non-existent", CancellationToken.None);
            Assert.Null(result);
        }
    }
}
