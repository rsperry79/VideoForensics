using System.Text.Json;

using VideoForensics.Api.Contracts;
using VideoForensics.Client.Common.Contracts;
using VideoForensics.Hosting.Remote;

using Xunit;

namespace VideoForensics.Hosting.Tests
{
    public class RemoteBackupExportServiceTests
    {
        private readonly string _tempDirectory = Path.Combine(Path.GetTempPath(), $"backup-tests-{Guid.NewGuid()}");

        public RemoteBackupExportServiceTests()
        {
            _ = Directory.CreateDirectory(_tempDirectory);
        }

        ~RemoteBackupExportServiceTests()
        {
            try
            {
                if (Directory.Exists(_tempDirectory))
                {
                    Directory.Delete(_tempDirectory, recursive: true);
                }
            }
            catch
            {
                // Ignore cleanup errors in tests
            }
        }

        [Fact]
        public async Task PrepareForExportAsync_WithValidResponse_ReturnsPrepareExportResult()
        {
            // Arrange
            var expectedDto = new PrepareExportResultDto(
                Validated: 100,
                Backfilled: 5,
                MissingMedia: 2,
                MissingSidecarUnrecoverable: 1,
                Details: new[] { "Validation complete" }
            );

            var handler = new FakeHttpMessageHandler(request =>
            {
                Assert.Equal(HttpMethod.Post, request.Method);
                Assert.Equal("/api/v1/backup/prepare-export", request.RequestUri?.AbsolutePath);
                Assert.Null(request.Content);

                string jsonContent = JsonSerializer.Serialize(expectedDto);
                return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json")
                };
            });

            var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test") };
            var service = new RemoteBackupExportService(httpClient);

            // Act
            PrepareExportResult result = await service.PrepareForExportAsync(CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(100, result.Validated);
            Assert.Equal(5, result.Backfilled);
            Assert.Equal(2, result.MissingMedia);
            Assert.Equal(1, result.MissingSidecarUnrecoverable);
            _ = Assert.Single(result.Details);
            Assert.Equal("Validation complete", result.Details[0]);
        }

        [Fact]
        public async Task PrepareForExportAsync_WithNullResponse_ThrowsInvalidOperationException()
        {
            // Arrange
            var handler = new FakeHttpMessageHandler(request =>
            {
                return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent("null", System.Text.Encoding.UTF8, "application/json")
                };
            });

            var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test") };
            var service = new RemoteBackupExportService(httpClient);

