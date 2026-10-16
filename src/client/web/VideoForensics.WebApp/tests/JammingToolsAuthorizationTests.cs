namespace VideoForensics.WebApp.Tests;

using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using VideoForensics.Data.Common.Entities;
using VideoForensics.WebApp.Auth;
using VideoForensics.WebApp.Mcp.Tools;
using Xunit;

public class JammingToolsAuthorization_Tests
{
    // The orchestrator is null on purpose: a denied caller must be stopped before any orchestrator call, so a missing
    // check would surface as a NullReferenceException instead of UnauthorizedAccessException.
    private static JammingTools Create(bool adminAllowed, out Mock<IAuthorizationService> authorization)
    {
        authorization = new Mock<IAuthorizationService>();
        authorization.Setup(a => a.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object?>(), VideoForensicsPolicies.Admin))
            .ReturnsAsync(adminAllowed ? AuthorizationResult.Success() : AuthorizationResult.Failed());

        var httpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity()) };
        var accessor = new Mock<IHttpContextAccessor>();
        accessor.Setup(a => a.HttpContext).Returns(httpContext);

        return new JammingTools(null!, authorization.Object, accessor.Object, NullLogger<JammingTools>.Instance);
    }

    [Fact]
    public async Task JammingTools_RecordJammingIncident_NonAdminCaller_ThrowsUnauthorized()
    {
        JammingTools tools = Create(adminAllowed: false, out _);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => tools.RecordJammingIncident(
            Guid.NewGuid(), DateTime.UtcNow.AddHours(-1), DateTime.UtcNow, 3, 6.5, JammingConfidenceLevel.High));
    }

    [Fact]
    public async Task JammingTools_RunJammingDetection_NonAdminCaller_ThrowsUnauthorized()
    {
        JammingTools tools = Create(adminAllowed: false, out _);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => tools.RunJammingDetection(
            Guid.NewGuid(), DateTime.UtcNow.AddHours(-1), DateTime.UtcNow));
    }

    [Fact]
    public async Task JammingTools_RecordJammingIncident_NoHttpContext_ThrowsUnauthorized()
    {
        var authorization = new Mock<IAuthorizationService>();
        var accessor = new Mock<IHttpContextAccessor>();
        accessor.Setup(a => a.HttpContext).Returns((HttpContext?)null);
        var tools = new JammingTools(null!, authorization.Object, accessor.Object, NullLogger<JammingTools>.Instance);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => tools.RecordJammingIncident(
            Guid.NewGuid(), DateTime.UtcNow.AddHours(-1), DateTime.UtcNow, 3, 6.5, JammingConfidenceLevel.High));
    }
}
