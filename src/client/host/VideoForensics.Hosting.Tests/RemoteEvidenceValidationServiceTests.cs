using System.Net;
using System.Text.Json;

using VideoForensics.Api.Contracts;
using VideoForensics.Client.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Hosting.Remote;

using Xunit;

namespace VideoForensics.Hosting.Tests
{
    public class RemoteEvidenceValidationServiceTests
    {
        private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

        [Fact]
        public async Task VerifyLocalIntegrityAsync_WithoutDeviceId_CallsCorrectEndpoint()
        {
            string? capturedUri = null;
            var handler = new CaptureHttpMessageHandler(async (request, ct) =>
            {
                capturedUri = request.RequestUri?.ToString();
                var response = new HttpResponseMessage(HttpStatusCode.OK);
                var dtos = new List<MediaVerificationResultDto>
                {
                    new(Guid.NewGuid(), "video1.mp4", "verified", null),
                    new(Guid.NewGuid(), "video2.mp4", "failed", "Hash mismatch")
                };
                response.Content = new StringContent(JsonSerializer.Serialize(dtos), System.Text.Encoding.UTF8, "application/json");
                return response;
            });

            var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test") };
            var service = new RemoteEvidenceValidationService(httpClient);

            IReadOnlyList<MediaVerificationResult> results = await service.VerifyLocalIntegrityAsync(null, CancellationToken.None);

            Assert.EndsWith("/api/v1/evidence/verify-local-integrity", capturedUri);
            Assert.Equal(2, results.Count);
        }

        [Fact]
        public async Task VerifyLocalIntegrityAsync_WithDeviceId_IncludesDeviceIdInRequest()
        {
            string? requestBody = null;
            var deviceId = Guid.NewGuid();

            var handler = new CaptureHttpMessageHandler(async (request, ct) =>
            {
                requestBody = await request.Content!.ReadAsStringAsync();
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("[]", System.Text.Encoding.UTF8, "application/json")
                };
                return response;
            });

            var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test") };
            var service = new RemoteEvidenceValidationService(httpClient);

            _ = await service.VerifyLocalIntegrityAsync(deviceId, CancellationToken.None);

            Assert.NotNull(requestBody);
            Assert.Contains(deviceId.ToString("D"), requestBody);
        }

