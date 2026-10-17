namespace VideoForensics.Ui.Shared.Tests;

using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Moq;
using Xunit;
using VideoForensics.Client.Common.Contracts;
using VideoForensics.Client.Core.Tools;
using VideoForensics.Core.Providers.Common.Contracts;
using VideoForensics.Data.Common.Contracts;
using Device = VideoForensics.Data.Common.Entities.Device;
using VideoForensics.Ui.Shared.Services;
using SettingsPage = VideoForensics.Ui.Shared.Pages.Settings;
using SettingsInfrastructurePage = VideoForensics.Ui.Shared.Pages.SettingsInfrastructure;
using ExportEvidencePage = VideoForensics.Ui.Shared.Pages.ExportEvidence;
using ValidateEvidencePage = VideoForensics.Ui.Shared.Pages.ValidateEvidence;

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
        Services.AddLocalization();
        Services.AddSingleton<Syncfusion.Blazor.ISyncfusionStringLocalizer, Syncfusion.Blazor.SyncfusionStringLocalizer>();
        Services.AddSingleton<Syncfusion.Blazor.GlobalOptions>();
        Services.AddScoped<Syncfusion.Blazor.SyncfusionBlazorService>();

        // Provider check is false: no camera account linked.
        var auth = new Mock<IProviderAuthService>();
        auth.Setup(m => m.IsAuthenticatedAsync(It.IsAny<CancellationToken>())).ReturnsAsync(false);
        Services.AddScoped(_ => auth.Object);
        Services.AddScoped(sp => new PairedSessionState(sp.GetRequiredService<IJSRuntime>()));

        var deviceRepo = new Mock<IDeviceRepository>();
        deviceRepo.Setup(m => m.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Device>());
        Services.AddScoped(_ => deviceRepo.Object);
        Services.AddScoped(_ => Mock.Of<IEvidenceExportService>());
        Services.AddScoped(_ => Mock.Of<VideoForensics.Data.Common.Contracts.ICaseRepository>());
        Services.AddScoped(_ => Mock.Of<IEvidenceValidationService>());
        Services.AddScoped(_ => Mock.Of<IForensicsConfiguration>());
        Services.AddScoped(_ => Mock.Of<IForensicsConfigurationService>());
        Services.AddScoped(_ => Mock.Of<IServerLocationInformationService>());
        Services.AddScoped(sp => new ConfigToolsOrchestrator(
            NullLogger<ConfigToolsOrchestrator>.Instance, sp.GetRequiredService<IForensicsConfigurationService>()));
        // The export page runs a passkey step-up before exporting; the gate test only checks the operator is signed in.
        Services.AddScoped(_ => new Mock<VideoForensics.Ui.Shared.Services.WebAuthnClient>(Mock.Of<Microsoft.JSInterop.IJSRuntime>(), Mock.Of<VideoForensics.Ui.Shared.Services.ISelfApiHttpClientFactory>()).Object);
        Services.AddScoped<VideoForensics.Ui.Shared.Services.StepUpService>();
        // The pages inject the interface; the host registers it over the concrete orchestrator.
        Services.AddScoped<VideoForensics.Client.Core.Contracts.IConfigToolsOrchestrator>(sp => sp.GetRequiredService<ConfigToolsOrchestrator>());
        Services.AddScoped<RightPanelContentService>();
        Services.AddScoped(_ => Mock.Of<ICultureSwitcher>());
        Services.AddScoped(_ => Mock.Of<IOperatorPreferencesRepository>());
        Services.AddScoped(sp => new ThemePreferenceService(
            sp.GetRequiredService<IJSRuntime>(),
            sp.GetRequiredService<IOperatorPreferencesRepository>(),
            sp.GetRequiredService<PairedSessionState>(),
            sp.GetRequiredService<ICultureSwitcher>()));
    }

    protected Task SignInOperatorAsync() =>
        Services.GetRequiredService<PairedSessionState>().SetAsync("test-token", Guid.NewGuid(), "Review");
}

public class OperatorSessionGateTests : OperatorSessionGateTestBase
{
    [Fact]
    public async Task Settings_OperatorSignedInProviderCheckFalse_DoesNotShowNotSignedIn()
    {
        await SignInOperatorAsync();
        var component = Render<SettingsPage>();
        Assert.DoesNotContain(NotSignedIn, component.Markup);
    }

    [Fact]
    public async Task SettingsInfrastructure_OperatorSignedInProviderCheckFalse_DoesNotShowNotSignedIn()
    {
        await SignInOperatorAsync();
        var component = Render<SettingsInfrastructurePage>();
        Assert.DoesNotContain(NotSignedIn, component.Markup);
    }

    [Fact]
    public async Task ExportEvidence_OperatorSignedInProviderCheckFalse_DoesNotShowNotSignedIn()
    {
        await SignInOperatorAsync();
        var component = Render<ExportEvidencePage>();
        Assert.DoesNotContain(NotSignedIn, component.Markup);
    }

    [Fact]
    public async Task ValidateEvidence_OperatorSignedInProviderCheckFalse_DoesNotShowNotSignedIn()
    {
        await SignInOperatorAsync();
        var component = Render<ValidateEvidencePage>();
        Assert.DoesNotContain(NotSignedIn, component.Markup);
    }

    [Fact]
    public void Settings_NoOperatorSession_ShowsNotSignedIn()
    {
        var component = Render<SettingsPage>();
        Assert.Contains(NotSignedIn, component.Markup);
    }
}
