using System.Net;
using System.Text.Json;

using VideoForensics.Api.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Hosting.Remote;

using Xunit;

namespace VideoForensics.Hosting.Tests
{
    public class RemoteDeviceHealthRepositoryTests
    {
        private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

        private static DeviceHealthDto CreateDeviceHealthDto(Guid? id = null, Guid? deviceId = null, DateTime? capturedAtUtc = null)
        {
            return new DeviceHealthDto(
                Id: id ?? Guid.NewGuid(),
                DeviceId: deviceId ?? Guid.NewGuid(),
                BatteryPercentage: 80m,
                BatteryVoltageValue: 4.1m,
                WifiSignalRssi: -55,
                WifiName: "HomeNetwork",
                IsExternalPowerConnected: false,
                OtaStatus: "up_to_date",
                IsOnline: true,
                LastHeartbeatUtc: DateTime.UtcNow,
                FirmwareVersion: "1.2.3",
                CapturedAtUtc: capturedAtUtc ?? DateTime.UtcNow
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
        public async Task GetHistoryAsync_WithDateRange_CallsCorrectEndpointWithEscapedQuery_AndReturnsItems()
        {
            var deviceId = Guid.NewGuid();
            var fromUtc = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var toUtc = new DateTime(2024, 1, 2, 0, 0, 0, DateTimeKind.Utc);

            DeviceHealthDto health1 = CreateDeviceHealthDto(deviceId: deviceId, capturedAtUtc: fromUtc.AddHours(1));
            DeviceHealthDto health2 = CreateDeviceHealthDto(deviceId: deviceId, capturedAtUtc: fromUtc.AddHours(2));
            var dtoList = new List<DeviceHealthDto> { health1, health2 };
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
            var repo = new RemoteDeviceHealthRepository(httpClient);

            IReadOnlyList<DeviceHealth> result = await repo.GetHistoryAsync(deviceId, fromUtc, toUtc, CancellationToken.None);

            Assert.NotNull(handler.CapturedRequest);
            Assert.Equal(HttpMethod.Get, handler.CapturedRequest.Method);
            string? uri = handler.CapturedRequest.RequestUri?.PathAndQuery;
            Assert.NotNull(uri);

            string expectedFromEscaped = Uri.EscapeDataString(fromUtc.ToString("O"));
            string expectedToEscaped = Uri.EscapeDataString(toUtc.ToString("O"));
            string expectedQuery = $"/api/v1/devices/{deviceId}/health?from={expectedFromEscaped}&to={expectedToEscaped}";
            Assert.Equal(expectedQuery, uri);

            Assert.Equal(2, result.Count);
            Assert.Equal(health1.Id, result[0].Id);
            Assert.Equal(health2.Id, result[1].Id);
        }

        [Fact]
        public async Task GetHistoryAsync_WithCancellationToken_PassesToHttpClient()
        {
            var cts = new CancellationTokenSource();
            var deviceId = Guid.NewGuid();
            var fromUtc = DateTime.UtcNow.AddDays(-1);
            var toUtc = DateTime.UtcNow;
            string json = JsonSerializer.Serialize(new List<DeviceHealthDto>(), JsonOptions);
            CancellationToken? capturedToken = null;

            FakeHttpMessageHandler handler = new(async _ =>
            {
                capturedToken = cts.Token;
                await Task.Yield();
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
                };
            });

            HttpClient httpClient = CreateHttpClientWithHandler(handler);
            var repo = new RemoteDeviceHealthRepository(httpClient);

            _ = await repo.GetHistoryAsync(deviceId, fromUtc, toUtc, cts.Token);

            _ = Assert.NotNull(capturedToken);
            Assert.Equal(cts.Token, capturedToken.Value);
        }

        [Fact]
        public async Task GetHistoryAsync_WithDateRange_EmptyResponse_ReturnsEmptyList()
        {
            var deviceId = Guid.NewGuid();
            string json = JsonSerializer.Serialize(new List<DeviceHealthDto>(), JsonOptions);

            FakeHttpMessageHandler handler = new(async _ =>
            {
                await Task.Yield();
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
                };
            });

            HttpClient httpClient = CreateHttpClientWithHandler(handler);
            var repo = new RemoteDeviceHealthRepository(httpClient);

            IReadOnlyList<DeviceHealth> result = await repo.GetHistoryAsync(deviceId, DateTime.UtcNow.AddDays(-1), DateTime.UtcNow, CancellationToken.None);

            Assert.Empty(result);
        }

