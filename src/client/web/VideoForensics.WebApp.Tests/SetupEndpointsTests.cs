using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Logging;

using Moq;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Hosting;
using VideoForensics.WebApp.Api;
using VideoForensics.WebApp.Auth;

using Xunit;

namespace VideoForensics.WebApp.Tests
{
    public class SetupEndpointsTests
    {
        private static (Mock<IOperatorRepository> Operators, Mock<ISecurityAuditLogger> AuditLog, Mock<INetworkTierResolver> TierResolver, Mock<ISessionTierHeaderProtector> HeaderProtector, ILogger<Program> Logger) CreateMocks(bool operatorsEmpty)
        {
            var operators = new Mock<IOperatorRepository>();
            _ = operators.Setup(o => o.IsEmptyAsync(It.IsAny<CancellationToken>())).ReturnsAsync(operatorsEmpty);
            _ = operators.Setup(o => o.AddAsync(It.IsAny<Operator>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Operator op, CancellationToken _) => op);

            var auditLog = new Mock<ISecurityAuditLogger>();
            var tierResolver = new Mock<INetworkTierResolver>();
            _ = tierResolver.Setup(t => t.ResolveClientIp(It.IsAny<HttpContext>())).Returns("127.0.0.1");
            var headerProtector = new Mock<ISessionTierHeaderProtector>();
            var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<Program>.Instance;

            return (operators, auditLog, tierResolver, headerProtector, logger);
        }

