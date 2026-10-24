
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

using Moq;

using System.Security.Claims;

using VideoForensics.Client.WebApp.Auth;
using VideoForensics.Client.WebApp.Contracts;
using VideoForensics.Client.WebApp.Mcp;
using VideoForensics.Client.WebApp.Mcp.Tools;
using VideoForensics.Data.Common.Contracts;

using Xunit;

namespace VideoForensics.Client.WebApp.Tests;

public class McpCallerAuthorizer_Tests
{
    private static McpCallerAuthorizer Create(bool succeeds, HttpContext? httpContext)
    {
        var authorization = new Mock<IAuthorizationService>();
        _ = authorization.Setup(a => a.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object?>(), VideoForensicsPolicies.Admin))
            .ReturnsAsync(succeeds ? AuthorizationResult.Success() : AuthorizationResult.Failed());
        var accessor = new Mock<IHttpContextAccessor>();
        _ = accessor.Setup(a => a.HttpContext).Returns(httpContext);
        return new McpCallerAuthorizer(authorization.Object, accessor.Object);
    }

    private static DefaultHttpContext CallerContext()
    {
        return new() { User = new ClaimsPrincipal(new ClaimsIdentity("test")) };
    }

    [Fact]
    public async Task McpCallerAuthorizer_AdminCaller_IsAllowed()
    {
        McpCallerAuthorizer authorizer = Create(succeeds: true, CallerContext());

        Assert.True(await authorizer.IsAllowedAsync(VideoForensicsPolicies.Admin));
    }

    [Fact]
    public async Task McpCallerAuthorizer_NonAdminCaller_IsDenied()
    {
        McpCallerAuthorizer authorizer = Create(succeeds: false, CallerContext());

        Assert.False(await authorizer.IsAllowedAsync(VideoForensicsPolicies.Admin));
    }

    [Fact]
    public async Task McpCallerAuthorizer_NoHttpContext_IsDenied()
    {
        McpCallerAuthorizer authorizer = Create(succeeds: true, httpContext: null);

        Assert.False(await authorizer.IsAllowedAsync(VideoForensicsPolicies.Admin));
    }

    [Fact]
    public async Task McpCallerAuthorizer_RequireAsync_NonAdminCaller_Throws()
    {
        McpCallerAuthorizer authorizer = Create(succeeds: false, CallerContext());

        _ = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => authorizer.RequireAsync(VideoForensicsPolicies.Admin));
    }
}

public class McpToolSuccessPath_Tests
{
    [Fact]
    public async Task AuditTrailTools_GetAuditTrailSummary_AdminCaller_QueriesRepository()
    {
        // The Admin path must reach the repository; the denial tests only prove the refusal.
        var repository = new Mock<IAuditTrailRepository>();
        _ = repository.Setup(r => r.GetAuditTrailSummaryAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AuditTrailSummary());
        var authorizer = new Mock<IMcpCallerAuthorizer>();
        _ = authorizer.Setup(a => a.RequireAsync(VideoForensicsPolicies.Admin)).Returns(Task.CompletedTask);
        var tools = new AuditTrailTools(repository.Object, authorizer.Object, NullLogger<AuditTrailTools>.Instance);
        var locationId = Guid.NewGuid();

        _ = await tools.GetAuditTrailSummary(locationId);

        repository.Verify(r => r.GetAuditTrailSummaryAsync(locationId, It.IsAny<CancellationToken>()), Times.Once);
    }
}
