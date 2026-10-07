using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Moq;
using VideoForensics.Api.Contracts;
using VideoForensics.Client.Common.Contracts;
using VideoForensics.Core.Logging.Services;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Hosting;
using VideoForensics.WebApp.Api;
using VideoForensics.WebApp.Auth;
using Xunit;

namespace VideoForensics.WebApp.Tests
{
    public class LogEndpointsTests
    {
        private static (InMemoryLogBuffer Buffer, Mock<ISecurityAuditLogger> AuditLog, Mock<INetworkTierResolver> TierResolver) CreateMocks()
        {
            var buffer = new InMemoryLogBuffer(capacity: 5000);
            var auditLog = new Mock<ISecurityAuditLogger>();
            _ = auditLog.Setup(al => al.LogAsync(It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

            var tierResolver = new Mock<INetworkTierResolver>();
            _ = tierResolver.Setup(t => t.ResolveClientIp(It.IsAny<HttpContext>())).Returns("127.0.0.1");

            return (buffer, auditLog, tierResolver);
        }

        private static DefaultHttpContext CreateHttpContextWithStepUpToken(Guid pairedDeviceId, string stepUpToken = "valid-step-up-token")
        {
            var context = new DefaultHttpContext();
            var claims = new[]
            {
                new Claim(VideoForensicsClaimTypes.PairedDeviceId, pairedDeviceId.ToString()),
                new Claim(VideoForensicsClaimTypes.OperatorId, Guid.NewGuid().ToString())
            };
            var identity = new ClaimsIdentity(claims);
            context.User = new ClaimsPrincipal(identity);
            context.Request.Headers["X-StepUp-Token"] = stepUpToken;
            return context;
        }

        [Fact]
        public async Task GetLogsAsync_ValidRequest_ReturnsLogPageDto()
        {
            (var buffer, var auditLog, var tierResolver) = CreateMocks();
            Guid pairedDeviceId = Guid.NewGuid();
            var context = CreateHttpContextWithStepUpToken(pairedDeviceId);

            // Add entries to buffer
            var entry1 = new LogRecord(0, DateTimeOffset.UtcNow.AddSeconds(-10), "Information", "TestCategory", "Test message 1", null);
            var entry2 = new LogRecord(0, DateTimeOffset.UtcNow, "Warning", "TestCategory", "Test message 2", null);
            buffer.Append(ref entry1);
            buffer.Append(ref entry2);

            var query = new LogQueryDto(null, null, null, 500);
            IResult result = await LogEndpoints.GetLogsAsync(query, buffer, auditLog.Object, tierResolver.Object, context, CancellationToken.None);

            var okResult = Assert.IsType<Ok<LogPageDto>>(result);
            var pageDto = okResult.Value;

            Assert.NotNull(pageDto);
            Assert.Equal(2, pageDto.Entries.Count);
            Assert.Equal(2, pageDto.LatestSequence);
            Assert.False(pageDto.Truncated);
            Assert.Equal(1, pageDto.Entries[0].Sequence);
            Assert.Equal(2, pageDto.Entries[1].Sequence);
            Assert.Equal("Information", pageDto.Entries[0].Level);
            Assert.Equal("Warning", pageDto.Entries[1].Level);

            // Verify audit log was called
            auditLog.Verify(al => al.LogAsync(
                SecurityAuditEventTypes.LogViewed,
                It.IsAny<Guid?>(),
                It.IsAny<Guid?>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Theory]
        [InlineData(0, 500)]
        [InlineData(-5, 500)]
        [InlineData(1, 1)]
        [InlineData(2000, 2000)]
        [InlineData(5000, 2000)]
        public async Task GetLogsAsync_Limit_IsClampedWithZeroMeaningDefault(int limit, int expectedCount)
        {
            (var buffer, var auditLog, var tierResolver) = CreateMocks();
            var context = CreateHttpContextWithStepUpToken(Guid.NewGuid());
            var entry = new LogRecord(0, DateTimeOffset.UtcNow, "Information", "Test", "Test", null);
            for (int i = 0; i < 2100; i++)
            {
                buffer.Append(ref entry);
            }

            var result = await LogEndpoints.GetLogsAsync(new LogQueryDto(null, null, null, limit), buffer, auditLog.Object, tierResolver.Object, context, CancellationToken.None);

            var okResult = Assert.IsType<Ok<LogPageDto>>(result);
            Assert.Equal(expectedCount, okResult.Value!.Entries.Count);
            Assert.True(okResult.Value.Truncated);
        }
        [Fact]
        public async Task GetLogsAsync_InvalidMinLevel_ReturnsBadRequest()
        {
            (var buffer, var auditLog, var tierResolver) = CreateMocks();
            Guid pairedDeviceId = Guid.NewGuid();
            var context = CreateHttpContextWithStepUpToken(pairedDeviceId);

            var query = new LogQueryDto("InvalidLevel", null, null, 500);
            IResult result = await LogEndpoints.GetLogsAsync(query, buffer, auditLog.Object, tierResolver.Object, context, CancellationToken.None);

            var badRequest = Assert.IsType<BadRequest<ProblemDetails>>(result);
            Assert.NotNull(badRequest.Value);
            Assert.Equal(400, badRequest.Value.Status);
            Assert.Contains("MinLevel", badRequest.Value.Detail);
        }

        [Fact]
        public async Task GetLogsAsync_TruncatedFlag_SetWhenLimitExceeded()
        {
            (var buffer, var auditLog, var tierResolver) = CreateMocks();
            Guid pairedDeviceId = Guid.NewGuid();
            var context = CreateHttpContextWithStepUpToken(pairedDeviceId);

            // Add 501 entries to exceed the 500 limit
            var entry = new LogRecord(0, DateTimeOffset.UtcNow, "Information", "Category", "Message", null);
            for (int i = 0; i < 501; i++)
            {
                buffer.Append(ref entry);
            }

            var query = new LogQueryDto(null, null, null, 500);
            IResult result = await LogEndpoints.GetLogsAsync(query, buffer, auditLog.Object, tierResolver.Object, context, CancellationToken.None);

            var okResult = Assert.IsType<Ok<LogPageDto>>(result);
            Assert.True(okResult.Value.Truncated);
            Assert.Equal(500, okResult.Value.Entries.Count);
        }

        [Fact]
        public async Task GetLogsAsync_WithMinLevel_FiltersEntries()
        {
            (var buffer, var auditLog, var tierResolver) = CreateMocks();
            Guid pairedDeviceId = Guid.NewGuid();
            var context = CreateHttpContextWithStepUpToken(pairedDeviceId);

            // Add entries with different levels
            var infoEntry = new LogRecord(0, DateTimeOffset.UtcNow, "Information", "Category", "Info message", null);
            var warningEntry = new LogRecord(0, DateTimeOffset.UtcNow, "Warning", "Category", "Warning message", null);
            buffer.Append(ref infoEntry);
            buffer.Append(ref warningEntry);

            // Query for Warning level or higher
            var query = new LogQueryDto("Warning", null, null, 500);
            var result = await LogEndpoints.GetLogsAsync(query, buffer, auditLog.Object, tierResolver.Object, context, CancellationToken.None);

            var okResult = Assert.IsType<Ok<LogPageDto>>(result);
            // Only warning entry should be returned
            Assert.Single(okResult.Value.Entries);
            Assert.Equal("Warning", okResult.Value.Entries[0].Level);
        }

        [Fact]
        public async Task GetLogsAsync_WithAfterSequence_ResumsFromSequence()
        {
            (var buffer, var auditLog, var tierResolver) = CreateMocks();
            Guid pairedDeviceId = Guid.NewGuid();
            var context = CreateHttpContextWithStepUpToken(pairedDeviceId);

            // Add three entries
            var entry1 = new LogRecord(0, DateTimeOffset.UtcNow, "Information", "Cat", "Msg1", null);
            var entry2 = new LogRecord(0, DateTimeOffset.UtcNow, "Information", "Cat", "Msg2", null);
            var entry3 = new LogRecord(0, DateTimeOffset.UtcNow, "Information", "Cat", "Msg3", null);
            buffer.Append(ref entry1);
            buffer.Append(ref entry2);
            buffer.Append(ref entry3);

            // Query with afterSequence = 1 should return only entries 2 and 3
            var query = new LogQueryDto(null, null, 1, 500);
            var result = await LogEndpoints.GetLogsAsync(query, buffer, auditLog.Object, tierResolver.Object, context, CancellationToken.None);

            var okResult = Assert.IsType<Ok<LogPageDto>>(result);
            Assert.Equal(2, okResult.Value.Entries.Count);
            Assert.Equal(2, okResult.Value.Entries[0].Sequence);
            Assert.Equal(3, okResult.Value.Entries[1].Sequence);
        }
    }
}