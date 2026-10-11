namespace VideoForensics.Ui.Shared.Tests;

using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Moq;
using VideoForensics.Ui.Shared.Pages;
using VideoForensics.Ui.Shared.Services;
using Xunit;

/// <summary>
/// Covers where SignIn.razor sends a user after a successful username/password sign-in:
/// a forced password change comes first, then a user with no operator-bound passkey is sent to
/// the passkey page to create one, and everyone else goes to the return URL.
/// </summary>
public class SignInPageTests : BunitContext
{
    private const string Username = "alice";
    private const string Password = "password123";

    public SignInPageTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddLocalization();
        Services.AddSingleton<Syncfusion.Blazor.ISyncfusionStringLocalizer, Syncfusion.Blazor.SyncfusionStringLocalizer>();
        Services.AddSingleton<Syncfusion.Blazor.GlobalOptions>();
        Services.AddScoped<Syncfusion.Blazor.SyncfusionBlazorService>();

        WebAuthn = new Mock<WebAuthnClient>(Mock.Of<IJSRuntime>(), Mock.Of<ISelfApiHttpClientFactory>());
        WebAuthn.Setup(w => w.IsAvailableAsync()).ReturnsAsync(false);
        WebAuthn.Setup(w => w.GetAuthMethodsAsync())
            .ReturnsAsync(new AuthMethodsInfo(PasswordEnabled: true, PasskeyEnabled: false));
        Services.AddScoped(_ => WebAuthn.Object);

        Services.AddScoped(sp => new PairedSessionState(sp.GetRequiredService<IJSRuntime>()));
        SetRendererInfo(new RendererInfo("Server", true));
    }

    private Mock<WebAuthnClient> WebAuthn { get; }

    [Fact]
    public async Task SignIn_PasswordLoginNoPasskey_NavigatesToPasskeySetup()
    {
        // Arrange
        SetupPasswordLogin(mustChangePassword: false, hasPasskey: false);
        var nav = Services.GetRequiredService<NavigationManager>();
        var component = await RenderAndWaitForFormAsync();

        // Act
        await SubmitPasswordLoginAsync(component);

        // Assert
        component.WaitForAssertion(() => Assert.Equal("/settings/passkeys?setup=true", new Uri(nav.Uri).PathAndQuery));
    }

    [Fact]
    public async Task SignIn_PasswordLoginWithPasskey_NavigatesToReturnUrl()
    {
        // Arrange
        SetupPasswordLogin(mustChangePassword: false, hasPasskey: true);
        var nav = Services.GetRequiredService<NavigationManager>();
        nav.NavigateTo("http://localhost/signin?returnUrl=%2Fcases%2F42");
        var component = await RenderAndWaitForFormAsync();

        // Act
        await SubmitPasswordLoginAsync(component);

        // Assert
        component.WaitForAssertion(() => Assert.Equal("/cases/42", new Uri(nav.Uri).PathAndQuery));
    }

    [Fact]
    public async Task SignIn_PasswordLoginMustChangePassword_NavigatesToChangePassword()
    {
        // Arrange - a forced change still comes before the passkey-setup redirect, even with no passkey
        SetupPasswordLogin(mustChangePassword: true, hasPasskey: false);
        var nav = Services.GetRequiredService<NavigationManager>();
        var component = await RenderAndWaitForFormAsync();

        // Act
        await SubmitPasswordLoginAsync(component);

        // Assert
        component.WaitForAssertion(() => Assert.Equal("/change-password", new Uri(nav.Uri).PathAndQuery));
    }

    private void SetupPasswordLogin(bool mustChangePassword, bool hasPasskey)
    {
        WebAuthn.Setup(w => w.SignInWithPasswordAsync(Username, Password))
            .ReturnsAsync((true, null, "session-token", Guid.NewGuid(), "ReadOnly", mustChangePassword, hasPasskey));
    }

    private async Task<IRenderedComponent<SignIn>> RenderAndWaitForFormAsync()
    {
        var component = Render<SignIn>();
        // SignIn shows a progress bar until its first-render OnAfterRenderAsync completes, then the form.
        await component.WaitForStateAsync(() => component.FindAll("input").Count >= 2);
        return component;
    }

    private static async Task SubmitPasswordLoginAsync(IRenderedComponent<SignIn> component)
    {
        component.Find("input[type='text']").Change(Username);
        component.Find("input[type='password']").Change(Password);
        await component.Find("button.e-primary").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
    }
}
