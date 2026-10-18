namespace VideoForensics.WebApp.Tests;

using Microsoft.AspNetCore.Http;
using Moq;
using VideoForensics.Client.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Hosting;
using VideoForensics.WebApp.Services;
using VideoForensics.Ui.Shared.Layout;
using VideoForensics.WebApp.Contracts;
using VideoForensics.WebApp.Services.CircuitGuards;
using Xunit;

public class GuardedReleaseChannelServiceTests
{
    private static readonly Guid ActingOperatorId = Guid.NewGuid();

    private sealed record Fixture(
        GuardedReleaseChannelService Guarded,
        Mock<IReleaseChannelService> Inner,
        Mock<ISecurityAuditLogger> Audit);

    private static SessionNetworkContext NetworkWithIp(string? ip)
    {
        var network = new SessionNetworkContext();
        if (ip is not null)
        {
            var httpContext = new DefaultHttpContext();
            var resolver = new Mock<INetworkTierResolver>();
            resolver.Setup(r => r.ResolveClientIp(httpContext)).Returns(ip);
            network.CaptureFromInitialConnection(httpContext, resolver.Object);
        }

        return network;
    }

    [Fact]
    public async Task GuardedReleaseChannelService_Set_Allowed_AuditsCapturedClientIp()
    {
        var f = Create(allowed: true, NetworkWithIp("203.0.113.7"));

        await f.Guarded.SetReleaseChannelAsync(UpdateReleaseChannel.Testing, CancellationToken.None);

        f.Audit.Verify(a => a.LogAsync(
            SecurityAuditEventTypes.ReleaseChannelChanged, ActingOperatorId, null, "203.0.113.7",
            It.IsAny<string?>(), true, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GuardedReleaseChannelService_Set_Allowed_NoCapturedIp_AuditsNullIp()
    {
        var f = Create(allowed: true, NetworkWithIp(null));

        await f.Guarded.SetReleaseChannelAsync(UpdateReleaseChannel.Testing, CancellationToken.None);

        f.Audit.Verify(a => a.LogAsync(
            SecurityAuditEventTypes.ReleaseChannelChanged, ActingOperatorId, null, null,
            It.IsAny<string?>(), true, It.IsAny<CancellationToken>()), Times.Once);
    }

    private static Fixture Create(bool allowed, SessionNetworkContext? network = null)
    {
        var inner = new Mock<IReleaseChannelService>();
        inner.Setup(s => s.GetReleaseChannelAsync(It.IsAny<CancellationToken>())).ReturnsAsync(UpdateReleaseChannel.Stable);
        inner.Setup(s => s.SetReleaseChannelAsync(It.IsAny<UpdateReleaseChannel>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(UpdateReleaseChannel.Testing);
        var gate = new Mock<ICircuitOperatorGate>();
        gate.Setup(g => g.Allows(AccessPolicy.SuperAdminLocal)).Returns(allowed);
        gate.Setup(g => g.Denial(AccessPolicy.SuperAdminLocal)).Returns("denied");
        gate.SetupGet(g => g.OperatorId).Returns(ActingOperatorId);
        var audit = new Mock<ISecurityAuditLogger>();
        return new Fixture(new GuardedReleaseChannelService(inner.Object, gate.Object, audit.Object, network ?? new SessionNetworkContext()), inner, audit);
    }

    [Fact]
    public async Task GuardedReleaseChannelService_Set_Denied_ThrowsAndDoesNotDelegate()
    {
        var f = Create(allowed: false);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => f.Guarded.SetReleaseChannelAsync(UpdateReleaseChannel.Testing, CancellationToken.None));

        f.Inner.Verify(s => s.SetReleaseChannelAsync(It.IsAny<UpdateReleaseChannel>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GuardedReleaseChannelService_Set_Denied_WritesNoAuditEntry()
    {
        var f = Create(allowed: false);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => f.Guarded.SetReleaseChannelAsync(UpdateReleaseChannel.Testing, CancellationToken.None));

        VerifyNoAudit(f.Audit);
    }

    [Fact]
    public async Task GuardedReleaseChannelService_Set_Allowed_Delegates()
    {
        var f = Create(allowed: true);

        UpdateReleaseChannel result = await f.Guarded.SetReleaseChannelAsync(UpdateReleaseChannel.Testing, CancellationToken.None);

        Assert.Equal(UpdateReleaseChannel.Testing, result);
        f.Inner.Verify(s => s.SetReleaseChannelAsync(UpdateReleaseChannel.Testing, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GuardedReleaseChannelService_Set_Allowed_WritesOneUrgentAuditEntryWithOperatorAndChange()
    {
        var f = Create(allowed: true);
        using var cts = new CancellationTokenSource();

        await f.Guarded.SetReleaseChannelAsync(UpdateReleaseChannel.Testing, cts.Token);

        f.Audit.Verify(a => a.LogAsync(
            SecurityAuditEventTypes.ReleaseChannelChanged,
            ActingOperatorId,
            null,
            null,
            "Release channel changed from 'Stable' to 'Testing'",
            true,
            cts.Token), Times.Once);
        f.Audit.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GuardedReleaseChannelService_Set_InnerThrows_WritesNoAuditEntry()
    {
        var f = Create(allowed: true);
        f.Inner.Setup(s => s.SetReleaseChannelAsync(It.IsAny<UpdateReleaseChannel>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("save failed"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Guarded.SetReleaseChannelAsync(UpdateReleaseChannel.Testing, CancellationToken.None));

        VerifyNoAudit(f.Audit);
    }

    [Fact]
    public async Task GuardedReleaseChannelService_Get_Denied_StillDelegates()
    {
        var f = Create(allowed: false);

        UpdateReleaseChannel result = await f.Guarded.GetReleaseChannelAsync(CancellationToken.None);

        Assert.Equal(UpdateReleaseChannel.Stable, result);
        f.Inner.Verify(s => s.GetReleaseChannelAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GuardedReleaseChannelService_Get_WritesNoAuditEntry()
    {
        var f = Create(allowed: true);

        await f.Guarded.GetReleaseChannelAsync(CancellationToken.None);

        VerifyNoAudit(f.Audit);
    }

    private static void VerifyNoAudit(Mock<ISecurityAuditLogger> audit) =>
        audit.Verify(a => a.LogAsync(
            It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<string?>(),
            It.IsAny<string?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
}
