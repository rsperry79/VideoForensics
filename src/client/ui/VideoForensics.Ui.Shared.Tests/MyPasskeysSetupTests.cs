namespace VideoForensics.Ui.Shared.Tests;

using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Moq;
using VideoForensics.Ui.Shared.Pages;
using VideoForensics.Ui.Shared.Services;
using Xunit;

/// <summary>
/// Covers the setup prompt on MyPasskeys: SignIn sends a user with no passkey to /settings/passkeys?setup=true,
/// and the page shows the prompt only when that query flag is present.
/// </summary>
public class MyPasskeysSetupTests : BunitContext
{
    private const string SetupPromptTestId = "[data-testid='passkey-setup-prompt']";

    public MyPasskeysSetupTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddLocalization();
        Services.AddSingleton<Syncfusion.Blazor.ISyncfusionStringLocalizer, Syncfusion.Blazor.SyncfusionStringLocalizer>();
        Services.AddSingleton<Syncfusion.Blazor.GlobalOptions>();
        Services.AddScoped<Syncfusion.Blazor.SyncfusionBlazorService>();
        Services.AddScoped(_ => new Mock<WebAuthnClient>(Mock.Of<IJSRuntime>(), Mock.Of<ISelfApiHttpClientFactory>()).Object);
        Services.AddScoped(sp => new PairedSessionState(sp.GetRequiredService<IJSRuntime>()));
        SetRendererInfo(new RendererInfo("Server", true));
    }

    [Fact]
    public void MyPasskeys_SetupQuery_ShowsSetupPrompt()
    {
        // Arrange
        Services.GetRequiredService<NavigationManager>().NavigateTo("http://localhost/settings/passkeys?setup=true");

        // Act
        var component = Render<MyPasskeys>();

        // Assert
        Assert.NotEmpty(component.FindAll(SetupPromptTestId));
    }

    [Fact]
    public void MyPasskeys_NoSetupQuery_HidesSetupPrompt()
    {
        // Arrange
        Services.GetRequiredService<NavigationManager>().NavigateTo("http://localhost/settings/passkeys");

        // Act
        var component = Render<MyPasskeys>();

        // Assert
        Assert.Empty(component.FindAll(SetupPromptTestId));
    }
}
