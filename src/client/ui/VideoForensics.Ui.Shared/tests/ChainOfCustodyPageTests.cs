namespace VideoForensics.Ui.Shared.Tests;

using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Moq;
using Xunit;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Core.Contracts;
using VideoForensics.Data.Core.Models;
using VideoForensics.Core.Providers.Common.Contracts;
using VideoForensics.Ui.Shared.Pages;
using VideoForensics.Ui.Shared.Services;
using VideoForensics.Ui.Shared.Services.Scope;

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

        Services.AddLocalization();
        Services.AddSingleton<Syncfusion.Blazor.ISyncfusionStringLocalizer, Syncfusion.Blazor.SyncfusionStringLocalizer>();
        Services.AddSingleton<Syncfusion.Blazor.GlobalOptions>();
        Services.AddScoped<Syncfusion.Blazor.SyncfusionBlazorService>();

        AuthServiceMock
            .Setup(m => m.IsAuthenticatedAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        DeviceRepositoryMock
            .Setup(m => m.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<VideoForensics.Data.Common.Entities.Device>());

        // Empty audit trail keeps the page on its paragraph branch (no Syncfusion grid to render).
        ReportGenerationServiceMock
            .Setup(m => m.BuildChainOfCustodyReportAsync(It.IsAny<Guid?>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChainOfCustodyReport { ChainIntegrityVerified = true });

        Services.AddScoped(_ => AuthServiceMock.Object);
        Services.AddScoped(_ => DeviceRepositoryMock.Object);
        Services.AddScoped(_ => ReportGenerationServiceMock.Object);
        Services.AddScoped<RightPanelContentService>();
        Services.AddScoped(sp => new PairedSessionState(sp.GetRequiredService<IJSRuntime>()));

        SignInOperatorAsync().GetAwaiter().GetResult();
    }

    protected Task SignInOperatorAsync() =>
        Services.GetRequiredService<PairedSessionState>().SetAsync("test-token", Guid.NewGuid(), "Review");

    protected Task SignOutOperatorAsync() => Services.GetRequiredService<PairedSessionState>().ClearAsync();

    protected IRenderedComponent<ChainOfCustody> RenderPage() => Render<ChainOfCustody>();
}

public class ChainOfCustody_OperatorGate_Tests : ChainOfCustodyPageTestBase
{
    [Fact]
    public async Task ChainOfCustody_OperatorSignedOut_ShowsSignInPrompt()
    {
        // Provider account linked, but no operator session: the sign-in prompt must render.
        await SignOutOperatorAsync();
        AuthServiceMock
            .Setup(m => m.IsAuthenticatedAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var component = RenderPage();

        Assert.Contains("You are not signed in.", component.Markup);
        Assert.Empty(component.FindAll(".alert-success"));
    }

    [Fact]
    public async Task ChainOfCustody_OperatorSignedInNoRingAccount_ShowsContent()
    {
        // Operator signed in, no provider account linked: the report reads the server DB only, so
        // the integrity banner must render and the sign-in prompt must not.
        await SignInOperatorAsync();
        AuthServiceMock
            .Setup(m => m.IsAuthenticatedAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var component = RenderPage();

        Assert.DoesNotContain("You are not signed in.", component.Markup);
        Assert.NotEmpty(component.FindAll(".alert-success"));
    }
}
