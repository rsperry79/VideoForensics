using System.Net;
using System.Text.Json;
using Xunit;
using VideoForensics.Api.Contracts;
using VideoForensics.Data.Core.Contracts;
using VideoForensics.Data.Core.Models;
using VideoForensics.Hosting.Remote;

namespace VideoForensics.Hosting.Tests
{
    public class RemoteReportGenerationServiceTests
    {
        private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

        [Fact]
        public async Task BuildEvidenceReviewAsync_CallsCorrectEndpoint()
        {
            string? capturedUri = null;
            var handler = new CaptureHttpMessageHandler(async (request, ct) =>
            {
                capturedUri = request.RequestUri?.ToString();
                var response = new HttpResponseMessage(HttpStatusCode.OK);
                var dto = new EvidenceReviewReportDto(DateTime.UtcNow, DateTime.UtcNow.AddDays(-7), DateTime.UtcNow, [], [], 0, 0, 0);
                response.Content = new StringContent(JsonSerializer.Serialize(dto), System.Text.Encoding.UTF8, "application/json");
                return response;
            });

            var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test") };
            var service = new RemoteReportGenerationService(httpClient);

            var result = await service.BuildEvidenceReviewAsync(null, DateTime.UtcNow.AddDays(-7), DateTime.UtcNow, CancellationToken.None);

            Assert.EndsWith("/api/v1/reports/evidence-review", capturedUri?.Split('?')[0]);
            Assert.NotNull(result);
        }

        [Fact]
        public async Task BuildEvidenceReviewAsync_IncludesDeviceIdInQueryString()
        {
            string? capturedUri = null;
            var deviceId = Guid.NewGuid();

            var handler = new CaptureHttpMessageHandler(async (request, ct) =>
            {
                capturedUri = request.RequestUri?.ToString();
                var response = new HttpResponseMessage(HttpStatusCode.OK);
                var dto = new EvidenceReviewReportDto(DateTime.UtcNow, DateTime.UtcNow.AddDays(-1), DateTime.UtcNow, [], [], 0, 0, 0);
                response.Content = new StringContent(JsonSerializer.Serialize(dto), System.Text.Encoding.UTF8, "application/json");
                return response;
            });

            var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test") };
            var service = new RemoteReportGenerationService(httpClient);

            await service.BuildEvidenceReviewAsync(deviceId, DateTime.UtcNow.AddDays(-1), DateTime.UtcNow, CancellationToken.None);

            Assert.Contains($"deviceId={deviceId:D}", capturedUri);
        }

        [Fact]
        public async Task BuildEvidenceReviewAsync_IncludesDateRangeInQueryString()
        {
            string? capturedUri = null;
            var fromUtc = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var toUtc = new DateTime(2024, 1, 31, 23, 59, 59, DateTimeKind.Utc);

            var handler = new CaptureHttpMessageHandler(async (request, ct) =>
            {
                capturedUri = request.RequestUri?.ToString();
                var response = new HttpResponseMessage(HttpStatusCode.OK);
                var dto = new EvidenceReviewReportDto(DateTime.UtcNow, fromUtc, toUtc, [], [], 0, 0, 0);
                response.Content = new StringContent(JsonSerializer.Serialize(dto), System.Text.Encoding.UTF8, "application/json");
                return response;
            });

            var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test") };
            var service = new RemoteReportGenerationService(httpClient);

            await service.BuildEvidenceReviewAsync(null, fromUtc, toUtc, CancellationToken.None);

            Assert.Contains("fromUtc", capturedUri);
            Assert.Contains("toUtc", capturedUri);
        }

        [Fact]
        public async Task BuildEvidenceReviewAsync_WithNullResponse_ThrowsInvalidOperationException()
        {
            var handler = new CaptureHttpMessageHandler(async (request, ct) =>
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK);
                response.Content = new StringContent("null", System.Text.Encoding.UTF8, "application/json");
                return response;
            });

