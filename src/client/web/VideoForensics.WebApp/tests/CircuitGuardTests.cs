namespace VideoForensics.WebApp.Tests;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Moq;
using VideoForensics.Client.Common.Contracts;
using VideoForensics.Core.Api.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Hosting;
using VideoForensics.Ui.Shared.Layout;
using VideoForensics.Ui.Shared.Services;
using VideoForensics.WebApp.Contracts;
using VideoForensics.WebApp.Services;
using VideoForensics.WebApp.Services.CircuitGuards;
using Xunit;

/// <summary>Builds the server-side circuit state the gate reads: an open or closed circuit, a token, and a tier.</summary>
internal sealed class CircuitFixture
{
    public CircuitScope Scope { get; } = new();
    public Mock<ISessionTokenService> Tokens { get; } = new();
    public Mock<PairedSessionState> Session { get; } = new(new Mock<IJSRuntime>().Object);
    public SessionNetworkContext Network { get; } = new();
    public Mock<IStepUpAuthService> StepUp { get; } = new();

    public CircuitOperatorGate CreateGate() => new(Scope, Session.Object, Tokens.Object, Network, StepUp.Object);

    /// <summary>Signs the circuit in as an operator with the given role and kind, and records the tier.</summary>
    public void SignIn(OperatorRole role, CredentialKind kind = CredentialKind.Password, NetworkTier tier = NetworkTier.Local)
    {
        const string token = "circuit-token";
        Session.Setup(s => s.SessionToken).Returns(token);
        var principal = new SessionPrincipal(
            Guid.NewGuid(), null, kind, role, Guid.NewGuid(), DateTime.UtcNow, DateTime.UtcNow.AddHours(1));
        Tokens.Setup(t => t.Validate(token)).Returns(principal);
        Network.SetTier(tier);
    }
}

public class CircuitOperatorGate_Tests
{
    [Fact]
    public void CircuitOperatorGate_ClosedCircuit_AllowsWithoutOperator()
    {
        // Background work runs in its own scope with no UI circuit; it is not an operator action.
        var fixture = new CircuitFixture();
        fixture.Scope.IsOpen = false;

        Assert.True(fixture.CreateGate().Allows(AccessPolicy.SuperAdminLocal));
    }

    [Fact]
    public void CircuitOperatorGate_OpenCircuitNoToken_Denies()
    {
        var fixture = new CircuitFixture();
        fixture.Scope.IsOpen = true;
        fixture.Session.Setup(s => s.SessionToken).Returns((string?)null);

        Assert.False(fixture.CreateGate().Allows(AccessPolicy.ReadOnly));
    }

    [Fact]
    public void CircuitOperatorGate_OpenCircuitInvalidToken_Denies()
    {
        // The browser can send any token; only one the server re-validates may count.
        var fixture = new CircuitFixture();
        fixture.Scope.IsOpen = true;
        fixture.Session.Setup(s => s.SessionToken).Returns("forged");
        fixture.Tokens.Setup(t => t.Validate("forged")).Returns((SessionPrincipal?)null);

        Assert.False(fixture.CreateGate().Allows(AccessPolicy.ReadOnly));
    }

    [Fact]
    public void CircuitOperatorGate_SuperAdminOnLocalTier_AllowsSuperAdminLocal()
    {
        var fixture = new CircuitFixture();
        fixture.Scope.IsOpen = true;
        fixture.SignIn(OperatorRole.SuperAdmin, tier: NetworkTier.Local);

        Assert.True(fixture.CreateGate().Allows(AccessPolicy.SuperAdminLocal));
    }

    [Fact]
    public void CircuitOperatorGate_SuperAdminOnRemoteTier_DeniesSuperAdminLocal()
    {
        var fixture = new CircuitFixture();
        fixture.Scope.IsOpen = true;
        fixture.SignIn(OperatorRole.SuperAdmin, tier: NetworkTier.Internet);

        Assert.False(fixture.CreateGate().Allows(AccessPolicy.SuperAdminLocal));
    }

