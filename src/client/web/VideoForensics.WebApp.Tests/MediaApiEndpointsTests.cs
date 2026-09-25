using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Logging;

using Moq;

using System.Security.Claims;

using VideoForensics.Api.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Hosting;
using VideoForensics.WebApp.Api;
using VideoForensics.WebApp.Auth;

using Xunit;

namespace VideoForensics.WebApp.Tests
{
    public class MediaApiEndpointsTests
    {
        private static (
            Mock<IMediaAccessTicketService> TicketService,
            Mock<IOperatorRepository> Operators,
            Mock<IAccessAuditLogRepository> AuditLog,
            Mock<INetworkTierResolver> TierResolver,
            Mock<ILogger<Program>> Logger
        ) CreateMocks()
        {
            var ticketService = new Mock<IMediaAccessTicketService>();
            var operators = new Mock<IOperatorRepository>();
            var auditLog = new Mock<IAccessAuditLogRepository>();
            var tierResolver = new Mock<INetworkTierResolver>();
            var logger = new Mock<ILogger<Program>>();

            tierResolver.Setup(t => t.ResolveClientIp(It.IsAny<HttpContext>())).Returns("127.0.0.1");

            return (ticketService, operators, auditLog, tierResolver, logger);
        }

        private Mock<INetworkTierResolver> CreateTierResolverMock()
        {
            var tierResolver = new Mock<INetworkTierResolver>();
            tierResolver.Setup(t => t.ResolveClientIp(It.IsAny<HttpContext>())).Returns("127.0.0.1");
            return tierResolver;
        }

        private ClaimsPrincipal CreatePrincipalWithOperatorId(Guid operatorId)
        {
            var claims = new List<Claim>
            {
                new Claim(VideoForensicsClaimTypes.OperatorId, operatorId.ToString()),
                new Claim(ClaimTypes.Role, OperatorRole.Admin.ToString())
            };
            var identity = new ClaimsIdentity(claims, "test");
            return new ClaimsPrincipal(identity);
        }

        private MediaItem CreateMediaItem(Guid id)
        {
            return new MediaItem
            {
                Id = id,
                DeviceId = Guid.NewGuid(),
                FileName = "test.mp4",
                FilePath = "/path/to/file",
                MediaFormat = "video/mp4",
                Sha256Hash = "abc123",
                RecordedAtUtc = DateTime.UtcNow,
                DownloadedAtUtc = DateTime.UtcNow
            };
        }

        private AccessAuditLogEntity CreateAuditLogEntry()
        {
            return new AccessAuditLogEntity
            {
                Id = Guid.NewGuid(),
                EvidenceId = Guid.NewGuid(),
                UserId = Guid.NewGuid().ToString(),
                Action = "View",
                IpAddress = "127.0.0.1",
                Purpose = "Test",
                AccessedAtUtc = DateTime.UtcNow,
                CreatedAtUtc = DateTime.UtcNow
            };
        }

        #region IssueTicketsAsync Tests

        [Fact]
        public async Task IssueTicketsAsync_NoOperatorClaim_ReturnsUnauthorized()
        {
            (var ticketService, _, _, _, _) = CreateMocks();
            var request = new MediaTicketRequestDto(new[] { Guid.NewGuid() });
            var context = new DefaultHttpContext();

            IResult result = await MediaApiEndpoints.IssueTicketsAsync(
                request, ticketService.Object, context, CancellationToken.None);

            Assert.IsType<UnauthorizedHttpResult>(result);
        }

