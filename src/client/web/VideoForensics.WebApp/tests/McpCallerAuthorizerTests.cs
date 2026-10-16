namespace VideoForensics.WebApp.Tests;

using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.WebApp.Auth;
using VideoForensics.WebApp.Mcp;
using VideoForensics.WebApp.Mcp.Tools;
using Xunit;

public class McpCallerAuthorizer_Tests
{
    private static McpCallerAuthorizer Create(bool succeeds, HttpContext? httpContext)
    {
        var authorization = new Mock<IAuthorizationService>();
        authorization.Setup(a => a.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object?>(), VideoForensicsPolicies.Admin))
            .ReturnsAsync(succeeds ? AuthorizationResult.Success() : AuthorizationResult.Failed());
        var accessor = new Mock<IHttpContextAccessor>();
        accessor.Setup(a => a.HttpContext).Returns(httpContext);
        return new McpCallerAuthorizer(authorization.Object, accessor.Object);
    }

    private static DefaultHttpContext CallerContext() => new() { User = new ClaimsPrincipal(new ClaimsIdentity("test")) };

    [Fact]
    public async Task McpCallerAuthorizer_AdminCaller_IsAllowed()
    {
        var authorizer = Create(succeeds: true, CallerContext());

        Assert.True(await authorizer.IsAllowedAsync(VideoForensicsPolicies.Admin));
    }

    [Fact]
    public async Task McpCallerAuthorizer_NonAdminCaller_IsDenied()
    {
        var authorizer = Create(succeeds: false, CallerContext());

        Assert.False(await authorizer.IsAllowedAsync(VideoForensicsPolicies.Admin));
    }

    [Fact]
    public async Task McpCallerAuthorizer_NoHttpContext_IsDenied()
    {
        var authorizer = Create(succeeds: true, httpContext: null);

        Assert.False(await authorizer.IsAllowedAsync(VideoForensicsPolicies.Admin));
    }

    [Fact]
    public async Task McpCallerAuthorizer_RequireAsync_NonAdminCaller_Throws()
    {
        var authorizer = Create(succeeds: false, CallerContext());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => authorizer.RequireAsync(VideoForensicsPolicies.Admin));
    }
}

public class McpToolSuccessPath_Tests
{
    [Fact]
    public async Task AuditTrailTools_GetAuditTrailSummary_AdminCaller_QueriesRepository()
    {
        // The Admin path must reach the repository; the denial tests only prove the refusal.
        var repository = new Mock<IAuditTrailRepository>();
        repository.Setup(r => r.GetAuditTrailSummaryAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AuditTrailSummary());
        var authorizer = new Mock<VideoForensics.WebApp.Contracts.IMcpCallerAuthorizer>();
        authorizer.Setup(a => a.RequireAsync(VideoForensicsPolicies.Admin)).Returns(Task.CompletedTask);
        var tools = new AuditTrailTools(repository.Object, authorizer.Object, NullLogger<AuditTrailTools>.Instance);
        Guid locationId = Guid.NewGuid();

        _ = await tools.GetAuditTrailSummary(locationId);

        repository.Verify(r => r.GetAuditTrailSummaryAsync(locationId, It.IsAny<CancellationToken>()), Times.Once);
    }
}