        [Fact]
        public async Task VerifyLocalIntegrityAsync_MapsResponseCorrectly()
        {
            var id1 = Guid.NewGuid();
            var id2 = Guid.NewGuid();

            var handler = new CaptureHttpMessageHandler(async (request, ct) =>
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK);
                var dtos = new List<MediaVerificationResultDto>
                {
                    new(id1, "video1.mp4", "verified", null),
                    new(id2, "video2.mp4", "failed", "Hash mismatch")
                };
                response.Content = new StringContent(JsonSerializer.Serialize(dtos), System.Text.Encoding.UTF8, "application/json");
                return response;
            });

            var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test") };
            var service = new RemoteEvidenceValidationService(httpClient);

            IReadOnlyList<MediaVerificationResult> results = await service.VerifyLocalIntegrityAsync(null, CancellationToken.None);

            Assert.Equal(id1, results[0].MediaItemId);
            Assert.Equal("verified", results[0].Status);
            Assert.Null(results[0].FailureReason);
        }

        [Fact]
        public async Task VerifyLocalIntegrityAsync_WithNullResponse_ReturnsEmptyList()
        {
            var handler = new CaptureHttpMessageHandler(async (request, ct) =>
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("null", System.Text.Encoding.UTF8, "application/json")
                };
                return response;
            });

            var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test") };
            var service = new RemoteEvidenceValidationService(httpClient);

            IReadOnlyList<MediaVerificationResult> results = await service.VerifyLocalIntegrityAsync(null, CancellationToken.None);

            Assert.Empty(results);
        }

        [Fact]
        public async Task VerifyLocalIntegrityAsync_WithCancellationToken_PassesCancellationToRequest()
        {
            bool cancellationObserved = false;

            var handler = new CaptureHttpMessageHandler(async (request, ct) =>
            {
                cancellationObserved = !ct.IsCancellationRequested;
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("[]", System.Text.Encoding.UTF8, "application/json")
                };
                return response;
            });

            var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test") };
            var service = new RemoteEvidenceValidationService(httpClient);

            _ = await service.VerifyLocalIntegrityAsync(null, new CancellationTokenSource().Token);

            Assert.True(cancellationObserved);
        }

        [Fact]
        public async Task ReconcileWithProviderAsync_CallsCorrectEndpoint()
        {
            string? capturedUri = null;
            var deviceId = Guid.NewGuid();

            var handler = new CaptureHttpMessageHandler(async (request, ct) =>
            {
                capturedUri = request.RequestUri?.ToString();
                var response = new HttpResponseMessage(HttpStatusCode.OK);
                var dtos = new List<ReconciliationDiscrepancyDto>
                {
                    new(DiscrepancyType.MetadataChanged, "event-123", "timestamp", "2024-01-15T10:00:00Z", "2024-01-15T10:05:00Z"),
                    new(DiscrepancyType.MissingFromProvider, "event-456", null, null, null)
                };
                response.Content = new StringContent(JsonSerializer.Serialize(dtos), System.Text.Encoding.UTF8, "application/json");
                return response;
            });

            var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test") };
            var service = new RemoteEvidenceValidationService(httpClient);

            IReadOnlyList<ReconciliationDiscrepancy> results = await service.ReconcileWithProviderAsync(deviceId, "ring-device-123", DateTime.UtcNow.AddDays(-1), DateTime.UtcNow, CancellationToken.None);

            Assert.EndsWith("/api/v1/evidence/reconcile", capturedUri);
            Assert.Equal(2, results.Count);
        }

        [Fact]
        public async Task ReconcileWithProviderAsync_MapsDiscrepanciesCorrectly()
        {
            var deviceId = Guid.NewGuid();

            var handler = new CaptureHttpMessageHandler(async (request, ct) =>
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK);
                var dtos = new List<ReconciliationDiscrepancyDto>
                {
                    new(DiscrepancyType.MetadataChanged, "event-123", "duration_seconds", "300", "400"),
                    new(DiscrepancyType.MissingFromProvider, "event-456", null, null, null)
                };
                response.Content = new StringContent(JsonSerializer.Serialize(dtos), System.Text.Encoding.UTF8, "application/json");
                return response;
            });

            var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test") };
            var service = new RemoteEvidenceValidationService(httpClient);

            IReadOnlyList<ReconciliationDiscrepancy> results = await service.ReconcileWithProviderAsync(deviceId, "ring-device-123", DateTime.UtcNow.AddDays(-1), DateTime.UtcNow, CancellationToken.None);

            Assert.Equal(DiscrepancyType.MetadataChanged, results[0].Type);
            Assert.Equal("event-123", results[0].ProviderEventId);
        }

        [Fact]
        public async Task ReconcileWithProviderAsync_IncludesRequestParametersInBody()
        {
            string? requestBody = null;
            var deviceId = Guid.NewGuid();

            var handler = new CaptureHttpMessageHandler(async (request, ct) =>
            {
                requestBody = await request.Content!.ReadAsStringAsync();
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("[]", System.Text.Encoding.UTF8, "application/json")
                };
                return response;
            });

            var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test") };
            var service = new RemoteEvidenceValidationService(httpClient);

            _ = await service.ReconcileWithProviderAsync(deviceId, "ring-device-123", DateTime.UtcNow.AddDays(-1), DateTime.UtcNow, CancellationToken.None);

            Assert.Contains(deviceId.ToString("D"), requestBody);
            Assert.Contains("ring-device-123", requestBody);
        }

        [Fact]
        public async Task ReconcileWithProviderAsync_WithNullResponse_ReturnsEmptyList()
        {
            var handler = new CaptureHttpMessageHandler(async (request, ct) =>
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("null", System.Text.Encoding.UTF8, "application/json")
                };
                return response;
            });

            var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test") };
            var service = new RemoteEvidenceValidationService(httpClient);

            IReadOnlyList<ReconciliationDiscrepancy> results = await service.ReconcileWithProviderAsync(Guid.NewGuid(), "device-id", DateTime.UtcNow, DateTime.UtcNow.AddDays(1), CancellationToken.None);

            Assert.Empty(results);
        }

        [Fact]
        public async Task ReconcileWithProviderAsync_WithCancellationToken_PassesCancellationToRequest()
        {
            bool cancellationObserved = false;

            var handler = new CaptureHttpMessageHandler(async (request, ct) =>
            {
                cancellationObserved = !ct.IsCancellationRequested;
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("[]", System.Text.Encoding.UTF8, "application/json")
                };
                return response;
            });

            var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test") };
            var service = new RemoteEvidenceValidationService(httpClient);

            _ = await service.ReconcileWithProviderAsync(Guid.NewGuid(), "device-id", DateTime.UtcNow, DateTime.UtcNow.AddDays(1), new CancellationTokenSource().Token);

            Assert.True(cancellationObserved);
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
