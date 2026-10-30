using Bunit;

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;

using Moq;

using VideoForensics.Client.Ui.Shared.Pages;
using VideoForensics.Client.Ui.Shared.Services;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests;

/// <summary>
/// Remember me on the sign-in page: a remembered username pre-fills the form and tries passkey sign-in once,
/// and the password box shows whenever the sign-in is by password.
/// </summary>
public class SignInRememberMeTests : BunitContext
{
    public SignInRememberMeTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        _ = Services.AddLocalization();
        _ = Services.AddSingleton<Syncfusion.Blazor.ISyncfusionStringLocalizer, Syncfusion.Blazor.SyncfusionStringLocalizer>();
        _ = Services.AddSingleton<Syncfusion.Blazor.GlobalOptions>();
        _ = Services.AddScoped<Syncfusion.Blazor.SyncfusionBlazorService>();

        WebAuthn = new Mock<WebAuthnClient>(Mock.Of<IJSRuntime>(), Mock.Of<ISelfApiHttpClientFactory>());
        _ = WebAuthn.Setup(w => w.IsAvailableAsync()).ReturnsAsync(true);
        _ = WebAuthn.Setup(w => w.IsPasskeyAvailableOnThisAddressAsync()).ReturnsAsync(true);
        _ = WebAuthn.Setup(w => w.GetAuthMethodsAsync())
            .ReturnsAsync(new AuthMethodsInfo(PasswordEnabled: true, PasskeyEnabled: true));
        _ = WebAuthn.Setup(w => w.SignInWithUsernameAsync(It.IsAny<string>(), It.IsAny<bool>()))
            .ReturnsAsync((false, "No passkey was accepted.", null, Guid.Empty, string.Empty));
        _ = Services.AddScoped(_ => WebAuthn.Object);

        _ = Services.AddScoped(sp => new PairedSessionState(sp.GetRequiredService<IJSRuntime>()));
        SetRendererInfo(new RendererInfo("Server", true));
    }

    private Mock<WebAuthnClient> WebAuthn { get; }

    [Fact]
    public void SignIn_RememberedUserWithPasskey_TriesPasskeyOnceOnLoad()
    {
        // Arrange
        JSInterop.Setup<string?>("localStorage.getItem", RememberedLogin.StorageKey).SetResult("alice");

        // Act
        _ = Render<SignIn>();

        // Assert
        WebAuthn.Verify(w => w.SignInWithUsernameAsync("alice", true), Times.Once);
    }

    [Fact]
    public void SignIn_NoRememberedUser_DoesNotTryPasskeyOnLoad()
    {
        // Arrange and Act
        _ = Render<SignIn>();

        // Assert
        WebAuthn.Verify(w => w.SignInWithUsernameAsync(It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
    }

    [Fact]
    public void SignIn_PasskeyFailsForRememberedUser_ShowsThePasswordBox()
    {
        // Arrange
        JSInterop.Setup<string?>("localStorage.getItem", RememberedLogin.StorageKey).SetResult("alice");

        // Act
        IRenderedComponent<SignIn> cut = Render<SignIn>();

        // Assert
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("input[name='password']")));
    }

    [Fact]
    public void SignIn_BrowserWithoutPasskeys_ShowsThePasswordBoxWithoutAClick()
    {
        // Arrange
        WebAuthn.Setup(w => w.IsAvailableAsync()).ReturnsAsync(false);

        // Act
        IRenderedComponent<SignIn> cut = Render<SignIn>();

        // Assert
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("input[name='password']")));
    }

    [Fact]
    public async Task PairedSessionState_SignOut_ForgetsTheRememberedUser()
    {
        // Arrange
        var session = new PairedSessionState(Services.GetRequiredService<IJSRuntime>());

        // Act
        await session.SignOutAsync();

        // Assert
        Assert.Contains(JSInterop.Invocations, i =>
            i.Identifier == "localStorage.removeItem" && i.Arguments.Count > 0 && i.Arguments[0]!.ToString() == RememberedLogin.StorageKey);
    }
}
