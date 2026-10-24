
using Microsoft.AspNetCore.Http;

using Moq;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Client.WebApp.Contracts;
using VideoForensics.Client.WebApp.Services;
using VideoForensics.Client.WebApp.Services.CircuitGuards;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Client.Hosting;
using VideoForensics.Client.Ui.Shared.Layout;

using Xunit;

namespace VideoForensics.Client.WebApp.Tests;

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
            _ = resolver.Setup(r => r.ResolveClientIp(httpContext)).Returns(ip);
            network.CaptureFromInitialConnection(httpContext, resolver.Object);
        }

        return network;
    }

    [Fact]
    public async Task GuardedReleaseChannelService_Set_Allowed_AuditsCapturedClientIp()
    {
        Fixture f = Create(allowed: true, NetworkWithIp("203.0.113.7"));

        _ = await f.Guarded.SetReleaseChannelAsync(UpdateReleaseChannel.Testing, CancellationToken.None);

        f.Audit.Verify(a => a.LogAsync(
            SecurityAuditEventTypes.ReleaseChannelChanged, ActingOperatorId, null, "203.0.113.7",
            It.IsAny<string?>(), true, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GuardedReleaseChannelService_Set_Allowed_NoCapturedIp_AuditsNullIp()
    {
        Fixture f = Create(allowed: true, NetworkWithIp(null));

        _ = await f.Guarded.SetReleaseChannelAsync(UpdateReleaseChannel.Testing, CancellationToken.None);

        f.Audit.Verify(a => a.LogAsync(
            SecurityAuditEventTypes.ReleaseChannelChanged, ActingOperatorId, null, null,
            It.IsAny<string?>(), true, It.IsAny<CancellationToken>()), Times.Once);
    }

    private static Fixture Create(bool allowed, SessionNetworkContext? network = null)
    {
        var inner = new Mock<IReleaseChannelService>();
        _ = inner.Setup(s => s.GetReleaseChannelAsync(It.IsAny<CancellationToken>())).ReturnsAsync(UpdateReleaseChannel.Stable);
        _ = inner.Setup(s => s.SetReleaseChannelAsync(It.IsAny<UpdateReleaseChannel>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(UpdateReleaseChannel.Testing);
        var gate = new Mock<ICircuitOperatorGate>();
        _ = gate.Setup(g => g.Allows(AccessPolicy.SuperAdminLocal)).Returns(allowed);
        _ = gate.Setup(g => g.Denial(AccessPolicy.SuperAdminLocal)).Returns("denied");
        _ = gate.SetupGet(g => g.OperatorId).Returns(ActingOperatorId);
        var audit = new Mock<ISecurityAuditLogger>();
        return new Fixture(new GuardedReleaseChannelService(inner.Object, gate.Object, audit.Object, network ?? new SessionNetworkContext()), inner, audit);
    }

    [Fact]
    public async Task GuardedReleaseChannelService_Set_Denied_ThrowsAndDoesNotDelegate()
    {
        Fixture f = Create(allowed: false);

        _ = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => f.Guarded.SetReleaseChannelAsync(UpdateReleaseChannel.Testing, CancellationToken.None));

        f.Inner.Verify(s => s.SetReleaseChannelAsync(It.IsAny<UpdateReleaseChannel>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GuardedReleaseChannelService_Set_Denied_WritesNoAuditEntry()
    {
        Fixture f = Create(allowed: false);

        _ = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => f.Guarded.SetReleaseChannelAsync(UpdateReleaseChannel.Testing, CancellationToken.None));

        VerifyNoAudit(f.Audit);
    }

    [Fact]
    public async Task GuardedReleaseChannelService_Set_Allowed_Delegates()
    {
        Fixture f = Create(allowed: true);

        UpdateReleaseChannel result = await f.Guarded.SetReleaseChannelAsync(UpdateReleaseChannel.Testing, CancellationToken.None);

        Assert.Equal(UpdateReleaseChannel.Testing, result);
        f.Inner.Verify(s => s.SetReleaseChannelAsync(UpdateReleaseChannel.Testing, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GuardedReleaseChannelService_Set_Allowed_WritesOneUrgentAuditEntryWithOperatorAndChange()
    {
        Fixture f = Create(allowed: true);
        using var cts = new CancellationTokenSource();

        _ = await f.Guarded.SetReleaseChannelAsync(UpdateReleaseChannel.Testing, cts.Token);

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
        Fixture f = Create(allowed: true);
        _ = f.Inner.Setup(s => s.SetReleaseChannelAsync(It.IsAny<UpdateReleaseChannel>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("save failed"));

        _ = await Assert.ThrowsAsync<InvalidOperationException>(() => f.Guarded.SetReleaseChannelAsync(UpdateReleaseChannel.Testing, CancellationToken.None));

        VerifyNoAudit(f.Audit);
    }

    [Fact]
    public async Task GuardedReleaseChannelService_Get_Denied_StillDelegates()
    {
        Fixture f = Create(allowed: false);

        UpdateReleaseChannel result = await f.Guarded.GetReleaseChannelAsync(CancellationToken.None);

        Assert.Equal(UpdateReleaseChannel.Stable, result);
        f.Inner.Verify(s => s.GetReleaseChannelAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GuardedReleaseChannelService_Get_WritesNoAuditEntry()
    {
        Fixture f = Create(allowed: true);

        _ = await f.Guarded.GetReleaseChannelAsync(CancellationToken.None);

        VerifyNoAudit(f.Audit);
    }

    private static void VerifyNoAudit(Mock<ISecurityAuditLogger> audit)
    {
        audit.Verify(a => a.LogAsync(
            It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<string?>(),
            It.IsAny<string?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
