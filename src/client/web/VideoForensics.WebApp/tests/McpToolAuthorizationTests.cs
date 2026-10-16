namespace VideoForensics.WebApp.Tests;

using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.WebApp.Auth;
using VideoForensics.WebApp.Contracts;
using VideoForensics.WebApp.Mcp.Tools;
using Xunit;

/// <summary>
/// The MCP tools run with baseline paired-device auth, so each tool that needs more must stop the call itself. The
/// repositories and orchestrator are null on purpose: a denied caller has to stop before any of them is used, so a
/// missing check would show up as a NullReferenceException instead of the expected result.
/// </summary>
public class McpToolAuthorization_Tests
{
    private static Mock<IMcpCallerAuthorizer> Authorizer(bool allowed, string policy)
    {
        var authorizer = new Mock<IMcpCallerAuthorizer>();
        authorizer.Setup(a => a.IsAllowedAsync(policy)).ReturnsAsync(allowed);
        authorizer.Setup(a => a.RequireAsync(policy)).Returns(allowed
            ? Task.CompletedTask
            : Task.FromException(new UnauthorizedAccessException($"requires {policy}")));
        return authorizer;
    }

    [Fact]
    public async Task JammingTools_RecordJammingIncident_NonAdminCaller_ThrowsUnauthorized()
    {
        var tools = new JammingTools(null!, Authorizer(false, VideoForensicsPolicies.Admin).Object, NullLogger<JammingTools>.Instance);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => tools.RecordJammingIncident(
            Guid.NewGuid(), DateTime.UtcNow.AddHours(-1), DateTime.UtcNow, 3, 6.5, JammingConfidenceLevel.High));
    }

    [Fact]
    public async Task JammingTools_RunJammingDetection_NonAdminCaller_ThrowsUnauthorized()
    {
        var tools = new JammingTools(null!, Authorizer(false, VideoForensicsPolicies.Admin).Object, NullLogger<JammingTools>.Instance);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => tools.RunJammingDetection(
            Guid.NewGuid(), DateTime.UtcNow.AddHours(-1), DateTime.UtcNow));
    }

    [Fact]
    public async Task AuditTrailTools_GetAuditTrailSummary_NonAdminCaller_ThrowsAndDoesNotQuery()
    {
        var repository = new Mock<IAuditTrailRepository>();
        var tools = new AuditTrailTools(repository.Object, Authorizer(false, VideoForensicsPolicies.Admin).Object, NullLogger<AuditTrailTools>.Instance);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => tools.GetAuditTrailSummary(Guid.NewGuid()));

        repository.Verify(r => r.GetAuditTrailSummaryAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AuditTrailTools_FlagUnauthorizedAccess_NonAdminCaller_ThrowsAndDoesNotQuery()
    {
        var repository = new Mock<IAuditTrailRepository>();
        var tools = new AuditTrailTools(repository.Object, Authorizer(false, VideoForensicsPolicies.Admin).Object, NullLogger<AuditTrailTools>.Instance);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => tools.FlagUnauthorizedAccess(Guid.NewGuid()));

        repository.Verify(r => r.FlagUnauthorizedAccessAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SecurityEventTools_CrossAccountQuery_RemoteSuperAdmin_ReturnsErrorWithoutQuerying()
    {
        // The caller is authenticated and is a SuperAdmin, but not on the Local tier, so SuperAdminLocal fails.
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(VideoForensicsClaimTypes.OperatorId, Guid.NewGuid().ToString()),
                new Claim(VideoForensicsClaimTypes.Role, OperatorRole.SuperAdmin.ToString()),
            ], "test")),
        };
        var accessor = new Mock<IHttpContextAccessor>();
        accessor.Setup(a => a.HttpContext).Returns(httpContext);
        var audit = new Mock<ISecurityAuditService>();
        var tools = new SecurityEventTools(audit.Object, accessor.Object, Authorizer(false, VideoForensicsPolicies.SuperAdminLocal).Object, NullLogger<SecurityEventTools>.Instance);

        var result = await tools.GetSecurityEvents(operatorId: Guid.NewGuid().ToString());

        Assert.Contains("SuperAdminLocal", result.Error);
        Assert.Empty(result.Events);
        audit.VerifyNoOtherCalls();
    }
}
