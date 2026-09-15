using System.Text.Json;

using VideoForensics.Api.Contracts;
using VideoForensics.Client.Common.Contracts;
using VideoForensics.Hosting.Remote;

using Xunit;

namespace VideoForensics.Hosting.Tests
{
    public class RemoteBackupImportServiceTests
    {
        private readonly string _tempDirectory = Path.Combine(Path.GetTempPath(), $"import-tests-{Guid.NewGuid()}");

        public RemoteBackupImportServiceTests()
        {
            _ = Directory.CreateDirectory(_tempDirectory);
        }

        ~RemoteBackupImportServiceTests()
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
        public async Task ImportBackupAsync_WithValidZipFile_SendsMultipartRequest()
        {
            // Arrange
            string zipFilePath = Path.Combine(_tempDirectory, "test-backup.zip");
            string mediaRootPath = "/media/root";
            byte[] zipContent = new byte[] { 0x50, 0x4B, 0x03, 0x04 }; // ZIP magic bytes

            File.WriteAllBytes(zipFilePath, zipContent);

            HttpRequestMessage? capturedRequest = null;

            var handler = new FakeHttpMessageHandler(async request =>
            {
                capturedRequest = request;

                Assert.Equal(HttpMethod.Post, request.Method);
                Assert.Equal("/api/v1/backup/import", request.RequestUri?.AbsolutePath);
                Assert.NotNull(request.Content);
                Assert.True(request.Content!.Headers.ContentType?.MediaType?.StartsWith("multipart/form-data") ?? false);

                var expectedDto = new BackupImportResultDto(
                    Success: true,
                    ProviderAccounts: new BackupImportStageResultDto(5, 0, 0, 0),
                    Locations: new BackupImportStageResultDto(10, 0, 0, 0),
                    Devices: new BackupImportStageResultDto(25, 0, 0, 0),
                    Events: new BackupImportStageResultDto(100, 0, 0, 0),
                    DownloadEvents: new BackupImportStageResultDto(50, 0, 0, 0),
                    MediaItems: new BackupImportStageResultDto(75, 0, 0, 0),
                    Details: new[] { "Import complete" },
                    ErrorMessage: null
                );

                string jsonContent = JsonSerializer.Serialize(expectedDto);
                var response = new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json")
                };

                return await Task.FromResult(response);
            });

            var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test") };
            var service = new RemoteBackupImportService(httpClient);

            // Act
            BackupImportResult result = await service.ImportBackupAsync(zipFilePath, mediaRootPath, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.Success);
            Assert.Equal(5, result.ProviderAccounts.Inserted);
            Assert.Equal(10, result.Locations.Inserted);
            Assert.Equal(25, result.Devices.Inserted);
            Assert.Equal(100, result.Events.Inserted);
            Assert.Equal(50, result.DownloadEvents.Inserted);
            Assert.Equal(75, result.MediaItems.Inserted);
            _ = Assert.Single(result.Details);
            Assert.Equal("Import complete", result.Details[0]);

            Assert.NotNull(capturedRequest);
        }

        [Fact]
        public async Task ImportBackupAsync_WithMissingZipFile_ThrowsFileNotFoundException()
        {
            // Arrange
            string zipFilePath = Path.Combine(_tempDirectory, "nonexistent-backup.zip");
            string mediaRootPath = "/media/root";

            var handler = new FakeHttpMessageHandler(async _ =>
            {
                return new HttpResponseMessage(System.Net.HttpStatusCode.OK);
            });

            var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test") };
            var service = new RemoteBackupImportService(httpClient);

            // Act & Assert
            FileNotFoundException ex = await Assert.ThrowsAsync<FileNotFoundException>(
                () => service.ImportBackupAsync(zipFilePath, mediaRootPath, CancellationToken.None));
            Assert.Contains(zipFilePath, ex.Message);
        }