        [Fact]
        public async Task GetHistoryAsync_WithDateRange_DtoMappingCorrect_PropertiesMatchDomain()
        {
            var deviceId = Guid.NewGuid();
            DeviceHealthDto dto = CreateDeviceHealthDto(deviceId: deviceId);
            string json = JsonSerializer.Serialize(new List<DeviceHealthDto> { dto }, JsonOptions);

            FakeHttpMessageHandler handler = new(async _ =>
            {
                await Task.Yield();
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
                };
            });

            HttpClient httpClient = CreateHttpClientWithHandler(handler);
            var repo = new RemoteDeviceHealthRepository(httpClient);

            IReadOnlyList<DeviceHealth> result = await repo.GetHistoryAsync(deviceId, DateTime.UtcNow.AddDays(-1), DateTime.UtcNow, CancellationToken.None);

            DeviceHealth health = result[0];
            Assert.Equal(dto.Id, health.Id);
            Assert.Equal(dto.DeviceId, health.DeviceId);
            Assert.Equal(dto.BatteryPercentage, health.BatteryPercentage);
            Assert.Equal(dto.BatteryVoltageValue, health.BatteryVoltageValue);
            Assert.Equal(dto.WifiSignalRssi, health.WifiSignalRssi);
            Assert.Equal(dto.WifiName, health.WifiName);
            Assert.Equal(dto.IsExternalPowerConnected, health.IsExternalPowerConnected);
            Assert.Equal(dto.OtaStatus, health.OtaStatus);
            Assert.Equal(dto.IsOnline, health.IsOnline);
            Assert.Equal(dto.FirmwareVersion, health.FirmwareVersion);
            Assert.Equal(dto.CapturedAtUtc, health.CapturedAtUtc);
        }

        [Fact]
        public async Task GetHistoryAsync_NoDateRange_Throws_NotSupportedException()
        {
            var handler = new FakeHttpMessageHandler(async _ =>
            {
                await Task.Yield();
                return new HttpResponseMessage(HttpStatusCode.OK);
            });

            HttpClient httpClient = CreateHttpClientWithHandler(handler);
            var repo = new RemoteDeviceHealthRepository(httpClient);

            NotSupportedException ex = await Assert.ThrowsAsync<NotSupportedException>(
                () => repo.GetHistoryAsync(Guid.NewGuid(), CancellationToken.None));

            Assert.Contains("Not supported on a remote", ex.Message);
        }

        [Fact]
        public async Task GetLatestAsync_Throws_NotSupportedException()
        {
            var handler = new FakeHttpMessageHandler(async _ =>
            {
                await Task.Yield();
                return new HttpResponseMessage(HttpStatusCode.OK);
            });

            HttpClient httpClient = CreateHttpClientWithHandler(handler);
            var repo = new RemoteDeviceHealthRepository(httpClient);

            NotSupportedException ex = await Assert.ThrowsAsync<NotSupportedException>(
                () => repo.GetLatestAsync(Guid.NewGuid(), CancellationToken.None));

            Assert.Contains("Not supported on a remote", ex.Message);
        }

        [Fact]
        public async Task AddAsync_Throws_NotSupportedException()
        {
            var handler = new FakeHttpMessageHandler(async _ =>
            {
                await Task.Yield();
                return new HttpResponseMessage(HttpStatusCode.OK);
            });

            HttpClient httpClient = CreateHttpClientWithHandler(handler);
            var repo = new RemoteDeviceHealthRepository(httpClient);

            var health = new DeviceHealth { Id = Guid.NewGuid(), DeviceId = Guid.NewGuid(), CapturedAtUtc = DateTime.UtcNow };

            NotSupportedException ex = await Assert.ThrowsAsync<NotSupportedException>(
                () => repo.AddAsync(health, CancellationToken.None));

            Assert.Contains("MAUI has no write path", ex.Message);
        }
    }
}