            var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test") };
            var service = new RemoteReportGenerationService(httpClient);

            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                await service.BuildEvidenceReviewAsync(null, DateTime.UtcNow, DateTime.UtcNow, CancellationToken.None);
            });
        }

        [Fact]
        public async Task BuildEvidenceReviewAsync_WithCancellationToken_PassesCancellationToRequest()
        {
            bool cancellationObserved = false;

            var handler = new CaptureHttpMessageHandler(async (request, ct) =>
            {
                cancellationObserved = !ct.IsCancellationRequested;
                var response = new HttpResponseMessage(HttpStatusCode.OK);
                var dto = new EvidenceReviewReportDto(DateTime.UtcNow, DateTime.UtcNow.AddDays(-1), DateTime.UtcNow, [], [], 0, 0, 0);
                response.Content = new StringContent(JsonSerializer.Serialize(dto), System.Text.Encoding.UTF8, "application/json");
                return response;
            });

            var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test") };
            var service = new RemoteReportGenerationService(httpClient);

            await service.BuildEvidenceReviewAsync(null, DateTime.UtcNow, DateTime.UtcNow, new CancellationTokenSource().Token);

            Assert.True(cancellationObserved);
        }

        [Fact]
        public async Task BuildForensicAnalysisReportAsync_CallsCorrectEndpoint()
        {
            string? capturedUri = null;

            var handler = new CaptureHttpMessageHandler(async (request, ct) =>
            {
                capturedUri = request.RequestUri?.ToString();
                var response = new HttpResponseMessage(HttpStatusCode.OK);
                var dto = new ForensicAnalysisReportDto(DateTime.UtcNow, DateTime.UtcNow.AddDays(-1), DateTime.UtcNow, [], [], [], "Test summary");
                response.Content = new StringContent(JsonSerializer.Serialize(dto), System.Text.Encoding.UTF8, "application/json");
                return response;
            });

            var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test") };
            var service = new RemoteReportGenerationService(httpClient);

            await service.BuildForensicAnalysisReportAsync(null, DateTime.UtcNow.AddDays(-1), DateTime.UtcNow, CancellationToken.None);

            Assert.EndsWith("/api/v1/reports/forensic-analysis", capturedUri?.Split('?')[0]);
        }

        [Fact]
        public async Task BuildSignalAnomalyReportAsync_CallsCorrectEndpoint()
        {
            string? capturedUri = null;

            var handler = new CaptureHttpMessageHandler(async (request, ct) =>
            {
                capturedUri = request.RequestUri?.ToString();
                var response = new HttpResponseMessage(HttpStatusCode.OK);
                var dto = new SignalAnomalyReportDto(DateTime.UtcNow, DateTime.UtcNow.AddDays(-1), DateTime.UtcNow, [], []);
                response.Content = new StringContent(JsonSerializer.Serialize(dto), System.Text.Encoding.UTF8, "application/json");
                return response;
            });

            var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test") };
            var service = new RemoteReportGenerationService(httpClient);

            await service.BuildSignalAnomalyReportAsync(null, DateTime.UtcNow.AddDays(-1), DateTime.UtcNow, CancellationToken.None);

            Assert.EndsWith("/api/v1/reports/signal-anomaly", capturedUri?.Split('?')[0]);
        }

        [Fact]
        public async Task BuildAccessControlReportAsync_CallsCorrectEndpoint()
        {
            string? capturedUri = null;

            var handler = new CaptureHttpMessageHandler(async (request, ct) =>
            {
                capturedUri = request.RequestUri?.ToString();
                var response = new HttpResponseMessage(HttpStatusCode.OK);
                var dto = new AccessControlReportDto(DateTime.UtcNow, DateTime.UtcNow.AddDays(-1), DateTime.UtcNow, [], []);
                response.Content = new StringContent(JsonSerializer.Serialize(dto), System.Text.Encoding.UTF8, "application/json");
                return response;
            });

            var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test") };
            var service = new RemoteReportGenerationService(httpClient);

            await service.BuildAccessControlReportAsync(null, DateTime.UtcNow.AddDays(-1), DateTime.UtcNow, CancellationToken.None);

            Assert.EndsWith("/api/v1/reports/access-control", capturedUri?.Split('?')[0]);
        }

        [Fact]
        public async Task BuildChainOfCustodyReportAsync_CallsCorrectEndpoint()
        {
            string? capturedUri = null;

            var handler = new CaptureHttpMessageHandler(async (request, ct) =>
            {
                capturedUri = request.RequestUri?.ToString();
                var response = new HttpResponseMessage(HttpStatusCode.OK);
                var dto = new ChainOfCustodyReportDto(DateTime.UtcNow, DateTime.UtcNow.AddDays(-1), DateTime.UtcNow, [], true, "valid");
                response.Content = new StringContent(JsonSerializer.Serialize(dto), System.Text.Encoding.UTF8, "application/json");
                return response;
            });

            var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test") };
            var service = new RemoteReportGenerationService(httpClient);

            await service.BuildChainOfCustodyReportAsync(null, DateTime.UtcNow.AddDays(-1), DateTime.UtcNow, CancellationToken.None);

            Assert.EndsWith("/api/v1/reports/chain-of-custody", capturedUri?.Split('?')[0]);
        }

        [Fact]
        public async Task WriteReportAsync_CallsCorrectEndpointWithPost()
        {
            string? capturedUri = null;
            string? capturedMethod = null;
            string? requestBody = null;

            var handler = new CaptureHttpMessageHandler(async (request, ct) =>
            {
                capturedUri = request.RequestUri?.ToString();
                capturedMethod = request.Method?.ToString();
                requestBody = await request.Content!.ReadAsStringAsync();
                return new HttpResponseMessage(HttpStatusCode.OK);
            });

            var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test") };
            var service = new RemoteReportGenerationService(httpClient);

            var reportDto = new { Title = "Test Report", Data = "test data" };
            await service.WriteReportAsync(reportDto, "json", CancellationToken.None);

            Assert.Equal("POST", capturedMethod);
            Assert.EndsWith("/api/v1/reports/write", capturedUri);
            Assert.Contains("json", requestBody);
        }

        [Fact]
        public async Task WriteReportAsync_IncludesFormatInRequest()
        {
            string? requestBody = null;

            var handler = new CaptureHttpMessageHandler(async (request, ct) =>
            {
                requestBody = await request.Content!.ReadAsStringAsync();
                return new HttpResponseMessage(HttpStatusCode.OK);
            });

            var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test") };
            var service = new RemoteReportGenerationService(httpClient);

            await service.WriteReportAsync(new { }, "xml", CancellationToken.None);

            Assert.Contains("xml", requestBody);
        }

        [Fact]
        public async Task WriteReportAsync_WithCancellationToken_PassesCancellationToRequest()
        {
            bool cancellationObserved = false;

            var handler = new CaptureHttpMessageHandler(async (request, ct) =>
            {
                cancellationObserved = !ct.IsCancellationRequested;
                return new HttpResponseMessage(HttpStatusCode.OK);
            });

            var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test") };
            var service = new RemoteReportGenerationService(httpClient);

            await service.WriteReportAsync(new { }, "json", new CancellationTokenSource().Token);

            Assert.True(cancellationObserved);
        }

        [Fact]
        public async Task WriteReportAsync_WithHttpError_ThrowsHttpRequestException()
        {
            var handler = new CaptureHttpMessageHandler(async (request, ct) =>
            {
                return new HttpResponseMessage(HttpStatusCode.InternalServerError);
            });

            var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test") };
            var service = new RemoteReportGenerationService(httpClient);

            await Assert.ThrowsAsync<HttpRequestException>(async () =>
            {
                await service.WriteReportAsync(new { }, "json", CancellationToken.None);
            });
        }

        private class CaptureHttpMessageHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handler;

            public CaptureHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
            {
                _handler = handler;
            }

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                return await _handler(request, cancellationToken);
            }
        }
    }
}
