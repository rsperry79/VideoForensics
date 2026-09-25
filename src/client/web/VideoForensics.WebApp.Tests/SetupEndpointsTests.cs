using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

using Moq;

using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Hosting;
using VideoForensics.WebApp.Api;

using Xunit;

namespace VideoForensics.WebApp.Tests
{
    public class SetupEndpointsTests
    {
        private static (Mock<IOperatorRepository> Operators, Mock<ISecurityAuditLogger> AuditLog, Mock<INetworkTierResolver> TierResolver) CreateMocks(bool operatorsEmpty)
        {
            var operators = new Mock<IOperatorRepository>();
            _ = operators.Setup(o => o.IsEmptyAsync(It.IsAny<CancellationToken>())).ReturnsAsync(operatorsEmpty);
            _ = operators.Setup(o => o.AddAsync(It.IsAny<Operator>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Operator op, CancellationToken _) => op);

            var auditLog = new Mock<ISecurityAuditLogger>();
            var tierResolver = new Mock<INetworkTierResolver>();
            _ = tierResolver.Setup(t => t.ResolveClientIp(It.IsAny<HttpContext>())).Returns("127.0.0.1");

            return (operators, auditLog, tierResolver);
        }

        [Fact]
        public async Task CreateAdminAsync_EmptyOperatorsTable_CreatesSuperAdminAndReturnsOk()
        {
            (Mock<IOperatorRepository> operators, Mock<ISecurityAuditLogger> auditLog, Mock<INetworkTierResolver> tierResolver) = CreateMocks(operatorsEmpty: true);
            var request = new CreateSetupAdminRequest("admin", "a-very-long-password-123");

            IResult result = await SetupEndpoints.CreateAdminAsync(
                request, operators.Object, auditLog.Object, tierResolver.Object, new DefaultHttpContext(), CancellationToken.None);

            var statusResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
            Assert.Equal(StatusCodes.Status200OK, statusResult.StatusCode);
            operators.Verify(o => o.AddAsync(
                It.Is<Operator>(op => op.Username == "admin" && op.Role == OperatorRole.SuperAdmin && op.IsApproved && op.MustChangePassword == false),
                It.IsAny<CancellationToken>()), Times.Once);
            auditLog.Verify(a => a.LogAsync(
                SecurityAuditEventTypes.SetupAdminCreated, It.IsAny<Guid?>(), null, "127.0.0.1", It.IsAny<string>(), true, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task CreateAdminAsync_OperatorAlreadyExists_ReturnsForbid()
        {
            (Mock<IOperatorRepository> operators, Mock<ISecurityAuditLogger> auditLog, Mock<INetworkTierResolver> tierResolver) = CreateMocks(operatorsEmpty: false);
            var request = new CreateSetupAdminRequest("admin", "a-very-long-password-123");

            IResult result = await SetupEndpoints.CreateAdminAsync(
                request, operators.Object, auditLog.Object, tierResolver.Object, new DefaultHttpContext(), CancellationToken.None);

            _ = Assert.IsType<ForbidHttpResult>(result);
            operators.Verify(o => o.AddAsync(It.IsAny<Operator>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task CreateAdminAsync_PasswordTooShort_ReturnsBadRequest()
        {
            (Mock<IOperatorRepository> operators, Mock<ISecurityAuditLogger> auditLog, Mock<INetworkTierResolver> tierResolver) = CreateMocks(operatorsEmpty: true);
            var request = new CreateSetupAdminRequest("admin", "short");

            IResult result = await SetupEndpoints.CreateAdminAsync(
                request, operators.Object, auditLog.Object, tierResolver.Object, new DefaultHttpContext(), CancellationToken.None);

            var statusResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
            Assert.Equal(StatusCodes.Status400BadRequest, statusResult.StatusCode);
            operators.Verify(o => o.AddAsync(It.IsAny<Operator>(), It.IsAny<CancellationToken>()), Times.Never);
        }
    }
}