    [Fact]
    public void CircuitOperatorGate_PasskeyOnRemoteTier_DeniesSuperAdminLocal()
    {
        // A passkey satisfies SuperAdminLocalOrPasskey, not SuperAdminLocal.
        var fixture = new CircuitFixture();
        fixture.Scope.IsOpen = true;
        fixture.SignIn(OperatorRole.SuperAdmin, CredentialKind.OperatorPasskey, NetworkTier.Internet);

        Assert.False(fixture.CreateGate().Allows(AccessPolicy.SuperAdminLocal));
        Assert.True(fixture.CreateGate().Allows(AccessPolicy.SuperAdminLocalOrPasskey));
    }

    [Fact]
    public void CircuitOperatorGate_AdminOperator_DeniesSuperAdminLocal()
    {
        var fixture = new CircuitFixture();
        fixture.Scope.IsOpen = true;
        fixture.SignIn(OperatorRole.Admin, tier: NetworkTier.Local);

        Assert.False(fixture.CreateGate().Allows(AccessPolicy.SuperAdminLocal));
    }
}

public class GuardedProviderAccountRepository_Tests
{
    private static (GuardedProviderAccountRepository Guarded, Mock<IProviderAccountRepository> Inner) Create(bool allowed)
    {
        var inner = new Mock<IProviderAccountRepository>();
        var gate = new Mock<ICircuitOperatorGate>();
        gate.Setup(g => g.Allows(It.IsAny<AccessPolicy>())).Returns(allowed);
        return (new GuardedProviderAccountRepository(inner.Object, gate.Object), inner);
    }

    [Fact]
    public async Task GuardedProviderAccountRepository_AddAsync_Denied_ThrowsAndDoesNotCallInner()
    {
        var (guarded, inner) = Create(allowed: false);
        var account = new ProviderAccount { ProviderName = "Ring" };

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => guarded.AddAsync(account, CancellationToken.None));

