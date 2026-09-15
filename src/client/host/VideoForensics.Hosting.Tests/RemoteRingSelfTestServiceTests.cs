using System.Net;
using System.Text.Json;
using Xunit;
using VideoForensics.Api.Contracts;
using VideoForensics.Hosting.Remote;

namespace VideoForensics.Hosting.Tests
{
    public class RemoteRingSelfTestServiceTests
    {
        /// <summary>
        /// Minimal HttpMessageHandler for testing HTTP-based services.
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
                Task completed = await Task.WhenAny(handlerTask, Task.Delay(Timeout.Infinite, cancellationToken));
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
        public async Task ListEndpointsAsync_WithValidResponse_CallsCorrectRoute()
        {
            // Arrange
            string capturedRoute = "";
            string capturedMethod = "";
            var endpoints = new List<SelfTestEndpointDto>
            {
                new("endpoint1", "Test 1", "Tests something", "Session", "GET", "/api/test1", "PerLocation", false, false),
                new("endpoint2", "Test 2", "Tests another", "Session", "POST", "/api/test2", "PerDoorbot", true, false)
            };

            var httpClient = CreateHttpClient(request =>
            {
                capturedRoute = request.RequestUri?.PathAndQuery ?? "";
                capturedMethod = request.Method.Method;
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(JsonSerializer.Serialize(endpoints))
                };
                response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
                return Task.FromResult(response);
            });

            var service = new RemoteRingSelfTestService(httpClient);

            // Act
            _ = await service.ListEndpointsAsync();

            // Assert
            Assert.Equal("GET", capturedMethod);
            Assert.Equal("/api/v1/selftest", capturedRoute);
        }