            // Act & Assert
            InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.PrepareForExportAsync(CancellationToken.None));
            Assert.Contains("null prepare-export result", ex.Message);
        }

        [Fact]
        public async Task PrepareForExportAsync_WithHttpError_ThrowsHttpRequestException()
        {
            // Arrange
            var handler = new FakeHttpMessageHandler(request =>
            {
                return new HttpResponseMessage(System.Net.HttpStatusCode.BadRequest)
                {
                    Content = new StringContent("Bad request", System.Text.Encoding.UTF8, "text/plain")
                };
            });

            var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test") };
            var service = new RemoteBackupExportService(httpClient);

            // Act & Assert
            _ = await Assert.ThrowsAsync<HttpRequestException>(
                () => service.PrepareForExportAsync(CancellationToken.None));
        }

        [Fact]
        public async Task ExportBackupAsync_WithZipResponse_SavesFileAndReturnsResult()
        {
            // Arrange
            string outputDir = Path.Combine(_tempDirectory, "export-zip");
            byte[] zipContent = new byte[] { 0x50, 0x4B, 0x03, 0x04 }; // ZIP magic bytes
            string fileName = "backup-20240101-120000.zip";

            var handler = new FakeHttpMessageHandler(request =>
            {
                Assert.Equal(HttpMethod.Post, request.Method);
                Assert.Equal("/api/v1/backup/export", request.RequestUri?.AbsolutePath);
                Assert.NotNull(request.Content);

                var response = new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(zipContent)
                };
                response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/zip");
                response.Content.Headers.ContentDisposition = new System.Net.Http.Headers.ContentDispositionHeaderValue("attachment")
                {
                    FileName = $"\"{fileName}\""
                };

                return response;
            });

            var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test") };
            var service = new RemoteBackupExportService(httpClient);

            // Act
            BackupExportResult result = await service.ExportBackupAsync(outputDir, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.Success);
            Assert.Contains(fileName, result.ArchivePath);
            Assert.True(File.Exists(result.ArchivePath));

            byte[] savedBytes = File.ReadAllBytes(result.ArchivePath);
            Assert.Equal(zipContent, savedBytes);
        }

        [Fact]
        public async Task ExportBackupAsync_WithZipResponseAndNoFileName_UsesDefaultFileName()
        {
            // Arrange
            string outputDir = Path.Combine(_tempDirectory, "export-zip-default");
            byte[] zipContent = new byte[] { 0x50, 0x4B, 0x03, 0x04 }; // ZIP magic bytes

            var handler = new FakeHttpMessageHandler(request =>
            {
                var response = new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(zipContent)
                };
                response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/zip");
                // No Content-Disposition header

                return response;
            });

            var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test") };
            var service = new RemoteBackupExportService(httpClient);

            // Act
            BackupExportResult result = await service.ExportBackupAsync(outputDir, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.Success);
            Assert.NotNull(result.ArchivePath);
            Assert.True(File.Exists(result.ArchivePath));
            Assert.Contains("backup-", Path.GetFileName(result.ArchivePath));
            Assert.EndsWith(".zip", result.ArchivePath);
        }

        [Fact]
        public async Task ExportBackupAsync_WithJsonResponse_ReturnsBackupExportResult()
        {
            // Arrange
            string outputDir = Path.Combine(_tempDirectory, "export-json");
            var expectedDto = new BackupExportResultDto(
                Success: true,
                ArchivePath: "/server/path/backup.zip",
                ArchiveSha256Hash: "abc123def456",
                ProviderAccountCount: 5,
                LocationCount: 10,
                DeviceCount: 25,
                EventCount: 1000,
                DownloadEventCount: 500,
                MediaItemCount: 300,
                ErrorMessage: null
            );

            var handler = new FakeHttpMessageHandler(request =>
            {
                Assert.Equal(HttpMethod.Post, request.Method);
                Assert.Equal("/api/v1/backup/export", request.RequestUri?.AbsolutePath);

                string jsonContent = JsonSerializer.Serialize(expectedDto);
                var response = new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json")
                };

                return response;
            });

            var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test") };
            var service = new RemoteBackupExportService(httpClient);

            // Act
            BackupExportResult result = await service.ExportBackupAsync(outputDir, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.Success);
            Assert.Equal("/server/path/backup.zip", result.ArchivePath);
            Assert.Equal("abc123def456", result.ArchiveSha256Hash);
            Assert.Equal(5, result.ProviderAccountCount);
            Assert.Equal(10, result.LocationCount);
            Assert.Equal(25, result.DeviceCount);
            Assert.Equal(1000, result.EventCount);
            Assert.Equal(500, result.DownloadEventCount);
            Assert.Equal(300, result.MediaItemCount);
            Assert.Null(result.ErrorMessage);
        }

        [Fact]
        public async Task ExportBackupAsync_CreatesOutputDirectoryIfNotExists()
        {
            // Arrange
            string outputDir = Path.Combine(_tempDirectory, "deeply", "nested", "path");
            Assert.False(Directory.Exists(outputDir));

            byte[] zipContent = new byte[] { 0x50, 0x4B, 0x03, 0x04 }; // ZIP magic bytes

            var handler = new FakeHttpMessageHandler(request =>
            {
                var response = new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(zipContent)
                };
                response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/zip");
                response.Content.Headers.ContentDisposition = new System.Net.Http.Headers.ContentDispositionHeaderValue("attachment")
                {
                    FileName = "\"backup.zip\""
                };

                return response;
            });

            var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test") };
            var service = new RemoteBackupExportService(httpClient);

            // Act
            BackupExportResult result = await service.ExportBackupAsync(outputDir, CancellationToken.None);

            // Assert
            Assert.True(Directory.Exists(outputDir));
            Assert.NotNull(result.ArchivePath);
            Assert.True(File.Exists(result.ArchivePath));
        }

        [Fact]
        public async Task ExportBackupAsync_WithNullJsonResponse_ThrowsInvalidOperationException()
        {
            // Arrange
            string outputDir = Path.Combine(_tempDirectory, "export-null");

            var handler = new FakeHttpMessageHandler(request =>
            {
                var response = new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent("null", System.Text.Encoding.UTF8, "application/json")
                };

                return response;
            });

            var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test") };
            var service = new RemoteBackupExportService(httpClient);

            // Act & Assert
            InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.ExportBackupAsync(outputDir, CancellationToken.None));
            Assert.Contains("null export result", ex.Message);
        }

        [Fact]
        public async Task ExportBackupAsync_WithHttpError_ThrowsHttpRequestException()
        {
            // Arrange
            string outputDir = Path.Combine(_tempDirectory, "export-error");

            var handler = new FakeHttpMessageHandler(request =>
            {
                return new HttpResponseMessage(System.Net.HttpStatusCode.InternalServerError)
                {
                    Content = new StringContent("Server error", System.Text.Encoding.UTF8, "text/plain")
                };
            });

            var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test") };
            var service = new RemoteBackupExportService(httpClient);

            // Act & Assert
            _ = await Assert.ThrowsAsync<HttpRequestException>(
                () => service.ExportBackupAsync(outputDir, CancellationToken.None));
        }

        [Fact]
        public async Task ExportBackupAsync_SendsCorrectJsonRequest()
        {
            // Arrange
            string outputDir = Path.Combine(_tempDirectory, "export-request-check");
            ExportBackupRequest? capturedRequest = null;

            var handler = new FakeHttpMessageHandler(async request =>
            {
                Assert.NotNull(request.Content);
                string content = await request.Content!.ReadAsStringAsync();
                capturedRequest = JsonSerializer.Deserialize<ExportBackupRequest>(content);

                var response = new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        JsonSerializer.Serialize(new BackupExportResultDto(
                            Success: true,
                            ArchivePath: "/path",
                            ArchiveSha256Hash: null,
                            ProviderAccountCount: 0,
                            LocationCount: 0,
                            DeviceCount: 0,
                            EventCount: 0,
                            DownloadEventCount: 0,
                            MediaItemCount: 0,
                            ErrorMessage: null
                        )),
                        System.Text.Encoding.UTF8,
                        "application/json")
                };

                return response;
            });

            var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test") };
            var service = new RemoteBackupExportService(httpClient);

            // Act
            _ = await service.ExportBackupAsync(outputDir, CancellationToken.None);

            // Assert
            Assert.NotNull(capturedRequest);
            Assert.Equal(outputDir, capturedRequest!.OutputDirectory);
        }

        [Fact]
        public async Task ExportBackupAsync_WithPathTraversalFileName_ThrowsInvalidOperationException()
        {
            // Arrange
            string outputDir = Path.Combine(_tempDirectory, "export-path-traversal");
            byte[] zipContent = new byte[] { 0x50, 0x4B, 0x03, 0x04 }; // ZIP magic bytes
            string maliciousFileName = "../../../../etc/passwd.zip"; // Path traversal attempt

            var handler = new FakeHttpMessageHandler(request =>
            {
                var response = new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(zipContent)
                };
                response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/zip");
                response.Content.Headers.ContentDisposition = new System.Net.Http.Headers.ContentDispositionHeaderValue("attachment")
                {
                    FileName = $"\"{maliciousFileName}\""
                };

                return response;
            });

            var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test") };
            var service = new RemoteBackupExportService(httpClient);

            // Act & Assert
            InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.ExportBackupAsync(outputDir, CancellationToken.None));
            Assert.Contains("escapes the intended output directory", ex.Message);
        }

        /// <summary>
        /// Fake HTTP message handler that executes a delegate to generate responses for testing.
        /// </summary>
        private class FakeHttpMessageHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;
            private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>>? _asyncHandler;

            public FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
            {
                _handler = handler;
            }

            public FakeHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> asyncHandler)
            {
                _asyncHandler = asyncHandler;
            }

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                return _asyncHandler != null ? await _asyncHandler(request) : _handler(request);
            }
        }
    }

    /// <summary>This type is mirrored from RemoteBackupExportService for testing purposes.</summary>
    internal record ExportBackupRequest(string OutputDirectory);
}
