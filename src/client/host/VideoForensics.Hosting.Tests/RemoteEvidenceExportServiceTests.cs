using System.Net;
using System.Text.Json;
using Xunit;
using VideoForensics.Api.Contracts;
using VideoForensics.Client.Common.Contracts;
using VideoForensics.Hosting.Remote;

namespace VideoForensics.Hosting.Tests
{
    public class RemoteEvidenceExportServiceTests
    {
        private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

        [Fact]
        public async Task ExportEvidenceAsync_WithValidMediaItems_CallsCorrectEndpoint()
        {
            // Arrange
            var requestCaptured = false;
            string? capturedMethod = null;
            string? capturedUri = null;

            var handler = new CaptureHttpMessageHandler(async (request, ct) =>
            {
                requestCaptured = true;
                capturedMethod = request.Method?.ToString();
                capturedUri = request.RequestUri?.ToString();

                var response = new HttpResponseMessage(HttpStatusCode.OK);
                var dto = new ExportResultDto(
                    Success: true,
                    ArchivePath: "/path/to/archive.zip",
                    ArchiveSha256Hash: "abc123",
                    ItemsIncluded: 2,
                    ItemsExcludedForFailedIntegrity: [],
                    ErrorMessage: null
                );
                response.Content = new StringContent(JsonSerializer.Serialize(dto), System.Text.Encoding.UTF8, "application/json");
                return response;
            });

            var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test") };
            var service = new RemoteEvidenceExportService(httpClient);

            var mediaIds = new[] { Guid.NewGuid(), Guid.NewGuid() };
            var cts = new CancellationTokenSource();

            // Act
            var result = await service.ExportEvidenceAsync(
                mediaIds,
                "/output",
                "CASE-001",
                "Detective Smith",
                "mypassphrase",
                cts.Token);

            // Assert
            Assert.True(requestCaptured);
            Assert.Equal("POST", capturedMethod);
            Assert.EndsWith("/api/v1/evidence/export", capturedUri);
            Assert.True(result.Success);
            Assert.Equal("/path/to/archive.zip", result.ArchivePath);
            Assert.Equal("abc123", result.ArchiveSha256Hash);
            Assert.Equal(2, result.ItemsIncluded);
        }

        [Fact]
        public async Task ExportEvidenceAsync_WithNullResults_ThrowsInvalidOperationException()
        {
            // Arrange
            var handler = new CaptureHttpMessageHandler(async (request, ct) =>
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK);
                response.Content = new StringContent("null", System.Text.Encoding.UTF8, "application/json");
                return response;
            });

            var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test") };
            var service = new RemoteEvidenceExportService(httpClient);

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                await service.ExportEvidenceAsync(
                    new[] { Guid.NewGuid() },
                    "/output",
                    null,
                    null,
                    null,
                    CancellationToken.None);
            });
        }

        [Fact]
        public async Task ExportEvidenceAsync_WithHttpError_ThrowsHttpRequestException()
        {
            // Arrange
            var handler = new CaptureHttpMessageHandler(async (request, ct) =>
            {
                return new HttpResponseMessage(HttpStatusCode.InternalServerError);
            });

            var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test") };
            var service = new RemoteEvidenceExportService(httpClient);

            // Act & Assert
            await Assert.ThrowsAsync<HttpRequestException>(async () =>
            {
                await service.ExportEvidenceAsync(
                    new[] { Guid.NewGuid() },
                    "/output",
                    null,
                    null,
                    null,
                    CancellationToken.None);
            });
        }

        [Fact]
        public async Task ExportEvidenceAsync_WithCancellationToken_PassesCancellationToRequest()
        {
            // Arrange
            bool cancellationObserved = false;

            var handler = new CaptureHttpMessageHandler(async (request, ct) =>
            {
                cancellationObserved = !ct.IsCancellationRequested;

                var response = new HttpResponseMessage(HttpStatusCode.OK);
                var dto = new ExportResultDto(true, null, null, 0, [], null);
                response.Content = new StringContent(JsonSerializer.Serialize(dto), System.Text.Encoding.UTF8, "application/json");
                return response;
            });

            var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test") };
            var service = new RemoteEvidenceExportService(httpClient);

            var cts = new CancellationTokenSource();

            // Act
            await service.ExportEvidenceAsync(
                new[] { Guid.NewGuid() },
                "/output",
                null,
                null,
                null,
                cts.Token);

            // Assert
            Assert.True(cancellationObserved);
        }

        [Fact]
        public async Task ExportEvidenceAsync_MapsResponseCorrectly()
        {
            // Arrange
            var handler = new CaptureHttpMessageHandler(async (request, ct) =>
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK);
                var dto = new ExportResultDto(
                    Success: false,
                    ArchivePath: null,
                    ArchiveSha256Hash: null,
                    ItemsIncluded: 1,
                    ItemsExcludedForFailedIntegrity: new[] { Guid.NewGuid() },
                    ErrorMessage: "Export failed: disk full"
                );
                response.Content = new StringContent(JsonSerializer.Serialize(dto), System.Text.Encoding.UTF8, "application/json");
                return response;
            });

            var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test") };
            var service = new RemoteEvidenceExportService(httpClient);

            // Act
            var result = await service.ExportEvidenceAsync(
                new[] { Guid.NewGuid() },
                "/output",
                null,
                null,
                null,
                CancellationToken.None);

            // Assert
            Assert.False(result.Success);
            Assert.Null(result.ArchivePath);
            Assert.Null(result.ArchiveSha256Hash);
            Assert.Equal(1, result.ItemsIncluded);
            Assert.Single(result.ItemsExcludedForFailedIntegrity);
            Assert.Equal("Export failed: disk full", result.ErrorMessage);
        }

        /// <summary>Helper class to capture HTTP requests and control responses.</summary>
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
