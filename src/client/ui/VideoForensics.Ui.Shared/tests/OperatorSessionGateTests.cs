
using Bunit;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;

using Moq;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Client.Core.Tools;
using VideoForensics.Core.Providers.Common.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Client.Ui.Shared.Services;

using Xunit;

using ExportEvidencePage = VideoForensics.Client.Ui.Shared.Pages.ExportEvidence;
using SettingsInfrastructurePage = VideoForensics.Client.Ui.Shared.Pages.SettingsInfrastructure;
using SettingsPage = VideoForensics.Client.Ui.Shared.Pages.Settings;
using ValidateEvidencePage = VideoForensics.Client.Ui.Shared.Pages.ValidateEvidence;

namespace VideoForensics.Client.Ui.Shared.Tests;
/// <summary>
/// Pages other than Sources (/accounts) must decide "signed in" from the operator session only. With an
/// operator session present and the camera-provider check returning false, the not-signed-in text must not show.
/// </summary>
public abstract class OperatorSessionGateTestBase : BunitContext
{
    protected const string NotSignedIn = "You are not signed in.";

    protected OperatorSessionGateTestBase()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        _ = Services.AddLocalization();
        ScopeOwnershipTestDoubles.RegisterEmpty(Services);
        _ = Services.AddSingleton<Syncfusion.Blazor.ISyncfusionStringLocalizer, Syncfusion.Blazor.SyncfusionStringLocalizer>();
        _ = Services.AddSingleton<Syncfusion.Blazor.GlobalOptions>();
        _ = Services.AddScoped<Syncfusion.Blazor.SyncfusionBlazorService>();

        // Provider check is false: no camera account linked.
        var auth = new Mock<IProviderAuthService>();
        _ = auth.Setup(m => m.IsAuthenticatedAsync(It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _ = Services.AddScoped(_ => auth.Object);
        _ = Services.AddScoped(sp => new PairedSessionState(sp.GetRequiredService<IJSRuntime>()));

        var deviceRepo = new Mock<IDeviceRepository>();
        _ = deviceRepo.Setup(m => m.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _ = Services.AddScoped(_ => deviceRepo.Object);
        _ = Services.AddScoped(_ => Mock.Of<IEvidenceExportService>());
        _ = Services.AddScoped(_ => Mock.Of<ICaseRepository>());
        _ = Services.AddScoped(_ => Mock.Of<IEvidenceValidationService>());
        _ = Services.AddScoped(_ => Mock.Of<IEvidenceValidationJobs>());
        _ = Services.AddScoped(_ => Mock.Of<IForensicsConfiguration>());
        _ = Services.AddScoped(_ => Mock.Of<IForensicsConfigurationService>());
        _ = Services.AddScoped(_ => Mock.Of<IServerLocationInformationService>());
        _ = Services.AddScoped(sp => new ConfigToolsOrchestrator(
            NullLogger<ConfigToolsOrchestrator>.Instance, sp.GetRequiredService<IForensicsConfigurationService>()));
        // The export page runs a passkey step-up before exporting; the gate test only checks the operator is signed in.
        _ = Services.AddScoped(_ => new Mock<WebAuthnClient>(Mock.Of<Microsoft.JSInterop.IJSRuntime>(), Mock.Of<ISelfApiHttpClientFactory>()).Object);
        _ = Services.AddScoped<StepUpService>();
        // The pages inject the interface; the host registers it over the concrete orchestrator.
        _ = Services.AddScoped<Core.Contracts.IConfigToolsOrchestrator>(sp => sp.GetRequiredService<ConfigToolsOrchestrator>());
        _ = Services.AddScoped<RightPanelContentService>();
        _ = Services.AddScoped(_ => Mock.Of<ICultureSwitcher>());
        _ = Services.AddScoped(_ => Mock.Of<IOperatorPreferencesRepository>());
        _ = Services.AddScoped(sp => new ThemePreferenceService(
            sp.GetRequiredService<IJSRuntime>(),
            sp.GetRequiredService<IOperatorPreferencesRepository>(),
            sp.GetRequiredService<PairedSessionState>(),
            sp.GetRequiredService<ICultureSwitcher>()));
    }

    protected Task SignInOperatorAsync()
    {
        return Services.GetRequiredService<PairedSessionState>().SetAsync("test-token", Guid.NewGuid(), "Review");
    }
}

public class OperatorSessionGateTests : OperatorSessionGateTestBase
{
    [Fact]
    public async Task Settings_OperatorSignedInProviderCheckFalse_DoesNotShowNotSignedIn()
    {
        await SignInOperatorAsync();
        IRenderedComponent<SettingsPage> component = Render<SettingsPage>();
        Assert.DoesNotContain(NotSignedIn, component.Markup);
    }

    [Fact]
    public async Task SettingsInfrastructure_OperatorSignedInProviderCheckFalse_DoesNotShowNotSignedIn()
    {
        await SignInOperatorAsync();
        IRenderedComponent<SettingsInfrastructurePage> component = Render<SettingsInfrastructurePage>();
        Assert.DoesNotContain(NotSignedIn, component.Markup);
    }

    [Fact]
    public async Task ExportEvidence_OperatorSignedInProviderCheckFalse_DoesNotShowNotSignedIn()
    {
        await SignInOperatorAsync();
        IRenderedComponent<ExportEvidencePage> component = Render<ExportEvidencePage>();
        Assert.DoesNotContain(NotSignedIn, component.Markup);
    }

    [Fact]
    public async Task ValidateEvidence_OperatorSignedInProviderCheckFalse_DoesNotShowNotSignedIn()
    {
        await SignInOperatorAsync();
        IRenderedComponent<ValidateEvidencePage> component = Render<ValidateEvidencePage>();
        Assert.DoesNotContain(NotSignedIn, component.Markup);
    }

    [Fact]
    public void Settings_NoOperatorSession_ShowsNotSignedIn()
    {
        IRenderedComponent<SettingsPage> component = Render<SettingsPage>();
        Assert.Contains(NotSignedIn, component.Markup);
    }
}
