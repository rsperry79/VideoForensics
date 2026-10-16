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

    public CircuitOperatorGate CreateGate() => new(Scope, Session.Object, Tokens.Object, Network);

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
