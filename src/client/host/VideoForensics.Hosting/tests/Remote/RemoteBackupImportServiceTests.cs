using System.Net;
using System.Text.Json;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Client.Hosting.Remote;
using VideoForensics.Core.Api.Contracts;

namespace VideoForensics.Client.Hosting.Tests.Remote
{
    public class RemoteBackupImportServiceTests : IDisposable
    {
        private static readonly Guid UploadId = Guid.Parse("11111111-2222-3333-4444-555555555555");
        private readonly string _tempDirectory = Path.Combine(Path.GetTempPath(), $"import-tests-{Guid.NewGuid()}");

        public RemoteBackupImportServiceTests()
        {
            _ = Directory.CreateDirectory(_tempDirectory);
        }

        public void Dispose()
        {
            if (Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, recursive: true);
            }
        }

        private static BackupImportResultDto OkResult(string detail) => new(
            Success: true,
            ProviderAccounts: new BackupImportStageResultDto(0, 0, 0, 0),
            Locations: new BackupImportStageResultDto(0, 0, 0, 0),
            Devices: new BackupImportStageResultDto(0, 0, 0, 0),
            Events: new BackupImportStageResultDto(0, 0, 0, 0),
            DownloadEvents: new BackupImportStageResultDto(0, 0, 0, 0),
            MediaItems: new BackupImportStageResultDto(0, 0, 0, 0),
            Details: new[] { detail },
            ErrorMessage: null);

        private static HttpResponseMessage Json(object value) => new(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(value), System.Text.Encoding.UTF8, "application/json"),
        };

        [Fact]
        public async Task ImportBackupAsync_LargeZip_StartsUploadSendsOneMegabyteChunksThenCompletes()
        {
            string zipPath = Path.Combine(_tempDirectory, "big.zip");
            byte[] zip = new byte[(2 * 1024 * 1024) + (512 * 1024)];
            File.WriteAllBytes(zipPath, zip);

            var calls = new List<(string Method, string Path, long? Length, string Body)>();
            var handler = new FakeHttpMessageHandler(async request =>
            {
                string path = request.RequestUri!.AbsolutePath;
                string body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync();
                long? length = request.Content?.Headers.ContentLength;
                calls.Add((request.Method.Method, path, length, body));

                if (request.Method == HttpMethod.Post && path == "/api/v1/backup/imports")
                {
                    return Json(new BackupUploadCreatedDto(UploadId));
                }

                if (request.Method == HttpMethod.Put)
                {
                    return new HttpResponseMessage(HttpStatusCode.NoContent);
                }

                return Json(OkResult("Imported from chunks"));
            });

            var service = new RemoteBackupImportService(new HttpClient(handler) { BaseAddress = new Uri("https://example.test") });

            BackupImportResult result = await service.ImportBackupAsync(zipPath, "/media/root", CancellationToken.None);

            Assert.True(result.Success);
            Assert.Equal("Imported from chunks", Assert.Single(result.Details));

            var puts = calls.Where(c => c.Method == "PUT").ToList();
            Assert.Equal(3, puts.Count);
            Assert.Equal($"/api/v1/backup/imports/{UploadId}/chunks/0", puts[0].Path);
            Assert.Equal($"/api/v1/backup/imports/{UploadId}/chunks/1", puts[1].Path);
            Assert.Equal($"/api/v1/backup/imports/{UploadId}/chunks/2", puts[2].Path);
            Assert.Equal(1024 * 1024, puts[0].Length);
            Assert.Equal(1024 * 1024, puts[1].Length);
            Assert.Equal(512 * 1024, puts[2].Length);

            var complete = Assert.Single(calls.Where(c => c.Path == $"/api/v1/backup/imports/{UploadId}/complete"));
            Assert.Equal("POST", complete.Method);
            using JsonDocument doc = JsonDocument.Parse(complete.Body);
            Assert.Equal(3, doc.RootElement.GetProperty("chunkCount").GetInt32());
            Assert.Equal("/media/root", doc.RootElement.GetProperty("mediaRootPath").GetString());
        }

        [Fact]
        public async Task ImportBackupAsync_WithMissingZipFile_ThrowsFileNotFoundException()
        {
            string zipPath = Path.Combine(_tempDirectory, "nonexistent.zip");
            var handler = new FakeHttpMessageHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
            var service = new RemoteBackupImportService(new HttpClient(handler) { BaseAddress = new Uri("https://example.test") });

            FileNotFoundException ex = await Assert.ThrowsAsync<FileNotFoundException>(
                () => service.ImportBackupAsync(zipPath, "/media/root", CancellationToken.None));
            Assert.Contains(zipPath, ex.Message);
        }

        [Fact]
        public async Task ImportBackupAsync_WhenStartFails_ThrowsHttpRequestException()
        {
            string zipPath = Path.Combine(_tempDirectory, "fail.zip");
            File.WriteAllBytes(zipPath, [1, 2, 3]);
            var handler = new FakeHttpMessageHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)));
            var service = new RemoteBackupImportService(new HttpClient(handler) { BaseAddress = new Uri("https://example.test") });

            _ = await Assert.ThrowsAsync<HttpRequestException>(
                () => service.ImportBackupAsync(zipPath, "/media/root", CancellationToken.None));
        }

        [Fact]
        public async Task ImportBackupAsync_WithNullCompleteResponse_ThrowsInvalidOperationException()
        {
            string zipPath = Path.Combine(_tempDirectory, "null.zip");
            File.WriteAllBytes(zipPath, [1, 2, 3]);
            var handler = new FakeHttpMessageHandler(request =>
            {
                if (request.Method == HttpMethod.Post && request.RequestUri!.AbsolutePath == "/api/v1/backup/imports")
                {
                    return Task.FromResult(Json(new BackupUploadCreatedDto(UploadId)));
                }

                if (request.Method == HttpMethod.Put)
                {
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));
                }

                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("null", System.Text.Encoding.UTF8, "application/json"),
                });
            });
            var service = new RemoteBackupImportService(new HttpClient(handler) { BaseAddress = new Uri("https://example.test") });

            InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.ImportBackupAsync(zipPath, "/media/root", CancellationToken.None));
            Assert.Contains("null import result", ex.Message);
        }

        [Fact]
        public async Task ImportBackupAsync_WithFailedImportResult_ReturnsFailureResult()
        {
            string zipPath = Path.Combine(_tempDirectory, "failed.zip");
            File.WriteAllBytes(zipPath, [1, 2, 3]);
            var failed = OkResult("Import failed due to corrupted archive") with { Success = false, ErrorMessage = "Archive is corrupted" };
            var handler = new FakeHttpMessageHandler(request =>
            {
                if (request.Method == HttpMethod.Post && request.RequestUri!.AbsolutePath == "/api/v1/backup/imports")
                {
                    return Task.FromResult(Json(new BackupUploadCreatedDto(UploadId)));
                }

                if (request.Method == HttpMethod.Put)
                {
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));
                }

                return Task.FromResult(Json(failed));
            });
            var service = new RemoteBackupImportService(new HttpClient(handler) { BaseAddress = new Uri("https://example.test") });

            BackupImportResult result = await service.ImportBackupAsync(zipPath, "/media/root", CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal("Archive is corrupted", result.ErrorMessage);
        }

        private sealed class FakeHttpMessageHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _handler;

            public FakeHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler)
            {
                _handler = handler;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                return _handler(request);
            }
        }
    }
}
