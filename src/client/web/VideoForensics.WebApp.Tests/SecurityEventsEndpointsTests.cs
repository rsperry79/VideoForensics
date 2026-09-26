using System.Security.Claims;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

using Moq;

using VideoForensics.Api.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Hosting;
using VideoForensics.WebApp.Api;
using VideoForensics.WebApp.Auth;

using Xunit;

namespace VideoForensics.WebApp.Tests
{
    /// <summary>
    /// Cross-account security-event queries require SuperAdmin + Local tier (plan §5.10). This must
    /// be decided from the AUTHENTICATED PRINCIPAL'S NetworkTier claim - set once, correctly, by
    /// PairedDeviceAuthenticationHandler - and never by re-resolving the connection's own tier here,
    /// which for a self-HTTP call from the WebApp's own Blazor UI is always loopback regardless of
    /// where the real browser is (see PairedDeviceAuthenticationHandlerTests' session-tier-header
    /// tests for the handler-side half of this fix).
    /// </summary>
    public class SecurityEventsEndpointsTests
    {
        private static HttpContext CreateAuthenticatedHttpContext(Guid operatorId, NetworkTier claimedTier, OperatorRole role = OperatorRole.SuperAdmin)
        {
            var context = new DefaultHttpContext();
            var claims = new[]
            {
                new Claim(VideoForensicsClaimTypes.OperatorId, operatorId.ToString()),
                new Claim(VideoForensicsClaimTypes.Role, role.ToString()),
                new Claim(VideoForensicsClaimTypes.NetworkTier, claimedTier.ToString())
            };
            context.User = new ClaimsPrincipal(new ClaimsIdentity(claims));
            return context;
        }

        private static async Task<IResult> InvokeAsync(
            SecurityEventsQueryRequest request,
            ISecurityAuditService auditService,
            HttpContext context,
            CancellationToken ct = default)
        {
            var method = typeof(SecurityEventsEndpoints)
                .GetMethod("GetSecurityEventsAsync",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static,
                    null,
                    [typeof(SecurityEventsQueryRequest), typeof(ISecurityAuditService),
                     typeof(HttpContext), typeof(ILogger<Program>), typeof(CancellationToken)],
                    null);

            if (method == null)
            {
                throw new InvalidOperationException("Could not find GetSecurityEventsAsync method");
            }

            ILogger<Program> logger = LoggerFactory.Create(b => { }).CreateLogger<Program>();
            var result = method.Invoke(null, [request, auditService, context, logger, ct]);
            return await (Task<IResult>)result!;
        }

        [Fact]
        public async Task GetSecurityEventsAsync_CrossAccountQuery_ConnectionResolvesLocalButClaimSaysInternet_Denied()
        {
            // Arrange - simulates a self-HTTP call: the request's own connection is loopback (as
            // every self-call is), but the authenticated principal's NetworkTier claim - set from
            // the real browser's tier via the session-tier header - is Internet. The endpoint must
            // trust the claim, not re-resolve the connection.
            var callerId = Guid.NewGuid();
            var targetOperatorId = Guid.NewGuid();
            HttpContext context = CreateAuthenticatedHttpContext(callerId, NetworkTier.Internet);

            var auditService = new Mock<ISecurityAuditService>();

            var request = new SecurityEventsQueryRequest(Limit: 20, Offset: 0, OperatorId: targetOperatorId.ToString());

            // Act
            IResult result = await InvokeAsync(request, auditService.Object, context);

            // Assert
            var statusResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
            Assert.Equal(StatusCodes.Status403Forbidden, statusResult.StatusCode);
        }

        [Fact]
        public async Task GetSecurityEventsAsync_CrossAccountQuery_ClaimSaysLocal_Allowed()
        {
            // Arrange
            var callerId = Guid.NewGuid();
            var targetOperatorId = Guid.NewGuid();
            HttpContext context = CreateAuthenticatedHttpContext(callerId, NetworkTier.Local);

            var auditService = new Mock<ISecurityAuditService>();
            _ = auditService.Setup(a => a.GetOperatorEventsAsync(targetOperatorId, 0, 20, It.IsAny<CancellationToken>()))
                .Returns(EmptyEvents());

            var request = new SecurityEventsQueryRequest(Limit: 20, Offset: 0, OperatorId: targetOperatorId.ToString());

            // Act
            IResult result = await InvokeAsync(request, auditService.Object, context);

            // Assert
            var statusResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
            Assert.Equal(StatusCodes.Status200OK, statusResult.StatusCode);
        }

#pragma warning disable CS1998
        private static async IAsyncEnumerable<VideoForensics.Data.Common.Contracts.SecurityEventDto> EmptyEvents()
        {
            yield break;
        }
#pragma warning restore CS1998
    }
}
