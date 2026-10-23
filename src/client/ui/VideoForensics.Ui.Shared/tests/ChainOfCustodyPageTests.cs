
using Bunit;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

using Moq;

using VideoForensics.Core.Providers.Common.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Core.Contracts;
using VideoForensics.Data.Core.Models;
using VideoForensics.Ui.Shared.Pages;
using VideoForensics.Ui.Shared.Services;

using Xunit;

namespace VideoForensics.Ui.Shared.Tests;
/// <summary>
/// ChainOfCustody reads the server database only, so its gate is the paired operator session
/// (PairedSessionState), not whether a provider (Ring) account is linked.
/// </summary>
public abstract class ChainOfCustodyPageTestBase : BunitContext
{
    protected Mock<IProviderAuthService> AuthServiceMock { get; } = new();
    protected Mock<IDeviceRepository> DeviceRepositoryMock { get; } = new();
    protected Mock<IReportGenerationService> ReportGenerationServiceMock { get; } = new();

    protected ChainOfCustodyPageTestBase()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;

        _ = Services.AddLocalization();
        ScopeOwnershipTestDoubles.RegisterEmpty(Services);
        _ = Services.AddSingleton<Syncfusion.Blazor.ISyncfusionStringLocalizer, Syncfusion.Blazor.SyncfusionStringLocalizer>();
        _ = Services.AddSingleton<Syncfusion.Blazor.GlobalOptions>();
        _ = Services.AddScoped<Syncfusion.Blazor.SyncfusionBlazorService>();

        _ = AuthServiceMock
            .Setup(m => m.IsAuthenticatedAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _ = DeviceRepositoryMock
            .Setup(m => m.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        // Empty audit trail keeps the page on its paragraph branch (no Syncfusion grid to render).
        _ = ReportGenerationServiceMock
            .Setup(m => m.BuildChainOfCustodyReportAsync(It.IsAny<Guid?>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChainOfCustodyReport { ChainIntegrityVerified = true });

        _ = Services.AddScoped(_ => AuthServiceMock.Object);
        _ = Services.AddScoped(_ => DeviceRepositoryMock.Object);
        _ = Services.AddScoped(_ => ReportGenerationServiceMock.Object);
        _ = Services.AddScoped<RightPanelContentService>();
        _ = Services.AddScoped(sp => new PairedSessionState(sp.GetRequiredService<IJSRuntime>()));

        SignInOperatorAsync().GetAwaiter().GetResult();
    }

    protected Task SignInOperatorAsync()
    {
        return Services.GetRequiredService<PairedSessionState>().SetAsync("test-token", Guid.NewGuid(), "Review");
    }

    protected Task SignOutOperatorAsync()
    {
        return Services.GetRequiredService<PairedSessionState>().ClearAsync();
    }

    protected IRenderedComponent<ChainOfCustody> RenderPage()
    {
        return Render<ChainOfCustody>();
    }
}

public class ChainOfCustody_OperatorGate_Tests : ChainOfCustodyPageTestBase
{
    [Fact]
    public async Task ChainOfCustody_OperatorSignedOut_ShowsSignInPrompt()
    {
        // Provider account linked, but no operator session: the sign-in prompt must render.
        await SignOutOperatorAsync();
        _ = AuthServiceMock
            .Setup(m => m.IsAuthenticatedAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        IRenderedComponent<ChainOfCustody> component = RenderPage();

        Assert.Contains("You are not signed in.", component.Markup);
        Assert.Empty(component.FindAll(".alert-success"));
    }

    [Fact]
    public async Task ChainOfCustody_OperatorSignedInNoRingAccount_ShowsContent()
    {
        // Operator signed in, no provider account linked: the report reads the server DB only, so
        // the integrity banner must render and the sign-in prompt must not.
        await SignInOperatorAsync();
        _ = AuthServiceMock
            .Setup(m => m.IsAuthenticatedAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        IRenderedComponent<ChainOfCustody> component = RenderPage();

        Assert.DoesNotContain("You are not signed in.", component.Markup);
        Assert.NotEmpty(component.FindAll(".alert-success"));
    }
}