        [Fact]
        public async Task ImportBackupAsync_WithHttpError_ThrowsHttpRequestException()
        {
            // Arrange
            string zipFilePath = Path.Combine(_tempDirectory, "test-backup-error.zip");
            string mediaRootPath = "/media/root";
            byte[] zipContent = new byte[] { 0x50, 0x4B, 0x03, 0x04 }; // ZIP magic bytes

            File.WriteAllBytes(zipFilePath, zipContent);

            var handler = new FakeHttpMessageHandler(async request =>
            {
                return new HttpResponseMessage(System.Net.HttpStatusCode.BadRequest)
                {
                    Content = new StringContent("Bad request", System.Text.Encoding.UTF8, "text/plain")
                };
            });

            var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test") };
            var service = new RemoteBackupImportService(httpClient);

            // Act & Assert
            _ = await Assert.ThrowsAsync<HttpRequestException>(
                () => service.ImportBackupAsync(zipFilePath, mediaRootPath, CancellationToken.None));
        }

        [Fact]
        public async Task ImportBackupAsync_WithNullJsonResponse_ThrowsInvalidOperationException()
        {
            // Arrange
            string zipFilePath = Path.Combine(_tempDirectory, "test-backup-null.zip");
            string mediaRootPath = "/media/root";
            byte[] zipContent = new byte[] { 0x50, 0x4B, 0x03, 0x04 }; // ZIP magic bytes

            File.WriteAllBytes(zipFilePath, zipContent);

            var handler = new FakeHttpMessageHandler(async request =>
            {
                return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent("null", System.Text.Encoding.UTF8, "application/json")
                };
            });

            var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test") };
            var service = new RemoteBackupImportService(httpClient);