        [Fact]
        public async Task ListEndpointsAsync_DeserializesEndpoints()
        {
            // Arrange
            var endpoints = new List<SelfTestEndpointDto>
            {
                new("endpoint1", "Test 1", "Desc 1", "Session", "GET", "/api/test1", "PerLocation", false, false),
                new("endpoint2", "Test 2", "Desc 2", "Session", "POST", "/api/test2", "PerDoorbot", true, true)
            };

            var httpClient = CreateHttpClient(_ =>
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(JsonSerializer.Serialize(endpoints))
                };
                response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
                return Task.FromResult(response);
            });

            var service = new RemoteRingSelfTestService(httpClient);

            // Act
            var result = await service.ListEndpointsAsync();

            // Assert
            Assert.Equal(2, result.Count);
            Assert.Equal("endpoint1", result[0].Key);
            Assert.Equal("Test 1", result[0].DisplayName);
            Assert.False(result[0].Destructive);
            Assert.False(result[0].Physical);
            Assert.Equal("endpoint2", result[1].Key);
            Assert.True(result[1].Destructive);
            Assert.True(result[1].Physical);
        }

        [Fact]
        public async Task ListEndpointsAsync_EmptyResponse_ReturnsEmptyList()
        {
            // Arrange
            var httpClient = CreateHttpClient(_ =>
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(JsonSerializer.Serialize(new List<SelfTestEndpointDto>()))
                };
                response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
                return Task.FromResult(response);
            });

            var service = new RemoteRingSelfTestService(httpClient);

            // Act
            var result = await service.ListEndpointsAsync();

            // Assert
            Assert.Empty(result);
        }

        [Fact]
        public async Task ListEndpointsAsync_ObservesCancellationToken()
        {
            // Arrange
            var cts = new CancellationTokenSource();
            var tcs = new TaskCompletionSource<HttpResponseMessage>();
            var httpClient = CreateHttpClient(_ => tcs.Task);
            var service = new RemoteRingSelfTestService(httpClient);

            // Act & Assert
            var task = service.ListEndpointsAsync(cts.Token);
            cts.Cancel();

            await Assert.ThrowsAsync<TaskCanceledException>(() => task);
        }

        [Fact]
        public async Task StartRunAsync_OnAccepted_ReturnsAcceptedTrue()
        {
            // Arrange
            var httpClient = CreateHttpClient(_ =>
            {
                var response = new HttpResponseMessage(HttpStatusCode.Accepted)
                {
                    Content = new StringContent(JsonSerializer.Serialize(new SelfTestRunResponseDto(true)))
                };
                response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
                return Task.FromResult(response);
            });

            var service = new RemoteRingSelfTestService(httpClient);
            var request = new SelfTestRunRequestDto(new List<string> { "endpoint1" });

            // Act
            var result = await service.StartRunAsync(request);

            // Assert
            Assert.True(result.Accepted);
            Assert.Null(result.Error);
        }

        [Fact]
        public async Task StartRunAsync_On202Accepted_CallsCorrectRoute()
        {
            // Arrange
            string capturedRoute = "";
            string capturedMethod = "";
            var httpClient = CreateHttpClient(request =>
            {
                capturedRoute = request.RequestUri?.PathAndQuery ?? "";
                capturedMethod = request.Method.Method;
                var response = new HttpResponseMessage(HttpStatusCode.Accepted)
                {
                    Content = new StringContent(JsonSerializer.Serialize(new SelfTestRunResponseDto(true)))
                };
                response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
                return Task.FromResult(response);
            });

            var service = new RemoteRingSelfTestService(httpClient);
            var request = new SelfTestRunRequestDto(new List<string> { "endpoint1" });

            // Act
            _ = await service.StartRunAsync(request);

            // Assert
            Assert.Equal("POST", capturedMethod);
            Assert.Equal("/api/v1/selftest/run", capturedRoute);
        }

        [Fact]
        public async Task StartRunAsync_OnConflict_ReturnsRejectionWithError()
        {
            // Arrange
            var httpClient = CreateHttpClient(_ =>
            {
                var response = new HttpResponseMessage(HttpStatusCode.Conflict)
                {
                    Content = new StringContent(JsonSerializer.Serialize(
                        new SelfTestRunResponseDto(false, "A test is already running")
                    ))
                };
                response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
                return Task.FromResult(response);
            });

            var service = new RemoteRingSelfTestService(httpClient);
            var request = new SelfTestRunRequestDto(new List<string> { "endpoint1" });

            // Act
            var result = await service.StartRunAsync(request);

            // Assert
            Assert.False(result.Accepted);
            Assert.Equal("A test is already running", result.Error);
        }

        [Fact]
        public async Task StartRunAsync_OnConflictWithoutBody_ReturnsDefaultMessage()
        {
            // Arrange
            var httpClient = CreateHttpClient(_ =>
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Conflict));
            });

            var service = new RemoteRingSelfTestService(httpClient);
            var request = new SelfTestRunRequestDto(new List<string> { "endpoint1" });

            // Act
            var result = await service.StartRunAsync(request);

            // Assert
            Assert.False(result.Accepted);
            Assert.NotNull(result.Error);
            Assert.Contains("already in progress", result.Error);
        }

        [Fact]
        public async Task StartRunAsync_OnForbidden_ReturnsPermissionError()
        {
            // Arrange
            var httpClient = CreateHttpClient(_ =>
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Forbidden));
            });

            var service = new RemoteRingSelfTestService(httpClient);
            var request = new SelfTestRunRequestDto(new List<string> { "endpoint1" }, Destructive: true);

            // Act
            var result = await service.StartRunAsync(request);

            // Assert
            Assert.False(result.Accepted);
            Assert.NotNull(result.Error);
            Assert.Contains("permission", result.Error!.ToLower());
            Assert.Contains("destructive", result.Error!.ToLower());
        }

        [Fact]
        public async Task StartRunAsync_OnHttpException_ReturnsErrorResponse()
        {
            // Arrange
            var httpClient = CreateHttpClient(_ =>
            {
                throw new HttpRequestException("Network error");
            });

            var service = new RemoteRingSelfTestService(httpClient);
            var request = new SelfTestRunRequestDto(new List<string> { "endpoint1" });

            // Act
            var result = await service.StartRunAsync(request);

            // Assert
            Assert.False(result.Accepted);
            Assert.NotNull(result.Error);
            Assert.Contains("Failed to start", result.Error);
        }

        [Fact]
        public async Task StartRunAsync_ObservesCancellationToken()
        {
            // Arrange
            var cts = new CancellationTokenSource();
            var tcs = new TaskCompletionSource<HttpResponseMessage>();
            var httpClient = CreateHttpClient(_ => tcs.Task);
            var service = new RemoteRingSelfTestService(httpClient);
            var request = new SelfTestRunRequestDto(new List<string> { "endpoint1" });

            // Act & Assert
            var task = service.StartRunAsync(request, cts.Token);
            cts.Cancel();

            await Assert.ThrowsAsync<TaskCanceledException>(() => task);
        }

        [Fact]
        public async Task GetStatusAsync_WithValidResponse_CallsCorrectRoute()
        {
            // Arrange
            string capturedRoute = "";
            string capturedMethod = "";
            var httpClient = CreateHttpClient(request =>
            {
                capturedRoute = request.RequestUri?.PathAndQuery ?? "";
                capturedMethod = request.Method.Method;
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        JsonSerializer.Serialize(new SelfTestStatusDto(SelfTestRunStatus.Idle))
                    )
                };
                response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
                return Task.FromResult(response);
            });

            var service = new RemoteRingSelfTestService(httpClient);

            // Act
            _ = await service.GetStatusAsync();

            // Assert
            Assert.Equal("GET", capturedMethod);
            Assert.Equal("/api/v1/selftest/status", capturedRoute);
        }

        [Fact]
        public async Task GetStatusAsync_DeserializesStatus()
        {
            // Arrange
            var startTime = DateTime.UtcNow;
            var completedTime = startTime.AddMinutes(5);
            var httpClient = CreateHttpClient(_ =>
            {
                var status = new SelfTestStatusDto(
                    SelfTestRunStatus.Completed,
                    StartedAtUtc: startTime,
                    CompletedAtUtc: completedTime
                );
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(JsonSerializer.Serialize(status))
                };
                response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
                return Task.FromResult(response);
            });

            var service = new RemoteRingSelfTestService(httpClient);

            // Act
            var result = await service.GetStatusAsync();

            // Assert
            Assert.Equal(SelfTestRunStatus.Completed, result.Status);
            Assert.Equal(startTime, result.StartedAtUtc);
            Assert.Equal(completedTime, result.CompletedAtUtc);
        }

        [Fact]
        public async Task GetStatusAsync_WithRunningStatus_ReturnsRunning()
        {
            // Arrange
            var httpClient = CreateHttpClient(_ =>
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        JsonSerializer.Serialize(new SelfTestStatusDto(SelfTestRunStatus.Running))
                    )
                };
                response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
                return Task.FromResult(response);
            });

            var service = new RemoteRingSelfTestService(httpClient);

            // Act
            var result = await service.GetStatusAsync();

            // Assert
            Assert.Equal(SelfTestRunStatus.Running, result.Status);
        }

        [Fact]
        public async Task GetStatusAsync_ObservesCancellationToken()
        {
            // Arrange
            var cts = new CancellationTokenSource();
            var tcs = new TaskCompletionSource<HttpResponseMessage>();
            var httpClient = CreateHttpClient(_ => tcs.Task);
            var service = new RemoteRingSelfTestService(httpClient);

            // Act & Assert
            var task = service.GetStatusAsync(cts.Token);
            cts.Cancel();

            await Assert.ThrowsAsync<TaskCanceledException>(() => task);
        }

        [Fact]
        public async Task GetResultAsync_OnNoContent_ReturnsNull()
        {
            // Arrange
            var httpClient = CreateHttpClient(_ =>
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));
            });

            var service = new RemoteRingSelfTestService(httpClient);

            // Act
            var result = await service.GetResultAsync();

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task GetResultAsync_On204NoContent_CallsCorrectRoute()
        {
            // Arrange
            string capturedRoute = "";
            string capturedMethod = "";
            var httpClient = CreateHttpClient(request =>
            {
                capturedRoute = request.RequestUri?.PathAndQuery ?? "";
                capturedMethod = request.Method.Method;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));
            });

            var service = new RemoteRingSelfTestService(httpClient);

            // Act
            _ = await service.GetResultAsync();

            // Assert
            Assert.Equal("GET", capturedMethod);
            Assert.Equal("/api/v1/selftest/result", capturedRoute);
        }

        [Fact]
        public async Task GetResultAsync_OnSuccess_ReturnsResults()
        {
            // Arrange
            var generatedAt = DateTime.UtcNow;
            var calls = new List<SelfTestCallDto>
            {
                new("endpoint1", "Test 1", "Session", false, false, "device1",
                    generatedAt.AddMinutes(-1), 100, true, null, false, null, null, "Not destructive", new List<string>())
            };
            var summary = new SelfTestSummaryDto(10, 9, 1);
            var result = new SelfTestResultDto("1.0.0", generatedAt, "Database", summary, calls);

            var httpClient = CreateHttpClient(_ =>
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(JsonSerializer.Serialize(result))
                };
                response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
                return Task.FromResult(response);
            });

            var service = new RemoteRingSelfTestService(httpClient);

            // Act
            var retrieved = await service.GetResultAsync();

            // Assert
            Assert.NotNull(retrieved);
            Assert.Equal("1.0.0", retrieved!.ToolVersion);
            Assert.Equal("Database", retrieved.CredentialSource);
            Assert.Equal(10, retrieved.Summary.TotalCalls);
            Assert.Equal(9, retrieved.Summary.Succeeded);
            Assert.Equal(1, retrieved.Summary.Failed);
            Assert.Single(retrieved.Calls);
            Assert.Equal("endpoint1", retrieved.Calls[0].Endpoint);
        }

        [Fact]
        public async Task GetResultAsync_ObservesCancellationToken()
        {
            // Arrange
            var cts = new CancellationTokenSource();
            var tcs = new TaskCompletionSource<HttpResponseMessage>();
            var httpClient = CreateHttpClient(_ => tcs.Task);
            var service = new RemoteRingSelfTestService(httpClient);

            // Act & Assert
            var task = service.GetResultAsync(cts.Token);
            cts.Cancel();

            await Assert.ThrowsAsync<TaskCanceledException>(() => task);
        }

        [Fact]
        public async Task GetResultAsync_On200WithResult_DeserializesCompleteResult()
        {
            // Arrange
            var startTime = DateTime.UtcNow.AddMinutes(-10);
            var call1 = new SelfTestCallDto(
                "endpoint1", "Endpoint 1", "Session", false, false,
                "device1", startTime, 150, true, null, false, null, null, "Not destructive",
                new List<string>()
            );
            var call2 = new SelfTestCallDto(
                "endpoint2", "Endpoint 2", "Session", true, true,
                "device1", startTime.AddSeconds(200), 200, false, "Device offline",
                true, false, "Failed to restore", null,
                new List<string> { "Invalid response schema" }
            );

            var summary = new SelfTestSummaryDto(2, 1, 1);
            var result = new SelfTestResultDto("1.0.0", startTime.AddSeconds(500), "Database", summary, new List<SelfTestCallDto> { call1, call2 });

            var httpClient = CreateHttpClient(_ =>
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(JsonSerializer.Serialize(result))
                };
                response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
                return Task.FromResult(response);
            });

            var service = new RemoteRingSelfTestService(httpClient);

            // Act
            var retrieved = await service.GetResultAsync();

            // Assert
            Assert.NotNull(retrieved);
            Assert.Equal(2, retrieved!.Calls.Count);
            Assert.True(retrieved.Calls[0].Success);
            Assert.False(retrieved.Calls[1].Success);
            Assert.Equal("Device offline", retrieved.Calls[1].Error);
            Assert.True(retrieved.Calls[1].RestoreAttempted);
            Assert.False(retrieved.Calls[1].RestoreSuccess);
        }
    }
}
