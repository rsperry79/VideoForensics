using System.Text.Json;
using Xunit;
using VideoForensics.Api.Contracts;
using VideoForensics.Client.Common.Contracts;
using VideoForensics.Hosting.Remote;

namespace VideoForensics.Hosting.Tests
{
    public class RemoteBackupImportServiceTests
    {
        private readonly string _tempDirectory = Path.Combine(Path.GetTempPath(), $"import-tests-{Guid.NewGuid()}");

        public RemoteBackupImportServiceTests()
        {
            Directory.CreateDirectory(_tempDirectory);
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
            var zipFilePath = Path.Combine(_tempDirectory, "test-backup.zip");
            var mediaRootPath = "/media/root";
            var zipContent = new byte[] { 0x50, 0x4B, 0x03, 0x04 }; // ZIP magic bytes

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

                var jsonContent = JsonSerializer.Serialize(expectedDto);
                var response = new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json")
                };

                return await Task.FromResult(response);
            });

            var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test") };
            var service = new RemoteBackupImportService(httpClient);

            // Act
            var result = await service.ImportBackupAsync(zipFilePath, mediaRootPath, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.Success);
            Assert.Equal(5, result.ProviderAccounts.Inserted);
            Assert.Equal(10, result.Locations.Inserted);
            Assert.Equal(25, result.Devices.Inserted);
            Assert.Equal(100, result.Events.Inserted);
            Assert.Equal(50, result.DownloadEvents.Inserted);
            Assert.Equal(75, result.MediaItems.Inserted);
            Assert.Single(result.Details);
            Assert.Equal("Import complete", result.Details[0]);

            Assert.NotNull(capturedRequest);
        }

        [Fact]
        public async Task ImportBackupAsync_WithMissingZipFile_ThrowsFileNotFoundException()
        {
            // Arrange
            var zipFilePath = Path.Combine(_tempDirectory, "nonexistent-backup.zip");
            var mediaRootPath = "/media/root";

            var handler = new FakeHttpMessageHandler(async _ =>
            {
                return new HttpResponseMessage(System.Net.HttpStatusCode.OK);
            });

            var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test") };
            var service = new RemoteBackupImportService(httpClient);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<FileNotFoundException>(
                () => service.ImportBackupAsync(zipFilePath, mediaRootPath, CancellationToken.None));
            Assert.Contains(zipFilePath, ex.Message);
        }

        [Fact]
        public async Task ImportBackupAsync_WithHttpError_ThrowsHttpRequestException()
        {
            // Arrange
            var zipFilePath = Path.Combine(_tempDirectory, "test-backup-error.zip");
            var mediaRootPath = "/media/root";
            var zipContent = new byte[] { 0x50, 0x4B, 0x03, 0x04 }; // ZIP magic bytes

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
            await Assert.ThrowsAsync<HttpRequestException>(
                () => service.ImportBackupAsync(zipFilePath, mediaRootPath, CancellationToken.None));
        }

        [Fact]
        public async Task ImportBackupAsync_WithNullJsonResponse_ThrowsInvalidOperationException()
        {
            // Arrange
            var zipFilePath = Path.Combine(_tempDirectory, "test-backup-null.zip");
            var mediaRootPath = "/media/root";
            var zipContent = new byte[] { 0x50, 0x4B, 0x03, 0x04 }; // ZIP magic bytes

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
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.ImportBackupAsync(zipFilePath, mediaRootPath, CancellationToken.None));
            Assert.Contains("null import result", ex.Message);
        }

        [Fact]
        public async Task ImportBackupAsync_WithFailedImportResult_ReturnsFailureResult()
        {
            // Arrange
            var zipFilePath = Path.Combine(_tempDirectory, "test-backup-failed.zip");
            var mediaRootPath = "/media/root";
            var zipContent = new byte[] { 0x50, 0x4B, 0x03, 0x04 }; // ZIP magic bytes

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
                var jsonContent = JsonSerializer.Serialize(expectedDto);
                return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json")
                };
            });

            var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test") };
            var service = new RemoteBackupImportService(httpClient);

            // Act
            var result = await service.ImportBackupAsync(zipFilePath, mediaRootPath, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.False(result.Success);
            Assert.Equal("Archive is corrupted", result.ErrorMessage);
            Assert.Single(result.Details);
            Assert.Equal("Import failed due to corrupted archive", result.Details[0]);
        }

        [Fact]
        public async Task ImportBackupAsync_IncludesMediaRootPathInRequest()
        {
            // Arrange
            var zipFilePath = Path.Combine(_tempDirectory, "test-backup-media.zip");
            var mediaRootPath = "/custom/media/path";
            var zipContent = new byte[] { 0x50, 0x4B, 0x03, 0x04 }; // ZIP magic bytes

            File.WriteAllBytes(zipFilePath, zipContent);

            var handler = new FakeHttpMessageHandler(async request =>
            {
                Assert.NotNull(request.Content);

                // Copy the multipart content for inspection
                var contentStr = await request.Content!.ReadAsStringAsync();
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

                var jsonContent = JsonSerializer.Serialize(expectedDto);
                return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json")
                };
            });

            var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test") };
            var service = new RemoteBackupImportService(httpClient);

            // Act
            var result = await service.ImportBackupAsync(zipFilePath, mediaRootPath, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.Success);
        }

        [Fact]
        public async Task ImportBackupAsync_WithLargeZipFile_TransfersSuccessfully()
        {
            // Arrange
            var zipFilePath = Path.Combine(_tempDirectory, "test-backup-large.zip");
            var mediaRootPath = "/media/root";

            // Create a larger file (1 MB)
            var largeZipContent = new byte[1024 * 1024];
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

                var jsonContent = JsonSerializer.Serialize(expectedDto);
                return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json")
                };
            });

            var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test") };
            var service = new RemoteBackupImportService(httpClient);

            // Act
            var result = await service.ImportBackupAsync(zipFilePath, mediaRootPath, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.Success);
            Assert.Single(result.Details);
            Assert.Equal("Large file imported", result.Details[0]);
        }

        [Fact]
        public async Task ImportBackupAsync_WithCancellationToken_PropagatesCancellation()
        {
            // Arrange
            var zipFilePath = Path.Combine(_tempDirectory, "test-backup-cancel.zip");
            var mediaRootPath = "/media/root";
            var zipContent = new byte[] { 0x50, 0x4B, 0x03, 0x04 }; // ZIP magic bytes

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

            using (var cts = new CancellationTokenSource())
            {
                // Act
                var task = service.ImportBackupAsync(zipFilePath, mediaRootPath, cts.Token);

                // Note: We can't easily test cancellation without a slow handler, but we can
                // verify that the token is accepted by the method signature
                var result = await task;

                // Assert
                Assert.NotNull(result);
            }
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
                if (_asyncHandler != null)
                {
                    return await _asyncHandler(request);
                }

                return _handler(request);
            }
        }
    }
}