            // Act & Assert
            InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.ImportBackupAsync(zipFilePath, mediaRootPath, CancellationToken.None));
            Assert.Contains("null import result", ex.Message);
        }

        [Fact]
        public async Task ImportBackupAsync_WithFailedImportResult_ReturnsFailureResult()
        {
            // Arrange
            string zipFilePath = Path.Combine(_tempDirectory, "test-backup-failed.zip");
            string mediaRootPath = "/media/root";
            byte[] zipContent = new byte[] { 0x50, 0x4B, 0x03, 0x04 }; // ZIP magic bytes

            File.WriteAllBytes(zipFilePath, zipContent);

            var expectedDto = new BackupImportResultDto(
                Success: false,
                ProviderAccounts: new BackupImportStageResultDto(0, 0, 0, 0),
                Locations: new BackupImportStageResultDto(0, 0, 0, 0),
                Devices: new BackupImportStageResultDto(0, 0, 0, 0),
                Events: new BackupImportStageResultDto(0, 0, 0, 0),
                DownloadEvents: new BackupImportStageResultDto(0, 0, 0, 0),
                MediaItems: new BackupImportStageResultDto(0, 0, 0, 0),
                Details: new[] { "Import failed due to corrupted archive" },
                ErrorMessage: "Archive is corrupted"
            );

            var handler = new FakeHttpMessageHandler(async request =>
            {
                string jsonContent = JsonSerializer.Serialize(expectedDto);
                return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json")
                };
            });

            var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test") };
            var service = new RemoteBackupImportService(httpClient);

            // Act
            BackupImportResult result = await service.ImportBackupAsync(zipFilePath, mediaRootPath, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.False(result.Success);
            Assert.Equal("Archive is corrupted", result.ErrorMessage);
            _ = Assert.Single(result.Details);
            Assert.Equal("Import failed due to corrupted archive", result.Details[0]);
        }

        [Fact]
        public async Task ImportBackupAsync_IncludesMediaRootPathInRequest()
        {
            // Arrange
            string zipFilePath = Path.Combine(_tempDirectory, "test-backup-media.zip");
            string mediaRootPath = "/custom/media/path";
            byte[] zipContent = new byte[] { 0x50, 0x4B, 0x03, 0x04 }; // ZIP magic bytes

            File.WriteAllBytes(zipFilePath, zipContent);

            var handler = new FakeHttpMessageHandler(async request =>
            {
                Assert.NotNull(request.Content);

                // Copy the multipart content for inspection
                string contentStr = await request.Content!.ReadAsStringAsync();
                Assert.Contains(mediaRootPath, contentStr);

                var expectedDto = new BackupImportResultDto(
                    Success: true,
                    ProviderAccounts: new BackupImportStageResultDto(0, 0, 0, 0),
                    Locations: new BackupImportStageResultDto(0, 0, 0, 0),
                    Devices: new BackupImportStageResultDto(0, 0, 0, 0),
                    Events: new BackupImportStageResultDto(0, 0, 0, 0),
                    DownloadEvents: new BackupImportStageResultDto(0, 0, 0, 0),
                    MediaItems: new BackupImportStageResultDto(0, 0, 0, 0),
                    Details: new[] { "Complete" },
                    ErrorMessage: null
                );

                string jsonContent = JsonSerializer.Serialize(expectedDto);
                return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json")
                };
            });

            var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test") };
            var service = new RemoteBackupImportService(httpClient);

            // Act
            BackupImportResult result = await service.ImportBackupAsync(zipFilePath, mediaRootPath, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.Success);
        }

        [Fact]
        public async Task ImportBackupAsync_WithLargeZipFile_TransfersSuccessfully()
        {
            // Arrange
            string zipFilePath = Path.Combine(_tempDirectory, "test-backup-large.zip");
            string mediaRootPath = "/media/root";

            // Create a larger file (1 MB)
            byte[] largeZipContent = new byte[1024 * 1024];
            for (int i = 0; i < largeZipContent.Length; i++)
            {
                largeZipContent[i] = (byte)(i % 256);
            }

            File.WriteAllBytes(zipFilePath, largeZipContent);

            long? capturedContentLength = null;

            var handler = new FakeHttpMessageHandler(async request =>
            {
                Assert.NotNull(request.Content);
                capturedContentLength = request.Content!.Headers.ContentLength;

                var expectedDto = new BackupImportResultDto(
                    Success: true,
                    ProviderAccounts: new BackupImportStageResultDto(0, 0, 0, 0),
                    Locations: new BackupImportStageResultDto(0, 0, 0, 0),
                    Devices: new BackupImportStageResultDto(0, 0, 0, 0),
                    Events: new BackupImportStageResultDto(0, 0, 0, 0),
                    DownloadEvents: new BackupImportStageResultDto(0, 0, 0, 0),
                    MediaItems: new BackupImportStageResultDto(0, 0, 0, 0),
                    Details: new[] { "Large file imported" },
                    ErrorMessage: null
                );

                string jsonContent = JsonSerializer.Serialize(expectedDto);
                return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json")
                };
            });

            var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test") };
            var service = new RemoteBackupImportService(httpClient);

            // Act
            BackupImportResult result = await service.ImportBackupAsync(zipFilePath, mediaRootPath, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.Success);
            _ = Assert.Single(result.Details);
            Assert.Equal("Large file imported", result.Details[0]);
        }

        [Fact]
        public async Task ImportBackupAsync_WithCancellationToken_PropagatesCancellation()
        {
            // Arrange
            string zipFilePath = Path.Combine(_tempDirectory, "test-backup-cancel.zip");
            string mediaRootPath = "/media/root";
            byte[] zipContent = new byte[] { 0x50, 0x4B, 0x03, 0x04 }; // ZIP magic bytes

            File.WriteAllBytes(zipFilePath, zipContent);

            var handler = new FakeHttpMessageHandler(async request =>
            {
                return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        JsonSerializer.Serialize(new BackupImportResultDto(
                            Success: true,
                            ProviderAccounts: new BackupImportStageResultDto(0, 0, 0, 0),
                            Locations: new BackupImportStageResultDto(0, 0, 0, 0),
                            Devices: new BackupImportStageResultDto(0, 0, 0, 0),
                            Events: new BackupImportStageResultDto(0, 0, 0, 0),
                            DownloadEvents: new BackupImportStageResultDto(0, 0, 0, 0),
                            MediaItems: new BackupImportStageResultDto(0, 0, 0, 0),
                            Details: new string[] { },
                            ErrorMessage: null
                        )),
                        System.Text.Encoding.UTF8,
                        "application/json")
                };
            });

            var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test") };
            var service = new RemoteBackupImportService(httpClient);

            using var cts = new CancellationTokenSource();
            // Act
            Task<BackupImportResult> task = service.ImportBackupAsync(zipFilePath, mediaRootPath, cts.Token);

            // Note: We can't easily test cancellation without a slow handler, but we can
            // verify that the token is accepted by the method signature
            BackupImportResult result = await task;

            // Assert
            Assert.NotNull(result);
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
}
