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
    /// Range-request behaviour of <see cref="MediaApiEndpoints.GetContentAsync"/>. The handler returns a
    /// stream result with range processing enabled, so the file bytes and Content-Range header are only
    /// produced when the result is executed against a real HttpContext. Each test writes its own temp file.
    /// </summary>
    public sealed class MediaContentRangeTests : IDisposable
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
        public async Task MediaContent_RangeBytes10To19_Returns206WithContentRange()
        {
            string path = CreateTempFileWithKnownBytes();
            var context = CreateAuthenticatedContext();
            context.Request.Headers["Range"] = "bytes=10-19";

            IResult result = await InvokeGetContentAsync(context, path);
            _ = Assert.IsType<FileStreamHttpResult>(result);

            byte[] body = await ExecuteResultAsync(result, context);

            Assert.Equal(StatusCodes.Status206PartialContent, context.Response.StatusCode);
            Assert.Equal("bytes 10-19/100", context.Response.Headers["Content-Range"].ToString());
            Assert.Equal(10, body.Length);
            Assert.Equal(Enumerable.Range(10, 10).Select(i => (byte)i), body);
        }

        [Fact]
        public async Task MediaContent_RangeStartsPastEnd_Returns416()
        {
            string path = CreateTempFileWithKnownBytes();
            var context = CreateAuthenticatedContext();
            context.Request.Headers["Range"] = "bytes=500-600";

            IResult result = await InvokeGetContentAsync(context, path);
            _ = Assert.IsType<FileStreamHttpResult>(result);

            _ = await ExecuteResultAsync(result, context);

            Assert.Equal(StatusCodes.Status416RangeNotSatisfiable, context.Response.StatusCode);
        }

        [Fact]
        public async Task MediaContent_NoRangeHeader_Returns200WithWholeFile()
        {
            string path = CreateTempFileWithKnownBytes();
            var context = CreateAuthenticatedContext();

            IResult result = await InvokeGetContentAsync(context, path);
            _ = Assert.IsType<FileStreamHttpResult>(result);

            byte[] body = await ExecuteResultAsync(result, context);

            Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
            Assert.Equal(FileLength, body.Length);
        }

        private string CreateTempFileWithKnownBytes()
        {
            string path = Path.Combine(Path.GetTempPath(), $"vf-range-{Guid.NewGuid():N}.bin");
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

        private static async Task<IResult> InvokeGetContentAsync(HttpContext context, string filePath)
        {
            Guid mediaItemId = Guid.NewGuid();

            var mediaItems = new Mock<IMediaItemRepository>();
            _ = mediaItems.Setup(m => m.GetAsync(mediaItemId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new MediaItem
                {
                    Id = mediaItemId,
                    DeviceId = Guid.NewGuid(),
                    FileName = "range-test.mp4",
                    FilePath = filePath,
                    MediaFormat = "video/mp4",
                    Sha256Hash = "abc123",
                    RecordedAtUtc = DateTime.UtcNow,
                    DownloadedAtUtc = DateTime.UtcNow
                });

            var storage = new Mock<IMediaStorageProvider>();
            _ = storage.Setup(s => s.ExistsAsync(filePath, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
            _ = storage.Setup(s => s.OpenReadStreamAsync(filePath, It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => File.OpenRead(filePath));

            var auditLog = new Mock<IAccessAuditLogRepository>();
            _ = auditLog.Setup(a => a.RecordAccessAsync(
                mediaItemId,
                It.IsAny<string>(),
                "View",
                "127.0.0.1",
                "In-app media view",
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(new AccessAuditLogEntity
                {
                    Id = Guid.NewGuid(),
                    EvidenceId = mediaItemId,
                    UserId = Guid.NewGuid().ToString(),
                    Action = "View",
                    IpAddress = "127.0.0.1",
                    Purpose = "Test",
                    AccessedAtUtc = DateTime.UtcNow,
                    CreatedAtUtc = DateTime.UtcNow
                });

            var tierResolver = new Mock<INetworkTierResolver>();
            _ = tierResolver.Setup(t => t.ResolveClientIp(It.IsAny<HttpContext>())).Returns("127.0.0.1");

            var ticketService = new Mock<IMediaAccessTicketService>();
            var operators = new Mock<IOperatorRepository>();
            var logger = new Mock<ILogger<Program>>();

            return await MediaApiEndpoints.GetContentAsync(
                mediaItemId, null, ticketService.Object, mediaItems.Object, operators.Object, storage.Object,
                auditLog.Object, tierResolver.Object, logger.Object, context, CancellationToken.None);
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
