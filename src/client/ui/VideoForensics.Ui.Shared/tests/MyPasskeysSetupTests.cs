namespace VideoForensics.Ui.Shared.Tests;

using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
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
        WebAuthn = new Mock<WebAuthnClient>(Mock.Of<IJSRuntime>(), Mock.Of<ISelfApiHttpClientFactory>());
        Services.AddScoped(_ => WebAuthn.Object);
        Services.AddScoped(sp => new PairedSessionState(sp.GetRequiredService<IJSRuntime>()));
        SetRendererInfo(new RendererInfo("Server", true));
    }

    private Mock<WebAuthnClient> WebAuthn { get; }

    [Fact]
    public async Task MyPasskeys_SetupApproved_StillClearsAndSignsIn()
    {
        // Arrange - a limited create-passkey session adds its passkey
        var session = Services.GetRequiredService<PairedSessionState>();
        await session.SetAsync("setup-token", Guid.NewGuid(), "ReadOnly", passkeySetupOnly: true);
        WebAuthn.Setup(w => w.RegisterOperatorCredentialAsync("setup-token", "Phone"))
            .ReturnsAsync((true, null, true));
        var nav = Services.GetRequiredService<NavigationManager>();
        nav.NavigateTo("http://localhost/settings/passkeys?setup=true");
        var component = Render<MyPasskeys>();
        component.Find("input[type='text']").Change("Phone");

        // Act
        await component.Find("button.e-primary:not(:disabled)").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        // Assert - the server has ended the limited session, so the client drops it and signs in again
        component.WaitForAssertion(() => Assert.Equal("/signin", new Uri(nav.Uri).AbsolutePath));
        Assert.False(session.IsSignedIn);
        Assert.False(session.PasskeySetupOnly);
    }

    [Fact]
    public async Task MyPasskeys_SetupPendingApproval_ShowsMessageAndKeepsSession()
    {
        // Arrange - the setup passkey was added but an admin still has to approve it
        var session = Services.GetRequiredService<PairedSessionState>();
        await session.SetAsync("setup-token", Guid.NewGuid(), "ReadOnly", passkeySetupOnly: true);
        WebAuthn.Setup(w => w.RegisterOperatorCredentialAsync("setup-token", "Phone"))
            .ReturnsAsync((true, null, false));
        var nav = Services.GetRequiredService<NavigationManager>();
        nav.NavigateTo("http://localhost/settings/passkeys?setup=true");
        var component = Render<MyPasskeys>();
        component.Find("input[type='text']").Change("Phone");

        // Act
        await component.Find("button.e-primary:not(:disabled)").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        // Assert - the page explains the wait and keeps the session; it does not navigate away
        var pending = component.Find("[data-testid='passkey-setup-pending']");
        Assert.Equal(Services.GetRequiredService<IStringLocalizer<MyPasskeys>>()["PasskeySetupPendingApproval"].Value, pending.TextContent.Trim());
        Assert.True(session.IsSignedIn);
        Assert.True(session.PasskeySetupOnly);
        Assert.Equal("/settings/passkeys", new Uri(nav.Uri).AbsolutePath);
    }

    [Fact]
    public async Task MyPasskeys_SetupPendingApproval_SignOutClearsSessionAndNavigatesToSignIn()
    {
        // Arrange - a pending setup passkey leaves the operator on the message with a sign-out button
        var session = Services.GetRequiredService<PairedSessionState>();
        await session.SetAsync("setup-token", Guid.NewGuid(), "ReadOnly", passkeySetupOnly: true);
        WebAuthn.Setup(w => w.RegisterOperatorCredentialAsync("setup-token", "Phone"))
            .ReturnsAsync((true, null, false));
        var nav = Services.GetRequiredService<NavigationManager>();
        nav.NavigateTo("http://localhost/settings/passkeys?setup=true");
        var component = Render<MyPasskeys>();
        component.Find("input[type='text']").Change("Phone");
        await component.Find("button.e-primary:not(:disabled)").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        // Act
        await component.Find("button.passkey-setup-pending-signout").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        // Assert
        component.WaitForAssertion(() => Assert.Equal("/signin", new Uri(nav.Uri).AbsolutePath));
        Assert.False(session.IsSignedIn);
        Assert.False(session.PasskeySetupOnly);
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
