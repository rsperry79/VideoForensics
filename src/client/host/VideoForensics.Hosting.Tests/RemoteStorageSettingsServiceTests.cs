using System.Net;
using System.Text.Json;

using VideoForensics.Api.Contracts;
using VideoForensics.Client.Common.Contracts;
using VideoForensics.Hosting.Remote;
using VideoForensics.Providers.Common.Helpers.Platform;

using Xunit;

namespace VideoForensics.Hosting.Tests
{
    public class RemoteStorageSettingsServiceTests
    {
        private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

        [Fact]
        public async Task GetStatusAsync_WithValidResponse_ReturnsCorrectlyMappedStorageSettings()
        {
            // Arrange
            bool requestCaptured = false;
            string? capturedMethod = null;
            string? capturedUri = null;

            var handler = new CaptureHttpMessageHandler(async (request, ct) =>
            {
                requestCaptured = true;
                capturedMethod = request.Method?.ToString();
                capturedUri = request.RequestUri?.ToString();

                var response = new HttpResponseMessage(HttpStatusCode.OK);
                var dto = new StorageSettingsDto(
                    Categories: new[]
                    {
                        new StorageCategoryStatusDto(
                            Category: StorageCategory.Database,
                            CurrentPath: "/data/database",
                            IsDefault: true,
                            UsedBytes: 1024L * 1024 * 100, // 100 MB
                            FreeBytes: 1024L * 1024 * 1024 * 10, // 10 GB
                            IsRelocatable: true,
                            RequiresRestart: true
                        ),
                        new StorageCategoryStatusDto(
                            Category: StorageCategory.Media,
                            CurrentPath: "/data/media",
                            IsDefault: false,
                            UsedBytes: 1024L * 1024 * 1024 * 5, // 5 GB
                            FreeBytes: 1024L * 1024 * 1024 * 50, // 50 GB
                            IsRelocatable: true,
                            RequiresRestart: false
                        )
                    }
                );
                response.Content = new StringContent(JsonSerializer.Serialize(dto), System.Text.Encoding.UTF8, "application/json");
                return response;
            });

            var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test") };
            var service = new RemoteStorageSettingsService(httpClient);

            // Act
            StorageSettings result = await service.GetStatusAsync(CancellationToken.None);

            // Assert
            Assert.True(requestCaptured);
            Assert.Equal("GET", capturedMethod);
            Assert.EndsWith("/api/v1/storage-settings", capturedUri);
            Assert.NotNull(result);
            Assert.Equal(2, result.Categories.Count);

            var dbCategory = result.Categories[0];
            Assert.Equal(StorageCategory.Database, dbCategory.Category);
            Assert.Equal("/data/database", dbCategory.CurrentPath);
            Assert.True(dbCategory.IsDefault);
            Assert.True(dbCategory.RequiresRestart);

            var mediaCategory = result.Categories[1];
            Assert.Equal(StorageCategory.Media, mediaCategory.Category);
            Assert.Equal("/data/media", mediaCategory.CurrentPath);
            Assert.False(mediaCategory.IsDefault);
        }

        [Fact]
        public async Task RelocateAsync_WithValidRequest_CallsCorrectEndpointAndReturnsResult()
        {
            // Arrange
            RelocateStorageCategoryRequestDto? capturedRequestDto = null;

            var handler = new CaptureHttpMessageHandler(async (request, ct) =>
            {
                if (request.Content != null)
                {
                    string content = await request.Content.ReadAsStringAsync();
                    capturedRequestDto = JsonSerializer.Deserialize<RelocateStorageCategoryRequestDto>(content, JsonOptions);
                }

                var response = new HttpResponseMessage(HttpStatusCode.OK);
                var dto = new RelocateStorageCategoryResultDto(
                    Succeeded: true,
                    ErrorMessage: null,
                    RestartRequired: true
                );
                response.Content = new StringContent(JsonSerializer.Serialize(dto), System.Text.Encoding.UTF8, "application/json");
                return response;
            });

            var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test") };
            var service = new RemoteStorageSettingsService(httpClient);

            var request = new RelocateStorageCategoryRequest(
                Category: StorageCategory.Database,
                NewRootPath: "/new/path"
            );

            // Act
            RelocateStorageCategoryResult result = await service.RelocateAsync(request, CancellationToken.None);

            // Assert
            Assert.NotNull(capturedRequestDto);
            Assert.Equal(StorageCategory.Database, capturedRequestDto.Category);
            Assert.Equal("/new/path", capturedRequestDto.NewRootPath);

            Assert.True(result.Succeeded);
            Assert.Null(result.ErrorMessage);
            Assert.True(result.RestartRequired);
        }

        [Fact]
        public async Task RelocateAsync_WithFailureResponse_ReturnsMappedErrorResult()
        {
            // Arrange
            var handler = new CaptureHttpMessageHandler(async (request, ct) =>
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK);
                var dto = new RelocateStorageCategoryResultDto(
                    Succeeded: false,
                    ErrorMessage: "Failed to relocate: insufficient permissions",
                    RestartRequired: false
                );
                response.Content = new StringContent(JsonSerializer.Serialize(dto), System.Text.Encoding.UTF8, "application/json");
                return response;
            });

            var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test") };
            var service = new RemoteStorageSettingsService(httpClient);

            var request = new RelocateStorageCategoryRequest(
                Category: StorageCategory.Media,
                NewRootPath: "/invalid/path"
            );

            // Act
            RelocateStorageCategoryResult result = await service.RelocateAsync(request, CancellationToken.None);

            // Assert
            Assert.False(result.Succeeded);
            Assert.Equal("Failed to relocate: insufficient permissions", result.ErrorMessage);
            Assert.False(result.RestartRequired);
        }

        [Fact]
        public async Task GetStatusAsync_WithHttpError_ThrowsHttpRequestException()
        {
            // Arrange
            var handler = new CaptureHttpMessageHandler(async (request, ct) =>
            {
                return new HttpResponseMessage(HttpStatusCode.InternalServerError);
            });

            var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test") };
            var service = new RemoteStorageSettingsService(httpClient);

            // Act & Assert
            _ = await Assert.ThrowsAsync<HttpRequestException>(async () =>
            {
                _ = await service.GetStatusAsync(CancellationToken.None);
            });
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
