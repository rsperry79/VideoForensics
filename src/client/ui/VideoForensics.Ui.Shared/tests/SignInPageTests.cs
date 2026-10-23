
using AngleSharp.Dom;

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
        _ = Services.AddLocalization();
        _ = Services.AddSingleton<Syncfusion.Blazor.ISyncfusionStringLocalizer, Syncfusion.Blazor.SyncfusionStringLocalizer>();
        _ = Services.AddSingleton<Syncfusion.Blazor.GlobalOptions>();
        _ = Services.AddScoped<Syncfusion.Blazor.SyncfusionBlazorService>();

        WebAuthn = new Mock<WebAuthnClient>(Mock.Of<IJSRuntime>(), Mock.Of<ISelfApiHttpClientFactory>());
        _ = WebAuthn.Setup(w => w.IsAvailableAsync()).ReturnsAsync(false);
        _ = WebAuthn.Setup(w => w.GetAuthMethodsAsync())
            .ReturnsAsync(new AuthMethodsInfo(PasswordEnabled: true, PasskeyEnabled: false));
        _ = Services.AddScoped(_ => WebAuthn.Object);

        _ = Services.AddScoped(sp => new PairedSessionState(sp.GetRequiredService<IJSRuntime>()));
        SetRendererInfo(new RendererInfo("Server", true));
    }

    private Mock<WebAuthnClient> WebAuthn { get; }

    [Fact]
    public async Task SignIn_PasswordLoginNoPasskey_NavigatesToPasskeySetup()
    {
        // Arrange
        SetupPasswordLogin(mustChangePassword: false, hasPasskey: false);
        NavigationManager nav = Services.GetRequiredService<NavigationManager>();
        IRenderedComponent<SignIn> component = await RenderAndWaitForFormAsync();

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
        NavigationManager nav = Services.GetRequiredService<NavigationManager>();
        nav.NavigateTo("http://localhost/signin?returnUrl=%2Fcases%2F42");
        IRenderedComponent<SignIn> component = await RenderAndWaitForFormAsync();

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
        NavigationManager nav = Services.GetRequiredService<NavigationManager>();
        IRenderedComponent<SignIn> component = await RenderAndWaitForFormAsync();

        // Act
        await SubmitPasswordLoginAsync(component);

        // Assert
        component.WaitForAssertion(() => Assert.Equal("/change-password", new Uri(nav.Uri).PathAndQuery));
    }

    [Fact]
    public async Task SignIn_TwoFactorRequired_ShowsPasskeyAndCreateChoices()
    {
        // Arrange - the operator has a passkey, so the server asks for a second factor instead of issuing a session
        SetupTwoFactorRequired("corr-123");
        IRenderedComponent<SignIn> component = await RenderAndWaitForFormAsync();

        // Act
        await SubmitPasswordLoginAsync(component);

        // Assert
        component.WaitForAssertion(() =>
        {
            var labels = component.FindAll("button").Select(b => b.TextContent.Trim()).ToList();
            Assert.Contains("Use passkey", labels);
            Assert.Contains("Create a new passkey", labels);
        });
    }

    [Fact]
    public async Task SignIn_CreatePasskeyChoice_NavigatesToPasskeySetup()
    {
        // Arrange
        SetupTwoFactorRequired("corr-123");
        _ = WebAuthn.Setup(w => w.CreatePasskeyLoginAsync(Username, Password))
            .ReturnsAsync((true, null, "passkey-setup-token", Guid.NewGuid(), "ReadOnly", false, false, false, null, Array.Empty<string>()));
        NavigationManager nav = Services.GetRequiredService<NavigationManager>();
        IRenderedComponent<SignIn> component = await RenderAndWaitForFormAsync();
        await SubmitPasswordLoginAsync(component);
        component.WaitForAssertion(() => Assert.Contains(component.FindAll("button"), b => b.TextContent.Trim() == "Create a new passkey"));

        // Act
        await ClickButtonAsync(component, "Create a new passkey");

        // Assert
        component.WaitForAssertion(() => Assert.Equal("/settings/passkeys?setup=true", new Uri(nav.Uri).PathAndQuery));
    }

    [Fact]
    public async Task SignIn_CreatePasskeyChoice_SetsPasskeySetupOnly()
    {
        // Arrange
        SetupTwoFactorRequired("corr-123");
        _ = WebAuthn.Setup(w => w.CreatePasskeyLoginAsync(Username, Password))
            .ReturnsAsync((true, null, "passkey-setup-token", Guid.NewGuid(), "ReadOnly", false, false, false, null, Array.Empty<string>()));
        NavigationManager nav = Services.GetRequiredService<NavigationManager>();
        PairedSessionState session = Services.GetRequiredService<PairedSessionState>();
        IRenderedComponent<SignIn> component = await RenderAndWaitForFormAsync();
        await SubmitPasswordLoginAsync(component);
        component.WaitForAssertion(() => Assert.Contains(component.FindAll("button"), b => b.TextContent.Trim() == "Create a new passkey"));

        // Act
        await ClickButtonAsync(component, "Create a new passkey");

        // Assert
        component.WaitForAssertion(() => Assert.Equal("/settings/passkeys?setup=true", new Uri(nav.Uri).PathAndQuery));
        Assert.True(session.PasskeySetupOnly);
    }

    [Fact]
    public async Task SignIn_TwoFactorRequired_HidesPasswordFieldsAndShowsPasskeyButtons()
    {
        // Arrange
        SetupTwoFactorRequired("corr-123");
        IRenderedComponent<SignIn> component = await RenderAndWaitForFormAsync();

        // Act
        await SubmitPasswordLoginAsync(component);

        // Assert
        component.WaitForAssertion(() =>
        {
            Assert.Empty(component.FindAll("input[type='text']"));
            Assert.Empty(component.FindAll("input[type='password']"));
            var labels = component.FindAll("button").Select(b => b.TextContent.Trim()).ToList();
            Assert.DoesNotContain("Sign In", labels);
            Assert.Contains("Use passkey", labels);
            Assert.Contains("Create a new passkey", labels);
        });
    }

    [Fact]
    public async Task SignIn_TwoFactorRequired_ShowsNoErrorText()
    {
        // Arrange - a failed attempt first, so a stale error exists when the two-factor response arrives
        _ = WebAuthn.Setup(w => w.SignInWithPasswordAsync(Username, Password, It.IsAny<bool>()))
            .ReturnsAsync((false, "Wrong password", null, default, string.Empty, false, false, false, null, Array.Empty<string>()));
        IRenderedComponent<SignIn> component = await RenderAndWaitForFormAsync();
        await SubmitPasswordLoginAsync(component);
        component.WaitForAssertion(() => Assert.NotEmpty(component.FindAll(".alert-danger")));

        SetupTwoFactorRequired("corr-123");

        // Act
        await SubmitPasswordLoginAsync(component);

        // Assert
        component.WaitForAssertion(() => Assert.Contains("Use passkey", component.FindAll("button").Select(b => b.TextContent.Trim())));
        Assert.Empty(component.FindAll(".alert-danger"));
        Assert.DoesNotContain("Wrong password", component.Markup);
    }

    [Fact]
    public async Task SignIn_TwoFactorRequired_BackRestoresPasswordFields()
    {
        // Arrange
        SetupTwoFactorRequired("corr-123");
        IStringLocalizer<SignIn> localizer = Services.GetRequiredService<IStringLocalizer<SignIn>>();
        IRenderedComponent<SignIn> component = await RenderAndWaitForFormAsync();
        await SubmitPasswordLoginAsync(component);
        component.WaitForAssertion(() => Assert.Contains(component.FindAll("button"), b => b.TextContent.Trim() == "Back"));

        // Act
        await ClickButtonAsync(component, "Back");

        // Assert
        component.WaitForAssertion(() =>
        {
            Assert.NotEmpty(component.FindAll("input[type='text']"));
            Assert.NotEmpty(component.FindAll("input[type='password']"));
            var labels = component.FindAll("button").Select(b => b.TextContent.Trim()).ToList();
            Assert.Contains(localizer["SignInButton"], labels);
            Assert.DoesNotContain("Use passkey", labels);
        });
    }

    [Fact]
    public async Task SignIn_NoTempDiagnosticsRendered()
    {
        // Arrange - a successful password sign-in previously rendered the diagnostic block
        SetupPasswordLogin(mustChangePassword: false, hasPasskey: true);
        IRenderedComponent<SignIn> component = await RenderAndWaitForFormAsync();

        // Act
        await SubmitPasswordLoginAsync(component);

        // Assert
        Assert.DoesNotContain("password result:", component.Markup);
        Assert.Empty(component.FindAll("pre"));
        Assert.Empty(component.FindAll("[class*='diag']"));
    }

    [Fact]
    public async Task SignIn_TwoFactorRequired_ShowsTitleAndNoIntroOrSignInPasskey()
    {
        // Arrange - passkey sign-in is offered before password entry, so it must go once a second factor is required
        _ = WebAuthn.Setup(w => w.IsAvailableAsync()).ReturnsAsync(true);
        SetupTwoFactorRequired("corr-123");
        IStringLocalizer<SignIn> localizer = Services.GetRequiredService<IStringLocalizer<SignIn>>();
        IRenderedComponent<SignIn> component = await RenderAndWaitForFormAsync();

        // Act - reveal the credential fields, then submit with ClickButtonAsync: the passkey button is now first among e-primary buttons
        await ClickButtonAsync(component, localizer["UseUserNameAndPasswordButton"]);
        component.Find("input[type='text']").Change(Username);
        component.Find("input[type='password']").Change(Password);
        await ClickButtonAsync(component, localizer["SignInButton"]);

        // Assert
        component.WaitForAssertion(() =>
        {
            Assert.Contains(localizer["TwoFactorRequiredTitle"], component.Markup);
            Assert.Contains(localizer["TwoFactorRequiredDescription"], component.Markup);
            var labels = component.FindAll("button").Select(b => b.TextContent.Trim()).ToList();
            Assert.DoesNotContain(localizer["SignInWithPasskeyButton"], labels);
        });
    }

    [Fact]
    public async Task SignIn_TwoFactorRequired_CreatePasskeyEnabled()
    {
        // Arrange
        SetupTwoFactorRequired("corr-123");
        IRenderedComponent<SignIn> component = await RenderAndWaitForFormAsync();

        // Act
        await SubmitPasswordLoginAsync(component);

        // Assert - neither the busy flag from the password call nor any other state may disable these controls
        component.WaitForAssertion(() =>
        {
            Assert.False(FindButton(component, "Use passkey").HasAttribute("disabled"));
            Assert.False(FindButton(component, "Create a new passkey").HasAttribute("disabled"));
            Assert.False(FindButton(component, "Back").HasAttribute("disabled"));
        });
    }

    [Fact]
    public async Task SignIn_TwoFactorRequired_HidesSignInHeading()
    {
        // Arrange - the "Sign In" page heading is shown in the normal state only
        SetupTwoFactorRequired("corr-123");
        IStringLocalizer<SignIn> localizer = Services.GetRequiredService<IStringLocalizer<SignIn>>();
        IRenderedComponent<SignIn> component = await RenderAndWaitForFormAsync();
        Assert.Contains(component.FindAll("h3"), h => h.TextContent.Trim() == localizer["Title"]);

        // Act
        await SubmitPasswordLoginAsync(component);

        // Assert
        component.WaitForAssertion(() =>
        {
            Assert.Contains("Use passkey", component.FindAll("button").Select(b => b.TextContent.Trim()));
            Assert.DoesNotContain(component.FindAll("h1,h2,h3,h4,h5,h6"), h => h.TextContent.Trim() == localizer["Title"]);
        });
    }

    [Fact]
    public async Task SignIn_TwoFactorRequired_CreatePasskeyNotOutlineGrey()
    {
        // Arrange
        SetupTwoFactorRequired("corr-123");
        IRenderedComponent<SignIn> component = await RenderAndWaitForFormAsync();

        // Act
        await SubmitPasswordLoginAsync(component);

        // Assert - an e-outline button renders grey text and reads as disabled; this one must be an active primary button
        component.WaitForAssertion(() =>
        {
            ITokenList classes = FindButton(component, "Create a new passkey").ClassList;
            Assert.Contains("e-primary", classes);
            Assert.DoesNotContain("e-outline", classes);
        });
    }

    [Fact]
    public async Task SignIn_TwoFactorRequired_UsePasskeyHiddenWhenMethodMissing()
    {
        // Arrange - the server accepts no second factor this client knows, so there is nothing to offer as "Use passkey"
        _ = WebAuthn.Setup(w => w.SignInWithPasswordAsync(Username, Password, It.IsAny<bool>()))
            .ReturnsAsync((true, null, null, default, string.Empty, false, false, true, "corr-123", Array.Empty<string>()));
        IRenderedComponent<SignIn> component = await RenderAndWaitForFormAsync();

        // Act
        await SubmitPasswordLoginAsync(component);

        // Assert
        component.WaitForAssertion(() =>
        {
            var labels = component.FindAll("button").Select(b => b.TextContent.Trim()).ToList();
            Assert.DoesNotContain("Use passkey", labels);
            Assert.Contains("Create a new passkey", labels);
            Assert.Contains("Back", labels);
        });
    }

    [Fact]
    public async Task SignIn_TwoFactorRequired_NoErrorText()
    {
        // Arrange - a fresh two-factor response with no earlier failure
        SetupTwoFactorRequired("corr-123");
        IRenderedComponent<SignIn> component = await RenderAndWaitForFormAsync();

        // Act
        await SubmitPasswordLoginAsync(component);

        // Assert - the user is still signed in at limited access; a second-factor step is not an error
        component.WaitForAssertion(() => Assert.Contains("Use passkey", component.FindAll("button").Select(b => b.TextContent.Trim())));
        Assert.Empty(component.FindAll(".alert-danger"));
    }

    [Fact]
    public async Task SignIn_TwoFactorRequired_HidesRegisterLink()
    {
        // Arrange - the operator is already signed in at limited access, so the register prompt must not show
        SetupTwoFactorRequired("corr-123");
        IStringLocalizer<SignIn> localizer = Services.GetRequiredService<IStringLocalizer<SignIn>>();
        IRenderedComponent<SignIn> component = await RenderAndWaitForFormAsync();
        Assert.Contains(localizer["NoAccountText"], component.Markup);

        // Act
        await SubmitPasswordLoginAsync(component);

        // Assert
        component.WaitForAssertion(() =>
        {
            Assert.DoesNotContain(localizer["NoAccountText"], component.Markup);
            Assert.Empty(component.FindAll("a[href='/register']"));
        });
    }

    [Fact]
    public async Task SignIn_NormalState_HeadingUsesTitleResource()
    {
        // Arrange - the page heading is a light h3 carrying the product-named Title resource
        IStringLocalizer<SignIn> localizer = Services.GetRequiredService<IStringLocalizer<SignIn>>();
        IRenderedComponent<SignIn> component = await RenderAndWaitForFormAsync();

        // Act & Assert
        component.WaitForAssertion(() =>
            Assert.Contains(component.FindAll("h3.fw-light"), h => h.TextContent.Trim() == localizer["Title"]));
    }

    [Fact]
    public async Task SignIn_NormalState_UsernameAndPasswordUseFloatingPlaceholders()
    {
        // Arrange
        IStringLocalizer<SignIn> localizer = Services.GetRequiredService<IStringLocalizer<SignIn>>();
        IRenderedComponent<SignIn> component = await RenderAndWaitForFormAsync();

        // Act & Assert - floating-label textboxes carry the label text as their placeholder
        // Syncfusion renders a FloatLabelType textbox's Placeholder as its floating label (.e-float-text), not as an input attribute
        component.WaitForAssertion(() =>
        {
            var floatingLabels = component.FindAll("label.e-float-text").Select(l => l.TextContent.Trim()).ToList();
            Assert.Contains(localizer["UsernameLabel"], floatingLabels);
            Assert.Contains(localizer["PasswordLabel"], floatingLabels);
        });
    }

    [Fact]
    public async Task SignIn_PasskeyAvailable_PasskeyButtonIsActivePrimary()
    {
        // Arrange - the passkey button is a full primary button, not an outlined one that reads as disabled
        _ = WebAuthn.Setup(w => w.IsAvailableAsync()).ReturnsAsync(true);
        IStringLocalizer<SignIn> localizer = Services.GetRequiredService<IStringLocalizer<SignIn>>();
        IRenderedComponent<SignIn> component = await RenderAndWaitForFormAsync();

        // Act & Assert
        component.WaitForAssertion(() =>
        {
            ITokenList classes = FindButton(component, localizer["SignInWithPasskeyButton"]).ClassList;
            Assert.Contains("e-primary", classes);
            Assert.DoesNotContain("e-outline", classes);
        });
    }

    [Fact]
    public async Task SignIn_NormalState_HasNoRememberMeOrForgotPassword()
    {
        // Arrange - no backing behavior exists for these, so they must not be rendered
        _ = WebAuthn.Setup(w => w.IsAvailableAsync()).ReturnsAsync(true);
        IRenderedComponent<SignIn> component = await RenderAndWaitForFormAsync();

        // Act & Assert
        IStringLocalizer<SignIn> localizer = Services.GetRequiredService<IStringLocalizer<SignIn>>();
        await ClickButtonAsync(component, localizer["UseUserNameAndPasswordButton"]);
        component.WaitForAssertion(() => Assert.NotNull(FindButton(component, localizer["SignInButton"])));
        Assert.DoesNotContain("Remember", component.Markup);
        Assert.DoesNotContain("Forgot", component.Markup);
    }
    [Fact]
    public async Task SignIn_UsernameField_IsMarkedAsAutofillableUsername()
    {
        _ = WebAuthn.Setup(w => w.IsAvailableAsync()).ReturnsAsync(true);
        IRenderedComponent<SignIn> component = await RenderAndWaitForFormAsync();

        Assert.Equal("username webauthn", component.Find("input[name='username']").GetAttribute("autocomplete"));
    }

    [Fact]
    public async Task SignIn_PasskeyAvailable_EnterInUsername_StartsPasskeySignIn()
    {
        _ = WebAuthn.Setup(w => w.IsAvailableAsync()).ReturnsAsync(true);
        _ = WebAuthn.Setup(w => w.SignInWithUsernameAsync("sysop", true))
            .ReturnsAsync((true, null, "token", Guid.NewGuid(), "Operator"));
        IRenderedComponent<SignIn> component = await RenderAndWaitForFormAsync();

        component.Find("input[type='text']").Change("sysop");
        component.Find("#username-passkey-entry").KeyDown(new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs { Key = "Enter" });

        component.WaitForAssertion(() => WebAuthn.Verify(w => w.SignInWithUsernameAsync("sysop", true), Times.Once));
    }

    [Fact]
    public async Task SignIn_PasskeyAvailable_OtherKeyInUsername_DoesNotStartPasskeySignIn()
    {
        _ = WebAuthn.Setup(w => w.IsAvailableAsync()).ReturnsAsync(true);
        IRenderedComponent<SignIn> component = await RenderAndWaitForFormAsync();

        component.Find("input[type='text']").Change("sysop");
        component.Find("#username-passkey-entry").KeyDown(new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs { Key = "a" });

        WebAuthn.Verify(w => w.SignInWithUsernameAsync(It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
    }

    [Fact]
    public async Task SignIn_PasskeyAvailable_CredentialFieldsHiddenUntilToggled()
    {
        // Arrange - with passkey sign-in available, the username is shown (the passkey needs it) and the password stays hidden behind a button
        _ = WebAuthn.Setup(w => w.IsAvailableAsync()).ReturnsAsync(true);
        IStringLocalizer<SignIn> localizer = Services.GetRequiredService<IStringLocalizer<SignIn>>();
        IRenderedComponent<SignIn> component = await RenderAndWaitForFormAsync();

        // Assert - username visible by default, password hidden
        Assert.NotEmpty(component.FindAll("input[type='text']"));
        Assert.Empty(component.FindAll("input[type='password']"));
        Assert.NotNull(FindButton(component, localizer["UseUserNameAndPasswordButton"]));

        // Act
        await ClickButtonAsync(component, localizer["UseUserNameAndPasswordButton"]);

        // Assert - fields appear and the toggle goes away
        component.WaitForAssertion(() =>
        {
            Assert.NotEmpty(component.FindAll("input[type='text']"));
            Assert.NotEmpty(component.FindAll("input[type='password']"));
            var labels = component.FindAll("button").Select(b => b.TextContent.Trim()).ToList();
            Assert.DoesNotContain(localizer["UseUserNameAndPasswordButton"], labels);
        });
    }

    [Fact]
    public async Task SignIn_NormalState_ShowsRestrictedAccessNotice()
    {
        // Arrange
        IStringLocalizer<SignIn> localizer = Services.GetRequiredService<IStringLocalizer<SignIn>>();
        IRenderedComponent<SignIn> component = await RenderAndWaitForFormAsync();

        // Assert
        IElement notice = component.Find("[data-testid='restricted-access-notice']");
        Assert.Equal(localizer["RestrictedAccessNotice"], notice.TextContent.Trim());
    }

    [Fact]
    public async Task SignIn_PasskeyUnavailable_CredentialFieldsShownWithoutToggle()
    {
        // Arrange - with no passkey option there is nothing to switch from, so the fields show straight away
        IStringLocalizer<SignIn> localizer = Services.GetRequiredService<IStringLocalizer<SignIn>>();
        IRenderedComponent<SignIn> component = await RenderAndWaitForFormAsync();

        // Assert
        Assert.NotEmpty(component.FindAll("input[type='text']"));
        Assert.NotEmpty(component.FindAll("input[type='password']"));
        var labels = component.FindAll("button").Select(b => b.TextContent.Trim()).ToList();
        Assert.DoesNotContain(localizer["UseUserNameAndPasswordButton"], labels);
    }

    private static IElement FindButton(IRenderedComponent<SignIn> component, string label)
    {
        return component.FindAll("button").First(b => b.TextContent.Trim() == label);
    }

    private void SetupPasswordLogin(bool mustChangePassword, bool hasPasskey)
    {
        _ = WebAuthn.Setup(w => w.SignInWithPasswordAsync(Username, Password, It.IsAny<bool>()))
            .ReturnsAsync((true, null, "session-token", Guid.NewGuid(), "ReadOnly", mustChangePassword, hasPasskey, false, null, Array.Empty<string>()));
    }

    private void SetupTwoFactorRequired(string correlationId)
    {
        _ = WebAuthn.Setup(w => w.SignInWithPasswordAsync(Username, Password, It.IsAny<bool>()))
            .ReturnsAsync((true, null, null, default, string.Empty, false, false, true, correlationId, new[] { "passkey" }));
    }

    private async Task<IRenderedComponent<SignIn>> RenderAndWaitForFormAsync()
    {
        IRenderedComponent<SignIn> component = Render<SignIn>();
        // SignIn shows a progress bar until its first-render OnAfterRenderAsync completes, then the form.
        // Either the credential fields or the "Use User Name and Password" toggle is the first sign of the form.
        await component.WaitForStateAsync(() => component.FindAll("button").Count > 0);
        return component;
    }

    private static async Task ClickButtonAsync(IRenderedComponent<SignIn> component, string label)
    {
        IElement button = component.FindAll("button").First(b => b.TextContent.Trim() == label);
        await button.ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
    }

    private static async Task SubmitPasswordLoginAsync(IRenderedComponent<SignIn> component)
    {
        component.Find("input[type='text']").Change(Username);
        component.Find("input[type='password']").Change(Password);
        await component.Find("button.e-primary").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
    }
}
