using System.Net;
using System.Text.Json;

using VideoForensics.Api.Contracts;
using VideoForensics.Hosting.Remote;
using VideoForensics.Providers.Common.Contracts;

using Xunit;

namespace VideoForensics.Hosting.Tests
{
    public class RemoteEventAndConfigServiceTests
    {
        /// <summary>
        /// Minimal HttpMessageHandler for testing HTTP-based services.
        /// Captures requests and returns configured responses.
        /// </summary>
        private class TestHttpMessageHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _handler;

            public TestHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler)
            {
                _handler = handler;
            }

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Task<HttpResponseMessage> handlerTask = _handler(request);

                _ = await Task.WhenAny(handlerTask, Task.Delay(Timeout.Infinite, cancellationToken));
                cancellationToken.ThrowIfCancellationRequested();
                return await handlerTask;
            }
        }

        private HttpClient CreateHttpClient(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler)
        {
            return new HttpClient(new TestHttpMessageHandler(handler))
            {
                BaseAddress = new Uri("https://example.test")
            };
        }

        [Fact]
        public async Task GetEventsAsync_WithValidDeviceId_CallsCorrectRoute()
        {
            // Arrange
            string capturedRoute = "";
            string capturedMethod = "";
            HttpClient httpClient = CreateHttpClient(request =>
            {
                capturedRoute = request.RequestUri?.PathAndQuery ?? "";
                capturedMethod = request.Method.Method;
                var dto = new List<DeviceEventDto>
                {
                    new("event1", "device123", "motion", DateTime.UtcNow, null, null)
                };
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(JsonSerializer.Serialize(dto))
                };
                response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
                return Task.FromResult(response);
            });

            var service = new RemoteEventAndConfigService(httpClient);
            DateTime startDate = DateTime.UtcNow.AddDays(-7);
            DateTime endDate = DateTime.UtcNow;

            // Act
            _ = await service.GetEventsAsync("device123", startDate, endDate);

            // Assert
            Assert.Equal("GET", capturedMethod);
            Assert.StartsWith("/api/v1/discovery/devices/device123/events", capturedRoute);
        }

        [Fact]
        public async Task GetEventsAsync_WithEventType_IncludesEventTypeInQuery()
        {
            // Arrange
            string capturedRoute = "";
            HttpClient httpClient = CreateHttpClient(request =>
            {
                capturedRoute = request.RequestUri?.PathAndQuery ?? "";
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(JsonSerializer.Serialize(new List<DeviceEventDto>()))
                };
                response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
                return Task.FromResult(response);
            });

            var service = new RemoteEventAndConfigService(httpClient);
            DateTime startDate = DateTime.UtcNow.AddDays(-7);
            DateTime endDate = DateTime.UtcNow;

            // Act
            _ = await service.GetEventsAsync("device123", startDate, endDate, "motion");

            // Assert
            Assert.Contains("eventType=motion", capturedRoute);
        }

        [Fact]
        public async Task GetEventsAsync_ObservesCancellationToken()
        {
            // Arrange
            var cts = new CancellationTokenSource();
            var tcs = new TaskCompletionSource<HttpResponseMessage>();
            HttpClient httpClient = CreateHttpClient(_ => tcs.Task);
            var service = new RemoteEventAndConfigService(httpClient);

            // Act & Assert
            Task<IReadOnlyList<DeviceEvent>> task = service.GetEventsAsync("device123", DateTime.UtcNow.AddDays(-7), DateTime.UtcNow, cancellationToken: cts.Token);
            cts.Cancel();

            _ = await Assert.ThrowsAsync<TaskCanceledException>(() => task);
        }

        [Fact]
        public async Task GetEventsAsync_DeserializesAndMapsDtos()
        {
            // Arrange
            DateTime startTime = DateTime.UtcNow;
            var eventDtos = new List<DeviceEventDto>
            {
                new("event1", "device123", "motion", startTime, "https://example.com/snap.jpg", new Dictionary<string, string> { { "key", "value" } }),
                new("event2", "device123", "person", startTime.AddMinutes(1), null, null)
            };

            HttpClient httpClient = CreateHttpClient(_ =>
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(JsonSerializer.Serialize(eventDtos))
                };
                response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
                return Task.FromResult(response);
            });

            var service = new RemoteEventAndConfigService(httpClient);

            // Act
            IReadOnlyList<DeviceEvent> result = await service.GetEventsAsync("device123", DateTime.UtcNow.AddDays(-7), DateTime.UtcNow);

            // Assert
            Assert.Equal(2, result.Count);
            Assert.Equal("event1", result[0].Id);
            Assert.Equal("device123", result[0].DeviceId);
            Assert.Equal("motion", result[0].EventType);
            Assert.Equal(startTime, result[0].Timestamp);
            Assert.Equal("https://example.com/snap.jpg", result[0].SnapshotUrl);
            Assert.NotNull(result[0].Metadata);
            Assert.Equal("value", result[0].Metadata["key"]);
        }

        [Fact]
        public async Task GetEventsAsync_EmptyListResponse_ReturnsEmptyList()
        {
            // Arrange
            HttpClient httpClient = CreateHttpClient(_ =>
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(JsonSerializer.Serialize(new List<DeviceEventDto>()))
                };
                response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
                return Task.FromResult(response);
            });

            var service = new RemoteEventAndConfigService(httpClient);

            // Act
            IReadOnlyList<DeviceEvent> result = await service.GetEventsAsync("device123", DateTime.UtcNow.AddDays(-7), DateTime.UtcNow);

            // Assert
            Assert.Empty(result);
        }

        [Fact]
        public async Task GetDeviceConfigAsync_WithValidDeviceId_CallsCorrectRoute()
        {
            // Arrange
            string capturedRoute = "";
            string capturedMethod = "";
            HttpClient httpClient = CreateHttpClient(request =>
            {
                capturedRoute = request.RequestUri?.PathAndQuery ?? "";
                capturedMethod = request.Method.Method;
                var dto = new DeviceConfigDto("device123", true, 50, "motion", null);
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(JsonSerializer.Serialize(dto))
                };
                response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
                return Task.FromResult(response);
            });

            var service = new RemoteEventAndConfigService(httpClient);

            // Act
            _ = await service.GetDeviceConfigAsync("device123");

            // Assert
            Assert.Equal("GET", capturedMethod);
            Assert.Equal("/api/v1/discovery/devices/device123/config", capturedRoute);
        }

        [Fact]
        public async Task GetDeviceConfigAsync_OnNotFound_ReturnsNull()
        {
            // Arrange
            HttpClient httpClient = CreateHttpClient(_ =>
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
            });

            var service = new RemoteEventAndConfigService(httpClient);

            // Act
            DeviceConfig? result = await service.GetDeviceConfigAsync("device123");

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task GetDeviceConfigAsync_DeserializesAndMapsDto()
        {
            // Arrange
            var customSettings = new Dictionary<string, object> { { "setting1", "value1" } };
            var dto = new DeviceConfigDto("device123", true, 75, "always", customSettings);

            HttpClient httpClient = CreateHttpClient(_ =>
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(JsonSerializer.Serialize(dto))
                };
                response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
                return Task.FromResult(response);
            });

            var service = new RemoteEventAndConfigService(httpClient);

            // Act
            DeviceConfig? result = await service.GetDeviceConfigAsync("device123");

            // Assert
            Assert.NotNull(result);
            Assert.Equal("device123", result!.DeviceId);
            Assert.True(result.MotionDetectionEnabled);
            Assert.Equal(75, result.MotionSensitivity);
            Assert.Equal("always", result.RecordingMode);
            Assert.NotNull(result.CustomSettings);
            Assert.Equal("value1", result.CustomSettings["setting1"]?.ToString());
        }

        [Fact]
        public async Task GetDeviceConfigAsync_ObservesCancellationToken()
        {
            // Arrange
            var cts = new CancellationTokenSource();
            var tcs = new TaskCompletionSource<HttpResponseMessage>();
            HttpClient httpClient = CreateHttpClient(_ => tcs.Task);
            var service = new RemoteEventAndConfigService(httpClient);

            // Act & Assert
            Task<DeviceConfig?> task = service.GetDeviceConfigAsync("device123", cts.Token);
            cts.Cancel();

            _ = await Assert.ThrowsAsync<TaskCanceledException>(() => task);
        }

        [Fact]
        public async Task UpdateDeviceConfigAsync_WithValidConfig_CallsCorrectRoute()
        {
            // Arrange
            string capturedRoute = "";
            string capturedMethod = "";
            HttpClient httpClient = CreateHttpClient(request =>
            {
                capturedRoute = request.RequestUri?.PathAndQuery ?? "";
                capturedMethod = request.Method.Method;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
            });

            var service = new RemoteEventAndConfigService(httpClient);
            var config = new DeviceConfig("device123", true, 50, "motion", null);

            // Act
            _ = await service.UpdateDeviceConfigAsync("device123", config);

            // Assert
            Assert.Equal("PUT", capturedMethod);
            Assert.Equal("/api/v1/discovery/devices/device123/config", capturedRoute);
        }

        [Fact]
        public async Task UpdateDeviceConfigAsync_OnSuccess_ReturnsTrue()
        {
            // Arrange
            HttpClient httpClient = CreateHttpClient(_ =>
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
            });

            var service = new RemoteEventAndConfigService(httpClient);
            var config = new DeviceConfig("device123", true, 50, "motion", null);

            // Act
            bool result = await service.UpdateDeviceConfigAsync("device123", config);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task UpdateDeviceConfigAsync_OnFailure_ReturnsFalse()
        {
            // Arrange
            HttpClient httpClient = CreateHttpClient(_ =>
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest));
            });

            var service = new RemoteEventAndConfigService(httpClient);
            var config = new DeviceConfig("device123", true, 50, "motion", null);

            // Act
            bool result = await service.UpdateDeviceConfigAsync("device123", config);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task UpdateDeviceConfigAsync_SerializesConfigDto()
        {
            // Arrange
            DeviceConfigDto? capturedDto = null;
            HttpClient httpClient = CreateHttpClient(async request =>
            {
                string content = await request.Content!.ReadAsStringAsync();
                capturedDto = JsonSerializer.Deserialize<DeviceConfigDto>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                return new HttpResponseMessage(HttpStatusCode.OK);
            });

            var service = new RemoteEventAndConfigService(httpClient);
            var config = new DeviceConfig("device123", false, 25, "off", new Dictionary<string, object> { { "key", "val" } });

            // Act
            _ = await service.UpdateDeviceConfigAsync("device123", config);

            // Assert
            Assert.NotNull(capturedDto);
            Assert.Equal("device123", capturedDto!.DeviceId);
            Assert.False(capturedDto.MotionDetectionEnabled);
            Assert.Equal(25, capturedDto.MotionSensitivity);
            Assert.Equal("off", capturedDto.RecordingMode);
        }

        [Fact]
        public async Task UpdateDeviceConfigAsync_ObservesCancellationToken()
        {
            // Arrange
            var cts = new CancellationTokenSource();
            var tcs = new TaskCompletionSource<HttpResponseMessage>();
            HttpClient httpClient = CreateHttpClient(_ => tcs.Task);
            var service = new RemoteEventAndConfigService(httpClient);
            var config = new DeviceConfig("device123", true, 50, "motion", null);

            // Act & Assert
            Task<bool> task = service.UpdateDeviceConfigAsync("device123", config, cts.Token);
            cts.Cancel();

            _ = await Assert.ThrowsAsync<TaskCanceledException>(() => task);
        }
    }
}
