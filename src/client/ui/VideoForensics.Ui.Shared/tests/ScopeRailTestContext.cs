
using Bunit;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

using Moq;

using VideoForensics.Utils.Testing;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Client.Ui.Shared.Services;
using VideoForensics.Client.Ui.Shared.Services.Cases;
using VideoForensics.Client.Ui.Shared.Services.Scope;

namespace VideoForensics.Client.Ui.Shared.Tests;
/// <summary>
/// Shared bUnit setup for ScopeRail tests. The constructor registers the defaults every rail test needs;
/// subclasses inherit them and may override any of them, since later registrations win resolution.
/// </summary>
public abstract class ScopeRailTestContext : BunitContext
{
    protected ScopeRailTestContext()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        _ = Services.AddLocalization();
        ScopeOwnershipTestDoubles.RegisterEmpty(Services);
        _ = Services.AddScoped<RightPanelContentService>();

        // Default TimeProvider for tests that don't need a fixed clock; RegisterFakeTime overrides
        // this (later registrations win resolution) for tests asserting exact quick-range dates.
        _ = Services.AddSingleton(TimeProvider.System);
        _ = Services.AddScoped(_ => new ScopeState());

        // Defaults: no open cases, no signed-in operator. Tests that care about case behavior
        // override these with RegisterCaseRepository / RegisterRoleAsync (later registrations win).
        _ = RegisterCaseRepository();
        _ = Services.AddScoped(sp => new CaseState(sp.GetRequiredService<ICaseRepository>(), sp.GetRequiredService<ScopeState>()));
        _ = Services.AddScoped(sp => new PairedSessionState(sp.GetRequiredService<IJSRuntime>()));
    }

    protected static Device MakeDevice(string name)
    {
        return new()
        {
            Id = Guid.NewGuid(),
            LocationId = Guid.NewGuid(),
            ProviderDeviceId = Guid.NewGuid().ToString(),
            Name = name,
            Type = "camera",
        };
    }

    protected static ForensicCase MakeCase(
        string caseNumber = "CASE-001",
        string title = "Test Case",
        CaseStatus status = CaseStatus.Open,
        DateTime? scopeFromUtc = null,
        DateTime? scopeToUtc = null)
    {
        return new()
        {
            Id = Guid.NewGuid(),
            CaseNumber = caseNumber,
            Title = title,
            Status = status,
            ScopeFromUtc = scopeFromUtc,
            ScopeToUtc = scopeToUtc,
            CreatedBy = "test",
            CreatedAtUtc = DateTime.UtcNow
        };
    }

    protected Mock<IDeviceRepository> RegisterDeviceRepository(params Device[] devices)
    {
        var deviceRepository = new Mock<IDeviceRepository>();
        _ = deviceRepository
            .Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(devices.ToList());
        _ = Services.AddScoped(_ => deviceRepository.Object);
        return deviceRepository;
    }

    // private protected: FakeTimeProvider is internal to this test assembly, so the helper cannot be protected.
    private protected FakeTimeProvider RegisterFakeTime(DateTime fixedUtcNow)
    {
        var timeProvider = new FakeTimeProvider(fixedUtcNow);
        _ = Services.AddSingleton<TimeProvider>(timeProvider);
        _ = Services.AddScoped(_ => new ScopeState(timeProvider));
        return timeProvider;
    }

    protected Mock<ICaseRepository> RegisterCaseRepository(params ForensicCase[] openCases)
    {
        var caseRepository = new Mock<ICaseRepository>();
        _ = caseRepository
            .Setup(r => r.ListAsync(CaseStatus.Open, It.IsAny<CancellationToken>()))
            .ReturnsAsync(openCases.ToList());
        _ = Services.AddScoped(_ => caseRepository.Object);
        return caseRepository;
    }

    protected async Task RegisterRoleAsync(OperatorRole role)
    {
        var session = new PairedSessionState(JSInterop.JSRuntime);
        await session.SetAsync("test-token", Guid.NewGuid(), role.ToString());
        _ = Services.AddScoped(_ => session);
    }
}