        inner.Verify(r => r.AddAsync(It.IsAny<ProviderAccount>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GuardedProviderAccountRepository_DeleteAsync_Allowed_CallsInner()
    {
        var (guarded, inner) = Create(allowed: true);
        var id = Guid.NewGuid();

        await guarded.DeleteAsync(id, CancellationToken.None);

        inner.Verify(r => r.DeleteAsync(id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GuardedProviderAccountRepository_ListAsync_Denied_StillPassesThrough()
    {
        // Collect and Device Configuration read accounts for every operator, so reads are never gated here.
        var (guarded, inner) = Create(allowed: false);
        inner.Setup(r => r.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<ProviderAccount>());

        _ = await guarded.ListAsync(CancellationToken.None);

        inner.Verify(r => r.ListAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}

public class GuardedDeviceConfigRepository_Tests
{
    [Fact]
    public async Task GuardedDeviceConfigRepository_AppendSnapshot_Denied_ThrowsAndDoesNotWrite()
    {
        var inner = new Mock<IDeviceConfigRepository>();
        var gate = new Mock<ICircuitOperatorGate>();
        gate.Setup(g => g.Allows(AccessPolicy.SuperAdminLocal)).Returns(false);
        var guarded = new GuardedDeviceConfigRepository(inner.Object, gate.Object);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => guarded.AppendSnapshotAsync(new DeviceConfigSnapshot(), CancellationToken.None));

        inner.Verify(r => r.AppendSnapshotAsync(It.IsAny<DeviceConfigSnapshot>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}

public class GuardedEvidenceExportService_Tests
{
    [Fact]
    public async Task GuardedEvidenceExportService_Export_Denied_ThrowsAndDoesNotExport()
    {
        var inner = new Mock<IEvidenceExportService>();
        var gate = new Mock<ICircuitOperatorGate>();
        gate.Setup(g => g.Allows(AccessPolicy.SuperAdminLocal)).Returns(false);
        var guarded = new GuardedEvidenceExportService(inner.Object, gate.Object);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => guarded.ExportEvidenceAsync(
            new List<Guid>(), "out", null, null, null, CancellationToken.None));

        inner.Verify(r => r.ExportEvidenceAsync(
            It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}

public class GuardedRingSelfTestService_Tests
{
    private static (GuardedRingSelfTestService Guarded, Mock<IRingSelfTestService> Inner) Create(bool allowed)
    {
        var inner = new Mock<IRingSelfTestService>();
        inner.Setup(s => s.StartRunAsync(It.IsAny<SelfTestRunRequestDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SelfTestRunResponseDto)null!);
        var gate = new Mock<ICircuitOperatorGate>();
        gate.Setup(g => g.Allows(AccessPolicy.SuperAdminLocal)).Returns(allowed);
        return (new GuardedRingSelfTestService(inner.Object, gate.Object), inner);
    }

    [Fact]
    public async Task GuardedRingSelfTestService_DestructiveRun_Denied_ThrowsAndDoesNotStart()
    {
        var (guarded, inner) = Create(allowed: false);
        var request = new SelfTestRunRequestDto(new List<string> { "unlock" }, Destructive: true);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => guarded.StartRunAsync(request, CancellationToken.None));

        inner.Verify(s => s.StartRunAsync(It.IsAny<SelfTestRunRequestDto>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GuardedRingSelfTestService_ReadOnlyRun_Denied_StillStarts()
    {
        var (guarded, inner) = Create(allowed: false);
        var request = new SelfTestRunRequestDto(new List<string> { "list" }, Destructive: false);

        _ = await guarded.StartRunAsync(request, CancellationToken.None);

        inner.Verify(s => s.StartRunAsync(request, It.IsAny<CancellationToken>()), Times.Once);
    }
}

public class CircuitGuardRegistration_Tests
{
    [Fact]
    public async Task CircuitGuardRegistration_RegisteredAccountRepository_IsWrappedByGuard()
    {
        var services = new ServiceCollection();
        services.AddScoped<IProviderAccountRepository, ProviderAccountRepositoryStub>();
        // The other guarded services must be registered too, or the helper fails fast (see the test below).
        services.AddScoped(_ => new Mock<IDeviceConfigRepository>().Object);
        services.AddScoped(_ => new Mock<IEvidenceExportService>().Object);
        services.AddScoped(_ => new Mock<IRingSelfTestService>().Object);
        services.AddScoped(_ => new Mock<ICaseRepository>().Object);
        services.AddScoped(_ => new Mock<ILegalHoldRepository>().Object);
        services.AddScoped(_ => new Mock<IMediaStillCaptureService>().Object);
        services.AddScoped(_ => new Mock<IJammingRepository>().Object);
        services.AddScoped(_ => new Mock<VideoForensics.Client.Core.Contracts.IConfigToolsOrchestrator>().Object);
        services.AddCircuitOperatorGuards();
        // The gate is overridden so the test controls the decision without a circuit.
        var gate = new Mock<ICircuitOperatorGate>();
        gate.Setup(g => g.Allows(AccessPolicy.SuperAdminLocal)).Returns(false);
        services.AddScoped(_ => gate.Object);

        await using ServiceProvider provider = services.BuildServiceProvider();
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        IProviderAccountRepository resolved = scope.ServiceProvider.GetRequiredService<IProviderAccountRepository>();

        Assert.IsType<GuardedProviderAccountRepository>(resolved);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => resolved.DeleteAsync(Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public void CircuitGuardRegistration_UnregisteredService_Throws()
    {
        var services = new ServiceCollection();

        Assert.Throws<InvalidOperationException>(() => services.AddCircuitOperatorGuards());
    }

    /// <summary>Stand-in for the real data-layer repository, so the registration test needs no database.</summary>
    public sealed class ProviderAccountRepositoryStub : IProviderAccountRepository
    {
        public Task<ProviderAccount?> GetAsync(Guid accountId, CancellationToken ct) => Task.FromResult<ProviderAccount?>(null);
        public Task<IReadOnlyList<ProviderAccount>> GetByUserIdAsync(Guid userId, CancellationToken ct) => Task.FromResult<IReadOnlyList<ProviderAccount>>([]);
        public Task<ProviderAccount?> GetByUserAndProviderAsync(Guid userId, string providerName, CancellationToken ct) => Task.FromResult<ProviderAccount?>(null);
        public Task<IReadOnlyList<ProviderAccount>> ListAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<ProviderAccount>>([]);
        public Task<IReadOnlyList<ProviderAccount>> ListActiveAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<ProviderAccount>>([]);
        public Task AddAsync(ProviderAccount account, CancellationToken ct) => Task.CompletedTask;
        public Task UpdateAsync(ProviderAccount account, CancellationToken ct) => Task.CompletedTask;
        public Task DeleteAsync(Guid accountId, CancellationToken ct) => Task.CompletedTask;
        public Task RecordErrorAsync(Guid providerAccountId, string errorMessage, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task RecordSuccessAsync(Guid providerAccountId, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}

public class GuardedCaseRepository_Tests
{
    [Fact]
    public async Task GuardedCaseRepository_CloseAsync_ReviewOnly_ThrowsAndDoesNotClose()
    {
        // Review is enough for case edits but not for close and reopen, which the server requires Admin for.
        var inner = new Mock<ICaseRepository>();
        var gate = new Mock<ICircuitOperatorGate>();
        gate.Setup(g => g.Allows(AccessPolicy.Review)).Returns(true);
        gate.Setup(g => g.Allows(AccessPolicy.Admin)).Returns(false);
        var guarded = new GuardedCaseRepository(inner.Object, gate.Object);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => guarded.CloseAsync(Guid.NewGuid(), "op", CancellationToken.None));

        inner.Verify(r => r.CloseAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GuardedCaseRepository_RemoveItemAsync_ReadOnlyOperator_ThrowsAndDoesNotRemove()
    {
        var inner = new Mock<ICaseRepository>();
        var gate = new Mock<ICircuitOperatorGate>();
        gate.Setup(g => g.Allows(AccessPolicy.Review)).Returns(false);
        var guarded = new GuardedCaseRepository(inner.Object, gate.Object);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => guarded.RemoveItemAsync(Guid.NewGuid(), "op", "why", CancellationToken.None));

        inner.Verify(r => r.RemoveItemAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}

public class GuardedLegalHoldRepository_Tests
{
    [Fact]
    public async Task GuardedLegalHoldRepository_ReleaseAsync_ReviewOperator_ThrowsAndDoesNotRelease()
    {
        var inner = new Mock<ILegalHoldRepository>();
        var gate = new Mock<ICircuitOperatorGate>();
        gate.Setup(g => g.Allows(AccessPolicy.Admin)).Returns(false);
        var guarded = new GuardedLegalHoldRepository(inner.Object, gate.Object);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => guarded.ReleaseAsync(Guid.NewGuid(), "op", "why", CancellationToken.None));

        inner.Verify(r => r.ReleaseAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}

public class GuardedMediaStillCaptureService_Tests
{
    [Fact]
    public async Task GuardedMediaStillCaptureService_Capture_ReadOnlyOperator_ThrowsAndDoesNotCapture()
    {
        var inner = new Mock<IMediaStillCaptureService>();
        var gate = new Mock<ICircuitOperatorGate>();
        gate.Setup(g => g.Allows(AccessPolicy.Review)).Returns(false);
        var guarded = new GuardedMediaStillCaptureService(inner.Object, gate.Object);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => guarded.CaptureStillAsync(
            Guid.NewGuid(), 0, null, null, null, "op", CancellationToken.None));

        inner.Verify(r => r.CaptureStillAsync(
            It.IsAny<Guid>(), It.IsAny<long>(), It.IsAny<string?>(), It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}

public class GuardedJammingRepository_Tests
{
    private static (GuardedJammingRepository Guarded, Mock<IJammingRepository> Inner) Create(bool allowed)
    {
        var inner = new Mock<IJammingRepository>();
        var gate = new Mock<ICircuitOperatorGate>();
        gate.Setup(g => g.Allows(AccessPolicy.Admin)).Returns(allowed);
        return (new GuardedJammingRepository(inner.Object, gate.Object), inner);
    }

    [Fact]
    public async Task GuardedJammingRepository_UpsertIncident_ReviewOperator_ThrowsAndDoesNotWrite()
    {
        var (guarded, inner) = Create(allowed: false);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => guarded.UpsertIncidentAsync(new JammingIncidentRecord(), CancellationToken.None));

        inner.Verify(r => r.UpsertIncidentAsync(It.IsAny<JammingIncidentRecord>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GuardedJammingRepository_RecomputeStats_ReviewOperator_ThrowsAndDoesNotRecompute()
    {
        var (guarded, inner) = Create(allowed: false);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => guarded.RecomputeStatsAsync(Guid.NewGuid(), CancellationToken.None));

        inner.Verify(r => r.RecomputeStatsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GuardedJammingRepository_ListIncidents_ReviewOperator_StillPassesThrough()
    {
        var (guarded, inner) = Create(allowed: false);
        inner.Setup(r => r.ListIncidentsAsync(null, null, null, It.IsAny<CancellationToken>())).ReturnsAsync(new List<JammingIncidentRecord>());

        _ = await guarded.ListIncidentsAsync(null, null, null, CancellationToken.None);

        inner.Verify(r => r.ListIncidentsAsync(null, null, null, It.IsAny<CancellationToken>()), Times.Once);
    }
}

public class GuardedConfigToolsOrchestrator_Tests
{
    [Fact]
    public async Task GuardedConfigToolsOrchestrator_HealthSync_ReadOnlyOperator_ReturnsFailureAndDoesNotChange()
    {
        var inner = new Mock<VideoForensics.Client.Core.Contracts.IConfigToolsOrchestrator>();
        var gate = new Mock<ICircuitOperatorGate>();
        gate.Setup(g => g.Allows(AccessPolicy.Admin)).Returns(false);
        var guarded = new VideoForensics.WebApp.Services.CircuitGuards.GuardedConfigToolsOrchestrator(inner.Object, gate.Object);

        var (success, _) = await guarded.SetHealthSyncAsync(new Mock<IForensicsConfiguration>().Object, false);

        Assert.False(success);
        inner.Verify(c => c.SetHealthSyncAsync(It.IsAny<IForensicsConfiguration>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GuardedConfigToolsOrchestrator_FactoryReset_LocalAdminOnly_ReturnsFailureAndDoesNotReset()
    {
        // Factory reset is destructive and has no HTTP endpoint, so the server check is the only one. A denied call
        // returns a failed result rather than throwing, so the page can show why.
        var inner = new Mock<VideoForensics.Client.Core.Contracts.IConfigToolsOrchestrator>();
        var gate = new Mock<ICircuitOperatorGate>();
        gate.Setup(g => g.Allows(AccessPolicy.SuperAdminLocal)).Returns(false);
        var guarded = new VideoForensics.WebApp.Services.CircuitGuards.GuardedConfigToolsOrchestrator(inner.Object, gate.Object);

        var (success, message) = await guarded.FactoryResetAsync();

        Assert.False(success);
        Assert.Contains("SuperAdminLocal", message);
        inner.Verify(c => c.FactoryResetAsync(It.IsAny<CancellationToken>(), It.IsAny<string?>(), It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task GuardedConfigToolsOrchestrator_Retention_AdminAllowed_CallsInner()
    {
        var inner = new Mock<VideoForensics.Client.Core.Contracts.IConfigToolsOrchestrator>();
        inner.Setup(c => c.SetRetentionDaysAsync(It.IsAny<IForensicsConfiguration>(), 30, It.IsAny<CancellationToken>()))
            .ReturnsAsync((true, "ok"));
        var gate = new Mock<ICircuitOperatorGate>();
        gate.Setup(g => g.Allows(AccessPolicy.Admin)).Returns(true);
        var guarded = new VideoForensics.WebApp.Services.CircuitGuards.GuardedConfigToolsOrchestrator(inner.Object, gate.Object);

        var (success, _) = await guarded.SetRetentionDaysAsync(new Mock<IForensicsConfiguration>().Object, 30);

        Assert.True(success);
        inner.Verify(c => c.SetRetentionDaysAsync(It.IsAny<IForensicsConfiguration>(), 30, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GuardedConfigToolsOrchestrator_Retention_ReadOnlyOperator_ReturnsFailureAndDoesNotChange()
    {
        var inner = new Mock<VideoForensics.Client.Core.Contracts.IConfigToolsOrchestrator>();
        var gate = new Mock<ICircuitOperatorGate>();
        gate.Setup(g => g.Allows(AccessPolicy.Admin)).Returns(false);
        var guarded = new VideoForensics.WebApp.Services.CircuitGuards.GuardedConfigToolsOrchestrator(inner.Object, gate.Object);

        var (success, _) = await guarded.SetRetentionDaysAsync(new Mock<IForensicsConfiguration>().Object, 30);

        Assert.False(success);
        inner.Verify(c => c.SetRetentionDaysAsync(It.IsAny<IForensicsConfiguration>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}

public class CircuitOperatorGateStepUp_Tests
{
    [Fact]
    public void CircuitOperatorGate_StepUpClosedCircuit_PassesWithoutToken()
    {
        // Background work has no operator to re-authenticate.
        var fixture = new CircuitFixture();
        fixture.Scope.IsOpen = false;

        Assert.True(fixture.CreateGate().IsStepUpValid(null));
    }

    [Fact]
    public void CircuitOperatorGate_StepUpNoToken_Denies()
    {
        var fixture = new CircuitFixture();
        fixture.Scope.IsOpen = true;
        fixture.SignIn(OperatorRole.SuperAdmin);

        Assert.False(fixture.CreateGate().IsStepUpValid(null));
    }

    [Fact]
    public void CircuitOperatorGate_StepUpPasskeySession_ValidatesAgainstCredential()
    {
        // The endpoint filter binds a step-up to the paired-device claim, which for a passkey session is its credential.
        var fixture = new CircuitFixture();
        fixture.Scope.IsOpen = true;
        Guid credentialId = Guid.NewGuid();
        fixture.Session.Setup(s => s.SessionToken).Returns("circuit-token");
        fixture.Tokens.Setup(t => t.Validate("circuit-token")).Returns(new SessionPrincipal(
            Guid.NewGuid(), credentialId, CredentialKind.OperatorPasskey, OperatorRole.SuperAdmin, Guid.NewGuid(), DateTime.UtcNow, DateTime.UtcNow.AddHours(1)));
        fixture.StepUp.Setup(v => v.Validate("step", credentialId)).Returns(true);

        Assert.True(fixture.CreateGate().IsStepUpValid("step"));
    }

    [Fact]
    public void CircuitOperatorGate_StepUpPasswordSession_ValidatesAgainstOperator()
    {
        var fixture = new CircuitFixture();
        fixture.Scope.IsOpen = true;
        Guid operatorId = Guid.NewGuid();
        fixture.Session.Setup(s => s.SessionToken).Returns("circuit-token");
        fixture.Tokens.Setup(t => t.Validate("circuit-token")).Returns(new SessionPrincipal(
            operatorId, null, CredentialKind.Password, OperatorRole.SuperAdmin, Guid.NewGuid(), DateTime.UtcNow, DateTime.UtcNow.AddHours(1)));
        fixture.StepUp.Setup(v => v.Validate("step", operatorId)).Returns(true);

        Assert.True(fixture.CreateGate().IsStepUpValid("step"));
    }

    [Fact]
    public void CircuitOperatorGate_StepUpExpiredOrForged_Denies()
    {
        var fixture = new CircuitFixture();
        fixture.Scope.IsOpen = true;
        fixture.SignIn(OperatorRole.SuperAdmin);
        fixture.StepUp.Setup(v => v.Validate("expired", It.IsAny<Guid>())).Returns(false);

        Assert.False(fixture.CreateGate().IsStepUpValid("expired"));
    }
}

public class GuardedEvidenceExportStepUp_Tests
{
    private static (GuardedEvidenceExportService Guarded, Mock<IEvidenceExportService> Inner) Create(bool localAdmin, bool stepUpValid)
    {
        var inner = new Mock<IEvidenceExportService>();
        inner.Setup(e => e.ExportEvidenceAsync(
                It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<CancellationToken>(), It.IsAny<string?>()))
            .ReturnsAsync(new ExportResult { Success = true });
        var gate = new Mock<ICircuitOperatorGate>();
        gate.Setup(g => g.Allows(AccessPolicy.SuperAdminLocal)).Returns(localAdmin);
        gate.Setup(g => g.IsStepUpValid(It.IsAny<string?>())).Returns(stepUpValid);
        return (new GuardedEvidenceExportService(inner.Object, gate.Object), inner);
    }

    [Fact]
    public async Task GuardedEvidenceExportService_NoStepUp_ThrowsAndDoesNotExport()
    {
        var (guarded, inner) = Create(localAdmin: true, stepUpValid: false);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => guarded.ExportEvidenceAsync(
            new List<Guid>(), "out", null, null, null, CancellationToken.None, null));

        inner.Verify(e => e.ExportEvidenceAsync(
            It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(),
            It.IsAny<CancellationToken>(), It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task GuardedEvidenceExportService_ValidStepUpAndLocalAdmin_Exports()
    {
        var (guarded, inner) = Create(localAdmin: true, stepUpValid: true);

        var result = await guarded.ExportEvidenceAsync(
            new List<Guid>(), "out", null, null, null, CancellationToken.None, "step");

        Assert.True(result.Success);
        inner.Verify(e => e.ExportEvidenceAsync(
            It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(),
            It.IsAny<CancellationToken>(), "step"), Times.Once);
    }
}
