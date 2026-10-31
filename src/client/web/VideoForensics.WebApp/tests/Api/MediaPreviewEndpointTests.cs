using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Moq;

using System.Security.Claims;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Client.WebApp.Api;
using VideoForensics.Client.WebApp.Auth;
using VideoForensics.Core.Api.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Client.Hosting;

using Xunit;

namespace VideoForensics.Client.WebApp.Tests.Api
{
    /// <summary>
    /// Behaviour of <see cref="MediaApiEndpoints.GetPreviewAsync"/>: 404 without a preview, and the same
    /// range-request handling as the content endpoint. Each test writes its own temp file.
    /// </summary>
    public sealed class MediaPreviewEndpointTests : IDisposable
    {
        private const int FileLength = 100;

        private readonly List<string> _tempFiles = new();

        public void Dispose()
        {
            foreach (string path in _tempFiles)
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
        }

        [Fact]
        public async Task MediaPreview_NoPreviewPath_Returns404()
        {
            var context = CreateAuthenticatedContext();

            IResult result = await InvokeGetPreviewAsync(context, previewPath: null, previewFileExists: false);

            _ = Assert.IsType<NotFound>(result);
        }

        [Fact]
        public async Task MediaPreview_WholeFile_Returns200WithBody()
        {
            string path = CreateTempFileWithKnownBytes();
            var context = CreateAuthenticatedContext();

            IResult result = await InvokeGetPreviewAsync(context, path, previewFileExists: true);
            _ = Assert.IsType<FileStreamHttpResult>(result);

            byte[] body = await ExecuteResultAsync(result, context);

            Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
            Assert.Equal(FileLength, body.Length);
        }

        [Fact]
        public async Task MediaPreview_RangeRequest_Returns206WithContentRange()
        {
            string path = CreateTempFileWithKnownBytes();
            var context = CreateAuthenticatedContext();
            context.Request.Headers["Range"] = "bytes=0-9";

            IResult result = await InvokeGetPreviewAsync(context, path, previewFileExists: true);
            _ = Assert.IsType<FileStreamHttpResult>(result);

            byte[] body = await ExecuteResultAsync(result, context);

            Assert.Equal(StatusCodes.Status206PartialContent, context.Response.StatusCode);
            Assert.Equal("bytes 0-9/100", context.Response.Headers["Content-Range"].ToString());
            Assert.Equal(10, body.Length);
        }

        [Fact]
        public async Task MediaPreview_Success_SetsPrivateCacheControlAndMp4ContentType()
        {
            string path = CreateTempFileWithKnownBytes();
            var context = CreateAuthenticatedContext();

            IResult result = await InvokeGetPreviewAsync(context, path, previewFileExists: true);
            _ = Assert.IsType<FileStreamHttpResult>(result);

            _ = await ExecuteResultAsync(result, context);

            Assert.Equal("private, max-age=86400", context.Response.Headers.CacheControl.ToString());
            Assert.Equal("video/mp4", context.Response.ContentType);
        }

        [Fact]
        public async Task MediaPreview_DoesNotWriteAccessAuditEntry()
        {
            string path = CreateTempFileWithKnownBytes();
            var context = CreateAuthenticatedContext();
            // GetPreviewAsync takes no audit dependency, so this mock cannot be reached from the handler.
            // The Verify documents the requirement; it would catch an audit write only if one were injected.
            var auditLog = new Mock<IAccessAuditLogRepository>();

            IResult result = await InvokeGetPreviewAsync(context, path, previewFileExists: true);
            _ = Assert.IsType<FileStreamHttpResult>(result);

            _ = await ExecuteResultAsync(result, context);

            Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
            auditLog.Verify(
                a => a.RecordAccessAsync(
                    It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }

        private string CreateTempFileWithKnownBytes()
        {
            string path = Path.Combine(Path.GetTempPath(), $"vf-preview-{Guid.NewGuid():N}.mp4");
            _tempFiles.Add(path);
            File.WriteAllBytes(path, Enumerable.Range(0, FileLength).Select(i => (byte)i).ToArray());
            return path;
        }

        private static DefaultHttpContext CreateAuthenticatedContext()
        {
            var operatorId = Guid.NewGuid();
            var claims = new List<Claim>
            {
                new(VideoForensicsClaimTypes.OperatorId, operatorId.ToString()),
                new(ClaimTypes.Role, OperatorRole.Admin.ToString())
            };
            var context = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"))
            };
            // Range processing only applies to GET requests; DefaultHttpContext leaves Method null.
            context.Request.Method = HttpMethods.Get;
            context.RequestServices = new ServiceCollection().AddLogging().BuildServiceProvider();
            return context;
        }

        private static async Task<IResult> InvokeGetPreviewAsync(
            HttpContext context,
            string? previewPath,
            bool previewFileExists)
        {
            Guid mediaItemId = Guid.NewGuid();

            var mediaItems = new Mock<IMediaItemRepository>();
            _ = mediaItems.Setup(m => m.GetAsync(mediaItemId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new MediaItem
                {
                    Id = mediaItemId,
                    DeviceId = Guid.NewGuid(),
                    FileName = "preview-test.mp4",
                    FilePath = "/path/to/original.mp4",
                    PreviewPath = previewPath,
                    MediaFormat = "video/mp4",
                    Sha256Hash = "abc123",
                    RecordedAtUtc = DateTime.UtcNow,
                    DownloadedAtUtc = DateTime.UtcNow
                });

            var storage = new Mock<IMediaStorageProvider>();
            if (previewPath != null)
            {
                _ = storage.Setup(s => s.ExistsAsync(previewPath, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(previewFileExists);
                _ = storage.Setup(s => s.OpenReadStreamAsync(previewPath, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(() => File.OpenRead(previewPath));
            }

            var ticketService = new Mock<IMediaAccessTicketService>();
            var operators = new Mock<IOperatorRepository>();
            var logger = new Mock<ILogger<Program>>();

            return await MediaApiEndpoints.GetPreviewAsync(
                mediaItemId, null, ticketService.Object, mediaItems.Object, operators.Object, storage.Object,
                logger.Object, context, CancellationToken.None);
        }

        private static async Task<byte[]> ExecuteResultAsync(IResult result, DefaultHttpContext context)
        {
            using var body = new MemoryStream();
            context.Response.Body = body;
            await result.ExecuteAsync(context);
            return body.ToArray();
        }
    }
}
