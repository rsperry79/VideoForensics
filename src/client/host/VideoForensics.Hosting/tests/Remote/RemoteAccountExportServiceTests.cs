using System.Net;
using System.Text;
using System.Text.Json;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Core.Api.Contracts;
using VideoForensics.Client.Hosting.Remote;

namespace VideoForensics.Client.Hosting.Tests.Remote
{
    public class RemoteAccountExportServiceTests
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

        private sealed class CapturingHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, HttpResponseMessage> _factory;

            public CapturingHandler(Func<HttpRequestMessage, HttpResponseMessage> factory)
            {
                _factory = factory;
            }

            public HttpRequestMessage? Captured { get; private set; }

            public string? CapturedBody { get; private set; }

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            {
                Captured = request;
                CapturedBody = request.Content == null ? null : await request.Content.ReadAsStringAsync(ct);
                return _factory(request);
            }
        }

        private static HttpResponseMessage Json(HttpStatusCode code, object body)
        {
            return new(code) { Content = new StringContent(JsonSerializer.Serialize(body, JsonOptions), Encoding.UTF8, "application/json") };
        }

        [Fact]
        public async Task RemoteAccountExportService_QueueExportAsync_PostsExportsRoute()
        {
            var accountId = Guid.NewGuid();
            var jobId = Guid.NewGuid();
            var dto = new ExportJobDto(jobId, accountId, "Queued", null, null, true, DateTime.UtcNow, null);
            var handler = new CapturingHandler(_ => Json(HttpStatusCode.Accepted, dto));
            var sut = new RemoteAccountExportService(new HttpClient(handler) { BaseAddress = new Uri("https://example.test") });

            Guid result = await sut.QueueExportAsync(accountId, true, CancellationToken.None);

            Assert.Equal(jobId, result);
            Assert.Equal(HttpMethod.Post, handler.Captured!.Method);
            Assert.Equal($"/api/v1/accounts/provider-accounts/{accountId}/exports", handler.Captured.RequestUri!.AbsolutePath);
            Assert.Contains("\"purgeAfterExport\":true", handler.CapturedBody);
        }

        [Fact]
        public async Task RemoteAccountExportService_QueueExportAsync_SendsStepUpTokenHeader()
        {
            var accountId = Guid.NewGuid();
            var dto = new ExportJobDto(Guid.NewGuid(), accountId, "Queued", null, null, true, DateTime.UtcNow, null);
            var handler = new CapturingHandler(_ => Json(HttpStatusCode.Accepted, dto));
            var sut = new RemoteAccountExportService(new HttpClient(handler) { BaseAddress = new Uri("https://example.test") });

            _ = await sut.QueueExportAsync(accountId, true, CancellationToken.None, "step-token");

            Assert.True(handler.Captured!.Headers.TryGetValues("X-StepUp-Token", out IEnumerable<string>? values));
            Assert.Equal("step-token", Assert.Single(values!));
            Assert.Contains("\"purgeAfterExport\":true", handler.CapturedBody);
        }

        [Fact]
        public async Task RemoteAccountExportService_GetStatusAsync_NotFound_ReturnsNull()
        {
            var handler = new CapturingHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
            var sut = new RemoteAccountExportService(new HttpClient(handler) { BaseAddress = new Uri("https://example.test") });

            ExportJobStatus? status = await sut.GetStatusAsync(Guid.NewGuid(), CancellationToken.None);

            Assert.Null(status);
        }

        [Fact]
        public async Task RemoteAccountExportService_GetStatusAsync_Found_MapsDto()
        {
            var jobId = Guid.NewGuid();
            var dto = new ExportJobDto(jobId, Guid.NewGuid(), "Succeeded", "x.json", null, false, DateTime.UtcNow, DateTime.UtcNow);
            var handler = new CapturingHandler(_ => Json(HttpStatusCode.OK, dto));
            var sut = new RemoteAccountExportService(new HttpClient(handler) { BaseAddress = new Uri("https://example.test") });

            ExportJobStatus? status = await sut.GetStatusAsync(jobId, CancellationToken.None);

            Assert.Equal(ExportJobState.Succeeded, status!.State);
            Assert.Equal($"/api/v1/accounts/exports/{jobId}", handler.Captured!.RequestUri!.AbsolutePath);
        }

        [Fact]
        public async Task RemoteAccountExportService_GetLatestStatusForAccountAsync_NotFound_ReturnsNull()
        {
            var accountId = Guid.NewGuid();
            var handler = new CapturingHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
            var sut = new RemoteAccountExportService(new HttpClient(handler) { BaseAddress = new Uri("https://example.test") });

            ExportJobStatus? status = await sut.GetLatestStatusForAccountAsync(accountId, CancellationToken.None);

            Assert.Null(status);
            Assert.Equal(HttpMethod.Get, handler.Captured!.Method);
            Assert.Equal($"/api/v1/accounts/provider-accounts/{accountId}/export-status", handler.Captured.RequestUri!.AbsolutePath);
        }

        [Fact]
        public async Task RemoteAccountExportService_GetLatestStatusForAccountAsync_Found_MapsDto()
        {
            var accountId = Guid.NewGuid();
            var jobId = Guid.NewGuid();
            var dto = new ExportJobDto(jobId, accountId, "Running", null, null, true, DateTime.UtcNow, null);
            var handler = new CapturingHandler(_ => Json(HttpStatusCode.OK, dto));
            var sut = new RemoteAccountExportService(new HttpClient(handler) { BaseAddress = new Uri("https://example.test") });

            ExportJobStatus? status = await sut.GetLatestStatusForAccountAsync(accountId, CancellationToken.None);

            Assert.Equal(jobId, status!.JobId);
            Assert.Equal(ExportJobState.Running, status.State);
        }

        private sealed class DisposeTrackingContent : StringContent
        {
            public DisposeTrackingContent(string json)
                : base(json, Encoding.UTF8, "application/json")
            {
            }

            public bool Disposed { get; private set; }

            protected override void Dispose(bool disposing)
            {
                Disposed = true;
                base.Dispose(disposing);
            }
        }

        [Fact]
        public async Task RemoteAccountExportService_GetStatusAsync_DisposesTheResponse()
        {
            var jobId = Guid.NewGuid();
            var dto = new ExportJobDto(jobId, Guid.NewGuid(), "Succeeded", "x.json", null, false, DateTime.UtcNow, DateTime.UtcNow);
            DisposeTrackingContent? content = null;
            var handler = new CapturingHandler(_ =>
            {
                content = new DisposeTrackingContent(JsonSerializer.Serialize(dto, JsonOptions));
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
            });
            var sut = new RemoteAccountExportService(new HttpClient(handler) { BaseAddress = new Uri("https://example.test") });

            _ = await sut.GetStatusAsync(jobId, CancellationToken.None);

            // The response (and its content stream) must be released once its body has been read.
            Assert.True(content!.Disposed);
        }
    }
}