        [Fact]
        public async Task IssueTicketsAsync_InvalidOperatorClaim_ReturnsUnauthorized()
        {
            (var ticketService, _, _, _, _) = CreateMocks();
            var request = new MediaTicketRequestDto(new[] { Guid.NewGuid() });
            var context = new DefaultHttpContext();
            context.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(VideoForensicsClaimTypes.OperatorId, "not-a-guid")
            }, "test"));

            IResult result = await MediaApiEndpoints.IssueTicketsAsync(
                request, ticketService.Object, context, CancellationToken.None);

            Assert.IsType<UnauthorizedHttpResult>(result);
        }

        [Fact]
        public async Task IssueTicketsAsync_EmptyList_ReturnsEmptyList()
        {
            (var ticketService, _, _, _, _) = CreateMocks();
            var operatorId = Guid.NewGuid();
            var request = new MediaTicketRequestDto(Array.Empty<Guid>());
            var context = new DefaultHttpContext { User = CreatePrincipalWithOperatorId(operatorId) };

            IResult result = await MediaApiEndpoints.IssueTicketsAsync(
                request, ticketService.Object, context, CancellationToken.None);

            // Check it's an Ok result - don't be strict about the generic type
            Assert.NotNull(result);
            Assert.True(result.GetType().Name.StartsWith("Ok`"));
        }

        [Fact]
        public async Task IssueTicketsAsync_MoreThanMaxTickets_ReturnsBadRequest()
        {
            (var ticketService, _, _, _, _) = CreateMocks();
            var operatorId = Guid.NewGuid();
            var ids = Enumerable.Range(0, MediaContentRoutes.MaxTicketsPerRequest + 1)
                .Select(_ => Guid.NewGuid())
                .ToList();
            var request = new MediaTicketRequestDto(ids);
            var context = new DefaultHttpContext { User = CreatePrincipalWithOperatorId(operatorId) };

            IResult result = await MediaApiEndpoints.IssueTicketsAsync(
                request, ticketService.Object, context, CancellationToken.None);

            Assert.IsType<BadRequest>(result);
        }

        [Fact]
        public async Task IssueTicketsAsync_ValidRequest_IssuesTicketsAndReturnsUrls()
        {
            (var ticketService, _, _, _, _) = CreateMocks();
            var operatorId = Guid.NewGuid();
            var id1 = Guid.NewGuid();
            var id2 = Guid.NewGuid();
            var request = new MediaTicketRequestDto(new[] { id1, id2 });
            var context = new DefaultHttpContext { User = CreatePrincipalWithOperatorId(operatorId) };

            var ticket1 = new MediaAccessTicket("token1", DateTime.UtcNow.AddMinutes(10));
            var ticket2 = new MediaAccessTicket("token2", DateTime.UtcNow.AddMinutes(10));

            ticketService.Setup(t => t.Issue(id1, operatorId)).Returns(ticket1);
            ticketService.Setup(t => t.Issue(id2, operatorId)).Returns(ticket2);

            IResult result = await MediaApiEndpoints.IssueTicketsAsync(
                request, ticketService.Object, context, CancellationToken.None);

            var okResult = Assert.IsType<Ok<IReadOnlyList<MediaTicketDto>>>(result);
            Assert.NotNull(okResult.Value);
            Assert.Equal(2, okResult.Value.Count);

            var dto1 = okResult.Value.First(d => d.MediaItemId == id1);
            var dto2 = okResult.Value.First(d => d.MediaItemId == id2);

            Assert.StartsWith($"/api/v1/media/{id1}/content?ticket=", dto1.ContentUrl);
            Assert.StartsWith($"/api/v1/media/{id2}/content?ticket=", dto2.ContentUrl);
            Assert.Equal(ticket1.ExpiresAtUtc, dto1.ExpiresAtUtc);
            Assert.Equal(ticket2.ExpiresAtUtc, dto2.ExpiresAtUtc);

            ticketService.Verify(t => t.Issue(id1, operatorId), Times.Once);
            ticketService.Verify(t => t.Issue(id2, operatorId), Times.Once);
        }

        [Fact]
        public async Task IssueTicketsAsync_DuplicateIds_Deduplicates()
        {
            (var ticketService, _, _, _, _) = CreateMocks();
            var operatorId = Guid.NewGuid();
            var id = Guid.NewGuid();
            var request = new MediaTicketRequestDto(new[] { id, id, id });
            var context = new DefaultHttpContext { User = CreatePrincipalWithOperatorId(operatorId) };

            var ticket = new MediaAccessTicket("token", DateTime.UtcNow.AddMinutes(10));
            ticketService.Setup(t => t.Issue(id, operatorId)).Returns(ticket);

            IResult result = await MediaApiEndpoints.IssueTicketsAsync(
                request, ticketService.Object, context, CancellationToken.None);

            var okResult = Assert.IsType<Ok<IReadOnlyList<MediaTicketDto>>>(result);
            Assert.Single(okResult.Value!);
            ticketService.Verify(t => t.Issue(id, operatorId), Times.Once);
        }

        #endregion

        #region GetContentAsync Tests

        [Fact]
        public async Task GetContentAsync_UnknownMediaId_ReturnsNotFound()
        {
            (var ticketService, _, _, var tierResolver, var logger) = CreateMocks();
            var mediaItemId = Guid.NewGuid();
            var operatorId = Guid.NewGuid();
            var context = new DefaultHttpContext { User = CreatePrincipalWithOperatorId(operatorId) };
            var mediaItems = new Mock<IMediaItemRepository>();
            mediaItems.Setup(m => m.GetAsync(mediaItemId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((MediaItem?)null);
            var operators = new Mock<IOperatorRepository>();

            IResult result = await MediaApiEndpoints.GetContentAsync(
                mediaItemId, null, ticketService.Object, mediaItems.Object, operators.Object, null!,
                null!, tierResolver.Object, logger.Object, context, CancellationToken.None);

            Assert.IsType<NotFound>(result);
        }

        [Fact]
        public async Task GetContentAsync_NoAuthNoTicket_ReturnsUnauthorized()
        {
            (var ticketService, _, _, var tierResolver, var logger) = CreateMocks();
            var mediaItemId = Guid.NewGuid();
            var context = new DefaultHttpContext();
            var mediaItems = new Mock<IMediaItemRepository>();
            var item = CreateMediaItem(mediaItemId);
            mediaItems.Setup(m => m.GetAsync(mediaItemId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(item);

            ticketService.Setup(t => t.Validate(null, mediaItemId)).Returns((Guid?)null);
            var operators = new Mock<IOperatorRepository>();

            IResult result = await MediaApiEndpoints.GetContentAsync(
                mediaItemId, null, ticketService.Object, mediaItems.Object, operators.Object, null!,
                null!, tierResolver.Object, logger.Object, context, CancellationToken.None);

            Assert.IsType<UnauthorizedHttpResult>(result);
        }

        [Fact]
        public async Task GetContentAsync_InvalidTicket_ReturnsUnauthorized()
        {
            (var ticketService, _, _, var tierResolver, var logger) = CreateMocks();
            var mediaItemId = Guid.NewGuid();
            var context = new DefaultHttpContext();
            var mediaItems = new Mock<IMediaItemRepository>();
            var item = CreateMediaItem(mediaItemId);
            mediaItems.Setup(m => m.GetAsync(mediaItemId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(item);

            ticketService.Setup(t => t.Validate("invalid_ticket", mediaItemId)).Returns((Guid?)null);
            var operators = new Mock<IOperatorRepository>();

            IResult result = await MediaApiEndpoints.GetContentAsync(
                mediaItemId, "invalid_ticket", ticketService.Object, mediaItems.Object, operators.Object, null!,
                null!, tierResolver.Object, logger.Object, context, CancellationToken.None);

            Assert.IsType<UnauthorizedHttpResult>(result);
        }

        [Fact]
        public async Task GetContentAsync_ValidTicketButOperatorInactive_ReturnsUnauthorized()
        {
            (var ticketService, var operators, _, var tierResolver, var logger) = CreateMocks();
            var mediaItemId = Guid.NewGuid();
            var operatorId = Guid.NewGuid();
            var context = new DefaultHttpContext();

            var mediaItems = new Mock<IMediaItemRepository>();
            var item = CreateMediaItem(mediaItemId);
            mediaItems.Setup(m => m.GetAsync(mediaItemId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(item);

            ticketService.Setup(t => t.Validate("ticket", mediaItemId)).Returns(operatorId);

            var inactiveOperator = new Operator
            {
                Id = operatorId,
                DisplayName = "Test",
                Username = "test",
                FirstName = "Test",
                LastName = "Operator",
                Email = "test@test.com",
                Active = false,
                IsApproved = true
            };
            operators.Setup(o => o.GetAsync(operatorId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(inactiveOperator);

            var storage = new Mock<IMediaStorageProvider>();
            storage.Setup(s => s.ExistsAsync("/path/to/file", It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
            var auditLog = new Mock<IAccessAuditLogRepository>();

            IResult result = await MediaApiEndpoints.GetContentAsync(
                mediaItemId, "ticket", ticketService.Object, mediaItems.Object, operators.Object, storage.Object,
                auditLog.Object, tierResolver.Object, logger.Object, context, CancellationToken.None);

            Assert.IsType<UnauthorizedHttpResult>(result);
        }

        [Fact]
        public async Task GetContentAsync_ValidTicketButOperatorNotApproved_ReturnsUnauthorized()
        {
            (var ticketService, var operators, _, var tierResolver, var logger) = CreateMocks();
            var mediaItemId = Guid.NewGuid();
            var operatorId = Guid.NewGuid();
            var context = new DefaultHttpContext();

            var mediaItems = new Mock<IMediaItemRepository>();
            var item = CreateMediaItem(mediaItemId);
            mediaItems.Setup(m => m.GetAsync(mediaItemId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(item);

            ticketService.Setup(t => t.Validate("ticket", mediaItemId)).Returns(operatorId);

            var unapprovedOperator = new Operator
            {
                Id = operatorId,
                DisplayName = "Test",
                Username = "test",
                FirstName = "Test",
                LastName = "Operator",
                Email = "test@test.com",
                Active = true,
                IsApproved = false
            };
            operators.Setup(o => o.GetAsync(operatorId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(unapprovedOperator);

            var storage = new Mock<IMediaStorageProvider>();
            storage.Setup(s => s.ExistsAsync("/path/to/file", It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
            var auditLog = new Mock<IAccessAuditLogRepository>();

            IResult result = await MediaApiEndpoints.GetContentAsync(
                mediaItemId, "ticket", ticketService.Object, mediaItems.Object, operators.Object, storage.Object,
                auditLog.Object, tierResolver.Object, logger.Object, context, CancellationToken.None);

            Assert.IsType<UnauthorizedHttpResult>(result);
        }

        [Fact]
        public async Task GetContentAsync_ValidTicketButOperatorMissing_ReturnsUnauthorized()
        {
            (var ticketService, var operators, _, var tierResolver, var logger) = CreateMocks();
            var mediaItemId = Guid.NewGuid();
            var operatorId = Guid.NewGuid();
            var context = new DefaultHttpContext();

            var mediaItems = new Mock<IMediaItemRepository>();
            var item = CreateMediaItem(mediaItemId);
            mediaItems.Setup(m => m.GetAsync(mediaItemId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(item);

            ticketService.Setup(t => t.Validate("ticket", mediaItemId)).Returns(operatorId);
            operators.Setup(o => o.GetAsync(operatorId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((Operator?)null);

            var storage = new Mock<IMediaStorageProvider>();
            storage.Setup(s => s.ExistsAsync("/path/to/file", It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
            var auditLog = new Mock<IAccessAuditLogRepository>();

            IResult result = await MediaApiEndpoints.GetContentAsync(
                mediaItemId, "ticket", ticketService.Object, mediaItems.Object, operators.Object, storage.Object,
                auditLog.Object, tierResolver.Object, logger.Object, context, CancellationToken.None);

            Assert.IsType<UnauthorizedHttpResult>(result);
        }

        [Fact]
        public async Task GetContentAsync_ValidTicket_ServeContentAndAudit()
        {
            (var ticketService, var operators, var auditLog, _, var logger) = CreateMocks();
            var mediaItemId = Guid.NewGuid();
            var operatorId = Guid.NewGuid();
            var context = new DefaultHttpContext();

            var mediaItems = new Mock<IMediaItemRepository>();
            var item = CreateMediaItem(mediaItemId);
            mediaItems.Setup(m => m.GetAsync(mediaItemId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(item);

            ticketService.Setup(t => t.Validate("ticket", mediaItemId)).Returns(operatorId);

            var approvedOperator = new Operator
            {
                Id = operatorId,
                DisplayName = "Test",
                Username = "test",
                FirstName = "Test",
                LastName = "Operator",
                Email = "test@test.com",
                Active = true,
                IsApproved = true
            };
            operators.Setup(o => o.GetAsync(operatorId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(approvedOperator);

            var storage = new Mock<IMediaStorageProvider>();
            storage.Setup(s => s.ExistsAsync("/path/to/file", It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
            storage.Setup(s => s.OpenReadStreamAsync("/path/to/file", It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => new MemoryStream(new byte[] { 1, 2, 3 }));

            var tierResolver = new Mock<INetworkTierResolver>();
            tierResolver.Setup(t => t.ResolveClientIp(It.IsAny<HttpContext>())).Returns("127.0.0.1");

            auditLog.Setup(a => a.RecordAccessAsync(
                mediaItemId,
                operatorId.ToString(),
                "View",
                "127.0.0.1",
                "In-app media view",
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(CreateAuditLogEntry());

            IResult result = await MediaApiEndpoints.GetContentAsync(
                mediaItemId, "ticket", ticketService.Object, mediaItems.Object, operators.Object, storage.Object,
                auditLog.Object, tierResolver.Object, logger.Object, context, CancellationToken.None);

            // Should be a stream result
            Assert.IsType<FileStreamHttpResult>(result);

            // Verify audit was recorded
            auditLog.Verify(a => a.RecordAccessAsync(
                mediaItemId,
                operatorId.ToString(),
                "View",
                "127.0.0.1",
                "In-app media view",
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task GetContentAsync_AuthenticatedUserWithoutTicket_ServeContentAndAudit()
        {
            (var ticketService, var operators, var auditLog, _, var logger) = CreateMocks();
            var mediaItemId = Guid.NewGuid();
            var operatorId = Guid.NewGuid();
            var context = new DefaultHttpContext { User = CreatePrincipalWithOperatorId(operatorId) };

            var mediaItems = new Mock<IMediaItemRepository>();
            var item = CreateMediaItem(mediaItemId);
            mediaItems.Setup(m => m.GetAsync(mediaItemId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(item);

            var approvedOperator = new Operator
            {
                Id = operatorId,
                DisplayName = "Test",
                Username = "test",
                FirstName = "Test",
                LastName = "Operator",
                Email = "test@test.com",
                Active = true,
                IsApproved = true
            };
            operators.Setup(o => o.GetAsync(operatorId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(approvedOperator);

            var storage = new Mock<IMediaStorageProvider>();
            storage.Setup(s => s.ExistsAsync("/path/to/file", It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
            storage.Setup(s => s.OpenReadStreamAsync("/path/to/file", It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => new MemoryStream(new byte[] { 1, 2, 3 }));

            var tierResolver = new Mock<INetworkTierResolver>();
            tierResolver.Setup(t => t.ResolveClientIp(It.IsAny<HttpContext>())).Returns("127.0.0.1");

            auditLog.Setup(a => a.RecordAccessAsync(
                mediaItemId,
                operatorId.ToString(),
                "View",
                "127.0.0.1",
                "In-app media view",
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(CreateAuditLogEntry());

            IResult result = await MediaApiEndpoints.GetContentAsync(
                mediaItemId, null, ticketService.Object, mediaItems.Object, operators.Object, storage.Object,
                auditLog.Object, tierResolver.Object, logger.Object, context, CancellationToken.None);

            Assert.IsType<FileStreamHttpResult>(result);

            // Should audit authenticated request
            auditLog.Verify(a => a.RecordAccessAsync(
                mediaItemId,
                operatorId.ToString(),
                "View",
                "127.0.0.1",
                "In-app media view",
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task GetContentAsync_RangeHeaderBytes1000_ServeButNotAudit()
        {
            (var ticketService, var operators, var auditLog, _, var logger) = CreateMocks();
            var mediaItemId = Guid.NewGuid();
            var operatorId = Guid.NewGuid();
            var context = new DefaultHttpContext
            {
                User = CreatePrincipalWithOperatorId(operatorId)
            };
            context.Request.Headers.Append("Range", "bytes=1000-2000");

            var mediaItems = new Mock<IMediaItemRepository>();
            var item = CreateMediaItem(mediaItemId);
            mediaItems.Setup(m => m.GetAsync(mediaItemId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(item);

            var approvedOperator = new Operator
            {
                Id = operatorId,
                DisplayName = "Test",
                Username = "test",
                FirstName = "Test",
                LastName = "Operator",
                Email = "test@test.com",
                Active = true,
                IsApproved = true
            };
            operators.Setup(o => o.GetAsync(operatorId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(approvedOperator);

            var storage = new Mock<IMediaStorageProvider>();
            storage.Setup(s => s.ExistsAsync("/path/to/file", It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
            storage.Setup(s => s.OpenReadStreamAsync("/path/to/file", It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => new MemoryStream(new byte[] { 1, 2, 3 }));

            var tierResolver = new Mock<INetworkTierResolver>();
            tierResolver.Setup(t => t.ResolveClientIp(It.IsAny<HttpContext>())).Returns("127.0.0.1");

            IResult result = await MediaApiEndpoints.GetContentAsync(
                mediaItemId, null, ticketService.Object, mediaItems.Object, operators.Object, storage.Object,
                auditLog.Object, tierResolver.Object, logger.Object, context, CancellationToken.None);

            Assert.IsType<FileStreamHttpResult>(result);

            // Should NOT audit range request not starting at byte 0
            auditLog.Verify(a => a.RecordAccessAsync(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task GetContentAsync_RangeHeaderBytes0_Audit()
        {
            (var ticketService, var operators, var auditLog, _, var logger) = CreateMocks();
            var mediaItemId = Guid.NewGuid();
            var operatorId = Guid.NewGuid();
            var context = new DefaultHttpContext
            {
                User = CreatePrincipalWithOperatorId(operatorId)
            };
            context.Request.Headers.Append("Range", "bytes=0-");

            var mediaItems = new Mock<IMediaItemRepository>();
            var item = CreateMediaItem(mediaItemId);
            mediaItems.Setup(m => m.GetAsync(mediaItemId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(item);

            var approvedOperator = new Operator
            {
                Id = operatorId,
                DisplayName = "Test",
                Username = "test",
                FirstName = "Test",
                LastName = "Operator",
                Email = "test@test.com",
                Active = true,
                IsApproved = true
            };
            operators.Setup(o => o.GetAsync(operatorId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(approvedOperator);

            var storage = new Mock<IMediaStorageProvider>();
            storage.Setup(s => s.ExistsAsync("/path/to/file", It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
            storage.Setup(s => s.OpenReadStreamAsync("/path/to/file", It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => new MemoryStream(new byte[] { 1, 2, 3 }));

            var tierResolver = new Mock<INetworkTierResolver>();
            tierResolver.Setup(t => t.ResolveClientIp(It.IsAny<HttpContext>())).Returns("127.0.0.1");

            auditLog.Setup(a => a.RecordAccessAsync(
                mediaItemId,
                operatorId.ToString(),
                "View",
                "127.0.0.1",
                "In-app media view",
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(CreateAuditLogEntry());

            IResult result = await MediaApiEndpoints.GetContentAsync(
                mediaItemId, null, ticketService.Object, mediaItems.Object, operators.Object, storage.Object,
                auditLog.Object, tierResolver.Object, logger.Object, context, CancellationToken.None);

            Assert.IsType<FileStreamHttpResult>(result);

            // Should audit range request starting at byte 0
            auditLog.Verify(a => a.RecordAccessAsync(
                mediaItemId,
                operatorId.ToString(),
                "View",
                "127.0.0.1",
                "In-app media view",
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task GetContentAsync_AuditThrows_Returns500()
        {
            (var ticketService, var operators, var auditLog, _, var logger) = CreateMocks();
            var mediaItemId = Guid.NewGuid();
            var operatorId = Guid.NewGuid();
            var context = new DefaultHttpContext { User = CreatePrincipalWithOperatorId(operatorId) };

            var mediaItems = new Mock<IMediaItemRepository>();
            var item = CreateMediaItem(mediaItemId);
            mediaItems.Setup(m => m.GetAsync(mediaItemId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(item);

            var approvedOperator = new Operator
            {
                Id = operatorId,
                DisplayName = "Test",
                Username = "test",
                FirstName = "Test",
                LastName = "Operator",
                Email = "test@test.com",
                Active = true,
                IsApproved = true
            };
            operators.Setup(o => o.GetAsync(operatorId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(approvedOperator);

            var storage = new Mock<IMediaStorageProvider>();
            storage.Setup(s => s.ExistsAsync("/path/to/file", It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
            storage.Setup(s => s.OpenReadStreamAsync("/path/to/file", It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => new MemoryStream(new byte[] { 1, 2, 3 }));

            var tierResolver = new Mock<INetworkTierResolver>();
            tierResolver.Setup(t => t.ResolveClientIp(It.IsAny<HttpContext>())).Returns("127.0.0.1");

            auditLog.Setup(a => a.RecordAccessAsync(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Audit failed"));

            IResult result = await MediaApiEndpoints.GetContentAsync(
                mediaItemId, null, ticketService.Object, mediaItems.Object, operators.Object, storage.Object,
                auditLog.Object, tierResolver.Object, logger.Object, context, CancellationToken.None);

            var problemResult = Assert.IsType<ProblemHttpResult>(result);
            Assert.Equal(StatusCodes.Status500InternalServerError, problemResult.StatusCode);
        }

        [Fact]
        public async Task GetContentAsync_FileNotOnDisk_ReturnsNotFound()
        {
            (var ticketService, _, _, var tierResolver, var logger) = CreateMocks();
            var mediaItemId = Guid.NewGuid();
            var context = new DefaultHttpContext { User = CreatePrincipalWithOperatorId(Guid.NewGuid()) };

            var mediaItems = new Mock<IMediaItemRepository>();
            var item = CreateMediaItem(mediaItemId);
            item.FilePath = "/nonexistent/file";
            mediaItems.Setup(m => m.GetAsync(mediaItemId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(item);

            var storage = new Mock<IMediaStorageProvider>();
            storage.Setup(s => s.ExistsAsync("/nonexistent/file", It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            var auditLog = new Mock<IAccessAuditLogRepository>();

            IResult result = await MediaApiEndpoints.GetContentAsync(
                mediaItemId, null, ticketService.Object, mediaItems.Object, null!, storage.Object,
                auditLog.Object, tierResolver.Object, logger.Object, context, CancellationToken.None);

            Assert.IsType<NotFound>(result);

            // Should NOT audit on file not found
            auditLog.Verify(a => a.RecordAccessAsync(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task GetContentAsync_TicketNoIdentity_InactiveOperator_ReturnsUnauthorized()
        {
            (var ticketService, var operators, _, var tierResolver, var logger) = CreateMocks();
            var mediaItemId = Guid.NewGuid();
            var operatorId = Guid.NewGuid();
            // context.User is new ClaimsPrincipal() - no identity at all
            var context = new DefaultHttpContext { User = new ClaimsPrincipal() };

            var mediaItems = new Mock<IMediaItemRepository>();
            var item = CreateMediaItem(mediaItemId);
            mediaItems.Setup(m => m.GetAsync(mediaItemId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(item);

            ticketService.Setup(t => t.Validate("ticket", mediaItemId)).Returns(operatorId);

            var inactiveOperator = new Operator
            {
                Id = operatorId,
                DisplayName = "Test",
                Username = "test",
                FirstName = "Test",
                LastName = "Operator",
                Email = "test@test.com",
                Active = false,
                IsApproved = true
            };
            operators.Setup(o => o.GetAsync(operatorId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(inactiveOperator);

            var storage = new Mock<IMediaStorageProvider>();
            storage.Setup(s => s.ExistsAsync("/path/to/file", It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
            var auditLog = new Mock<IAccessAuditLogRepository>();

            IResult result = await MediaApiEndpoints.GetContentAsync(
                mediaItemId, "ticket", ticketService.Object, mediaItems.Object, operators.Object, storage.Object,
                auditLog.Object, tierResolver.Object, logger.Object, context, CancellationToken.None);

            Assert.IsType<UnauthorizedHttpResult>(result);
        }

        [Fact]
        public async Task GetContentAsync_AuthenticatedNoClaim_TicketForInactiveOperator_ReturnsUnauthorized()
        {
            (var ticketService, var operators, _, var tierResolver, var logger) = CreateMocks();
            var mediaItemId = Guid.NewGuid();
            var operatorId = Guid.NewGuid();
            // Authenticated principal but NO OperatorId claim
            var context = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.Role, OperatorRole.Admin.ToString())
                }, "test"))
            };

            var mediaItems = new Mock<IMediaItemRepository>();
            var item = CreateMediaItem(mediaItemId);
            mediaItems.Setup(m => m.GetAsync(mediaItemId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(item);

            ticketService.Setup(t => t.Validate("ticket", mediaItemId)).Returns(operatorId);

            var inactiveOperator = new Operator
            {
                Id = operatorId,
                DisplayName = "Test",
                Username = "test",
                FirstName = "Test",
                LastName = "Operator",
                Email = "test@test.com",
                Active = false,
                IsApproved = true
            };
            operators.Setup(o => o.GetAsync(operatorId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(inactiveOperator);

            var storage = new Mock<IMediaStorageProvider>();
            storage.Setup(s => s.ExistsAsync("/path/to/file", It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
            var auditLog = new Mock<IAccessAuditLogRepository>();

            IResult result = await MediaApiEndpoints.GetContentAsync(
                mediaItemId, "ticket", ticketService.Object, mediaItems.Object, operators.Object, storage.Object,
                auditLog.Object, tierResolver.Object, logger.Object, context, CancellationToken.None);

            Assert.IsType<UnauthorizedHttpResult>(result);
        }

        [Fact]
        public async Task GetContentAsync_AuthenticatedNoClaim_TicketForActiveApprovedOperator_ServeContent()
        {
            (var ticketService, var operators, var auditLog, _, var logger) = CreateMocks();
            var mediaItemId = Guid.NewGuid();
            var operatorId = Guid.NewGuid();
            // Authenticated principal but NO OperatorId claim
            var context = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.Role, OperatorRole.Admin.ToString())
                }, "test"))
            };

            var mediaItems = new Mock<IMediaItemRepository>();
            var item = CreateMediaItem(mediaItemId);
            mediaItems.Setup(m => m.GetAsync(mediaItemId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(item);

            ticketService.Setup(t => t.Validate("ticket", mediaItemId)).Returns(operatorId);

            var activeApprovedOperator = new Operator
            {
                Id = operatorId,
                DisplayName = "Test",
                Username = "test",
                FirstName = "Test",
                LastName = "Operator",
                Email = "test@test.com",
                Active = true,
                IsApproved = true
            };
            operators.Setup(o => o.GetAsync(operatorId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(activeApprovedOperator);

            var storage = new Mock<IMediaStorageProvider>();
            storage.Setup(s => s.ExistsAsync("/path/to/file", It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
            storage.Setup(s => s.OpenReadStreamAsync("/path/to/file", It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => new MemoryStream(new byte[] { 1, 2, 3 }));

            var tierResolver = new Mock<INetworkTierResolver>();
            tierResolver.Setup(t => t.ResolveClientIp(It.IsAny<HttpContext>())).Returns("127.0.0.1");

            auditLog.Setup(a => a.RecordAccessAsync(
                mediaItemId,
                operatorId.ToString(),
                "View",
                "127.0.0.1",
                "In-app media view",
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(CreateAuditLogEntry());

            IResult result = await MediaApiEndpoints.GetContentAsync(
                mediaItemId, "ticket", ticketService.Object, mediaItems.Object, operators.Object, storage.Object,
                auditLog.Object, tierResolver.Object, logger.Object, context, CancellationToken.None);

            // Should serve content even though authenticated user has no OperatorId claim (falls back to ticket)
            Assert.IsType<FileStreamHttpResult>(result);

            // Should audit the request
            auditLog.Verify(a => a.RecordAccessAsync(
                mediaItemId,
                operatorId.ToString(),
                "View",
                "127.0.0.1",
                "In-app media view",
                It.IsAny<CancellationToken>()), Times.Once);
        }

        #endregion

        #region ListMediaItemsAsync Tests

        [Fact]
        public async Task ListMediaItemsAsync_DeviceIdAndDateRange_CallsRepositoryWithCorrectParameters()
        {
            var deviceId = Guid.NewGuid();
            var fromUtc = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var toUtc = new DateTime(2024, 1, 31, 23, 59, 59, DateTimeKind.Utc);

            var items = new List<MediaItem>
            {
                new MediaItem
                {
                    Id = Guid.NewGuid(),
                    DeviceId = deviceId,
                    FileName = "test1.mp4",
                    FilePath = "/path/test1.mp4",
                    MediaFormat = "video/mp4",
                    Sha256Hash = "hash1",
                    RecordedAtUtc = new DateTime(2024, 1, 15, 12, 0, 0, DateTimeKind.Utc),
                    DownloadedAtUtc = new DateTime(2024, 1, 15, 12, 5, 0, DateTimeKind.Utc)
                }
            };

            var mediaItemsRepo = new Mock<IMediaItemRepository>();
            mediaItemsRepo
                .Setup(m => m.GetByDeviceAndDateRangeAsync(deviceId, fromUtc, toUtc, It.IsAny<CancellationToken>()))
                .ReturnsAsync(items);

            IResult result = await MediaApiEndpoints.ListMediaItemsAsync(
                deviceId, fromUtc, toUtc, mediaItemsRepo.Object, CancellationToken.None);

            var okResult = Assert.IsType<Ok<IEnumerable<MediaItemDto>>>(result);
            var dtos = okResult.Value?.ToList();
            Assert.NotNull(dtos);
            Assert.Single(dtos);
            Assert.Equal(items[0].Id, dtos[0].Id);

            mediaItemsRepo.Verify(
                m => m.GetByDeviceAndDateRangeAsync(deviceId, fromUtc, toUtc, It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task ListMediaItemsAsync_DeviceIdOnly_CallsGetByDeviceIdAsync()
        {
            var deviceId = Guid.NewGuid();
            var items = new List<MediaItem>
            {
                new MediaItem
                {
                    Id = Guid.NewGuid(),
                    DeviceId = deviceId,
                    FileName = "test.mp4",
                    FilePath = "/path/test.mp4",
                    MediaFormat = "video/mp4",
                    Sha256Hash = "hash1",
                    RecordedAtUtc = DateTime.UtcNow,
                    DownloadedAtUtc = DateTime.UtcNow
                }
            };

            var mediaItemsRepo = new Mock<IMediaItemRepository>();
            mediaItemsRepo
                .Setup(m => m.GetByDeviceIdAsync(deviceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(items);

            IResult result = await MediaApiEndpoints.ListMediaItemsAsync(
                deviceId, null, null, mediaItemsRepo.Object, CancellationToken.None);

            var okResult = Assert.IsType<Ok<IEnumerable<MediaItemDto>>>(result);
            Assert.NotNull(okResult.Value);

            mediaItemsRepo.Verify(
                m => m.GetByDeviceIdAsync(deviceId, It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task ListMediaItemsAsync_NoDeviceId_CallsListAsync()
        {
            var items = new List<MediaItem>
            {
                new MediaItem
                {
                    Id = Guid.NewGuid(),
                    DeviceId = Guid.NewGuid(),
                    FileName = "test.mp4",
                    FilePath = "/path/test.mp4",
                    MediaFormat = "video/mp4",
                    Sha256Hash = "hash1",
                    RecordedAtUtc = DateTime.UtcNow,
                    DownloadedAtUtc = DateTime.UtcNow
                }
            };

            var mediaItemsRepo = new Mock<IMediaItemRepository>();
            mediaItemsRepo
                .Setup(m => m.ListAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(items);

            IResult result = await MediaApiEndpoints.ListMediaItemsAsync(
                null, null, null, mediaItemsRepo.Object, CancellationToken.None);

            var okResult = Assert.IsType<Ok<IEnumerable<MediaItemDto>>>(result);
            Assert.NotNull(okResult.Value);

            mediaItemsRepo.Verify(
                m => m.ListAsync(It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task ListMediaItemsAsync_FromWithoutDeviceId_ReturnsBadRequest()
        {
            var fromUtc = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var mediaItemsRepo = new Mock<IMediaItemRepository>();

            IResult result = await MediaApiEndpoints.ListMediaItemsAsync(
                null, fromUtc, null, mediaItemsRepo.Object, CancellationToken.None);

            Assert.IsType<BadRequest>(result);
        }

        [Fact]
        public async Task ListMediaItemsAsync_ToWithoutDeviceId_ReturnsBadRequest()
        {
            var toUtc = new DateTime(2024, 1, 31, 23, 59, 59, DateTimeKind.Utc);
            var mediaItemsRepo = new Mock<IMediaItemRepository>();

            IResult result = await MediaApiEndpoints.ListMediaItemsAsync(
                null, null, toUtc, mediaItemsRepo.Object, CancellationToken.None);

            Assert.IsType<BadRequest>(result);
        }

        [Fact]
        public async Task ListMediaItemsAsync_FromGreaterThanTo_ReturnsBadRequest()
        {
            var deviceId = Guid.NewGuid();
            var fromUtc = new DateTime(2024, 1, 31, 0, 0, 0, DateTimeKind.Utc);
            var toUtc = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var mediaItemsRepo = new Mock<IMediaItemRepository>();

            IResult result = await MediaApiEndpoints.ListMediaItemsAsync(
                deviceId, fromUtc, toUtc, mediaItemsRepo.Object, CancellationToken.None);

            Assert.IsType<BadRequest>(result);
        }

        #endregion

        #region GetMediaItemAsync Tests

        [Fact]
        public async Task GetMediaItemAsync_MediaItemExists_ReturnsOkWithDto()
        {
            var mediaItemId = Guid.NewGuid();
            var item = new MediaItem
            {
                Id = mediaItemId,
                DeviceId = Guid.NewGuid(),
                FileName = "test.mp4",
                FilePath = "/path/test.mp4",
                MediaFormat = "video/mp4",
                Sha256Hash = "hash1",
                RecordedAtUtc = DateTime.UtcNow,
                DownloadedAtUtc = DateTime.UtcNow
            };

            var mediaItemsRepo = new Mock<IMediaItemRepository>();
            mediaItemsRepo
                .Setup(m => m.GetAsync(mediaItemId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(item);

            IResult result = await MediaApiEndpoints.GetMediaItemAsync(
                mediaItemId, mediaItemsRepo.Object, CancellationToken.None);

            var okResult = Assert.IsType<Ok<MediaItemDto>>(result);
            Assert.NotNull(okResult.Value);
            Assert.Equal(mediaItemId, okResult.Value.Id);
            Assert.Equal(item.FileName, okResult.Value.FileName);
        }

        [Fact]
        public async Task GetMediaItemAsync_MediaItemNotFound_ReturnsNotFound()
        {
            var mediaItemId = Guid.NewGuid();
            var mediaItemsRepo = new Mock<IMediaItemRepository>();
            mediaItemsRepo
                .Setup(m => m.GetAsync(mediaItemId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((MediaItem?)null);

            IResult result = await MediaApiEndpoints.GetMediaItemAsync(
                mediaItemId, mediaItemsRepo.Object, CancellationToken.None);

            Assert.IsType<NotFound>(result);
        }

        #endregion
    }
}