        [Fact]
        public async Task CreateAdminAsync_EmptyOperatorsAndLocal_CreatesSuperAdminAndReturnsOk()
        {
            (var operators, var auditLog, var tierResolver, var headerProtector, var logger) = CreateMocks(operatorsEmpty: true);
            var request = new CreateSetupAdminRequest("admin", "a-very-long-password-123");

            IResult result = await SetupEndpoints.CreateAdminAsync(
                request, operators.Object, auditLog.Object, tierResolver.Object, headerProtector.Object, new DefaultHttpContext(), logger, CancellationToken.None);

            var statusResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
            Assert.Equal(StatusCodes.Status200OK, statusResult.StatusCode);
            operators.Verify(o => o.AddAsync(
                It.Is<Operator>(op => op.Username == "admin" && op.Role == OperatorRole.SuperAdmin && op.IsApproved && op.MustChangePassword == false),
                It.IsAny<CancellationToken>()), Times.Once);
            auditLog.Verify(a => a.LogAsync(
                SecurityAuditEventTypes.SetupAdminCreated, It.IsAny<Guid?>(), null, "127.0.0.1", It.IsAny<string>(), true, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task CreateAdminAsync_OperatorAlreadyExists_ReturnsForbidWithErrorMessage()
        {
            (var operators, var auditLog, var tierResolver, var headerProtector, var logger) = CreateMocks(operatorsEmpty: false);
            var request = new CreateSetupAdminRequest("admin", "a-very-long-password-123");

            IResult result = await SetupEndpoints.CreateAdminAsync(
                request, operators.Object, auditLog.Object, tierResolver.Object, headerProtector.Object, new DefaultHttpContext(), logger, CancellationToken.None);

            var statusResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
            Assert.Equal(StatusCodes.Status403Forbidden, statusResult.StatusCode);

            dynamic jsonResult = result;
            Assert.NotNull(jsonResult.Value);
            Assert.Equal("An administrator account already exists. Sign in instead, or contact whoever installed VideoForensics.", (string)jsonResult.Value.error);

            operators.Verify(o => o.AddAsync(It.IsAny<Operator>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task CreateAdminAsync_PasswordTooShort_ReturnsBadRequest()
        {
            (var operators, var auditLog, var tierResolver, var headerProtector, var logger) = CreateMocks(operatorsEmpty: true);
            var request = new CreateSetupAdminRequest("admin", "short");

            IResult result = await SetupEndpoints.CreateAdminAsync(
                request, operators.Object, auditLog.Object, tierResolver.Object, headerProtector.Object, new DefaultHttpContext(), logger, CancellationToken.None);

            var statusResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
            Assert.Equal(StatusCodes.Status400BadRequest, statusResult.StatusCode);
            operators.Verify(o => o.AddAsync(It.IsAny<Operator>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task CreateAdminAsync_EmptyOperatorsButRemote_ReturnsForbidWithErrorMessage()
        {
            (var operators, var auditLog, var tierResolver, var headerProtector, var logger) = CreateMocks(operatorsEmpty: true);
            _ = tierResolver.Setup(t => t.ResolveTier(It.IsAny<HttpContext>())).Returns(NetworkTier.Network);
            var request = new CreateSetupAdminRequest("admin", "a-very-long-password-123");

            IResult result = await SetupEndpoints.CreateAdminAsync(
                request, operators.Object, auditLog.Object, tierResolver.Object, headerProtector.Object, new DefaultHttpContext(), logger, CancellationToken.None);

            var statusResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
            Assert.Equal(StatusCodes.Status403Forbidden, statusResult.StatusCode);

            dynamic jsonResult = result;
            Assert.NotNull(jsonResult.Value);
            Assert.Equal("First-run setup can only be completed from the local machine. Ask whoever has physical access to this server to open it directly.", (string)jsonResult.Value.error);

            operators.Verify(o => o.AddAsync(It.IsAny<Operator>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task GetStatusAsync_OperatorsEmptyAndLocal_ReturnsIsEmptyTrueIsLocalTrue()
        {
            (var operators, _, var tierResolver, var headerProtector, var logger) = CreateMocks(operatorsEmpty: true);
            var context = new DefaultHttpContext();

            IResult result = await SetupEndpoints.GetStatusAsync(
                operators.Object, tierResolver.Object, headerProtector.Object, context, logger, CancellationToken.None);

            var statusResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
            Assert.Equal(StatusCodes.Status200OK, statusResult.StatusCode);

            dynamic jsonResult = result;
            Assert.NotNull(jsonResult.Value);
            Assert.True((bool)jsonResult.Value.isEmpty);
            Assert.True((bool)jsonResult.Value.isLocal);
        }

        [Fact]
        public async Task GetStatusAsync_OperatorsEmptyAndRemote_ReturnsIsEmptyTrueIsLocalFalse()
        {
            (var operators, _, var tierResolver, var headerProtector, var logger) = CreateMocks(operatorsEmpty: true);
            _ = tierResolver.Setup(t => t.ResolveTier(It.IsAny<HttpContext>())).Returns(NetworkTier.Network);
            var context = new DefaultHttpContext();

            IResult result = await SetupEndpoints.GetStatusAsync(
                operators.Object, tierResolver.Object, headerProtector.Object, context, logger, CancellationToken.None);

            var statusResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
            Assert.Equal(StatusCodes.Status200OK, statusResult.StatusCode);

            dynamic jsonResult = result;
            Assert.NotNull(jsonResult.Value);
            Assert.True((bool)jsonResult.Value.isEmpty);
            Assert.False((bool)jsonResult.Value.isLocal);
        }

        [Fact]
        public async Task GetStatusAsync_OperatorsNotEmpty_ReturnsIsEmptyFalse()
        {
            (var operators, _, var tierResolver, var headerProtector, var logger) = CreateMocks(operatorsEmpty: false);
            var context = new DefaultHttpContext();

            IResult result = await SetupEndpoints.GetStatusAsync(
                operators.Object, tierResolver.Object, headerProtector.Object, context, logger, CancellationToken.None);

            var statusResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
            Assert.Equal(StatusCodes.Status200OK, statusResult.StatusCode);

            dynamic jsonResult = result;
            Assert.NotNull(jsonResult.Value);
            Assert.False((bool)jsonResult.Value.isEmpty);
        }
    }
}
