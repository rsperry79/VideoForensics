namespace VideoForensics.Ui.Shared.Tests;

using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using Moq;
using VideoForensics.Ui.Shared.Layout;
using VideoForensics.Ui.Shared.Resources;
using VideoForensics.Ui.Shared.Services;
using Xunit;

/// <summary>
/// Tests for AuthGate.razor's three-way bootstrap redirect decision logic
/// (<see cref="AuthGate.ResolveBootstrapRedirectTarget"/>).
///
/// The full component render test is impractical in bUnit due to AuthGate's OnAfterRenderAsync-only
/// redirect logic and NavigationManager event subscription setup. These tests instead call the
/// component's own public static decision method directly, so they exercise the real logic rather
/// than a duplicate of it:
///
/// - If isEmpty &amp;&amp; isLocal: redirect to /setup (local admin creates first SuperAdmin)
/// - Else if isEmpty &amp;&amp; !isLocal: redirect to /awaiting-setup (remote caller waits for local admin)
/// - Else (!isEmpty): redirect to /welcome (normal login flow, operators exist)
/// </summary>
public class AuthGateTests : BunitContext
{
    public AuthGateTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddScoped<IStringLocalizer<SharedResources>>(_ => TestLocalizer.Create());
        Services.AddScoped(_ => new Mock<WebAuthnClient>(Mock.Of<IJSRuntime>(), Mock.Of<ISelfApiHttpClientFactory>()).Object);
        Services.AddScoped(sp => new PairedSessionState(sp.GetRequiredService<IJSRuntime>()));
        SetRendererInfo(new RendererInfo("Server", true));
    }

    [Fact]
    public async Task AuthGate_PasskeySetupOnly_RedirectsOtherPagesToPasskeySetup()
    {
        // Arrange - a limited create-passkey session on a page other than the passkey page
        var session = Services.GetRequiredService<PairedSessionState>();
        await session.SetAsync("token", Guid.NewGuid(), "ReadOnly", passkeySetupOnly: true);
        var nav = Services.GetRequiredService<NavigationManager>();
        nav.NavigateTo("http://localhost/cases");

        // Act
        var component = Render<AuthGate>();

        // Assert
        component.WaitForAssertion(() => Assert.Equal("/settings/passkeys?setup=true", new Uri(nav.Uri).PathAndQuery));
    }

    [Fact]
    public async Task AuthGate_PasskeySetupOnly_LeavesPasskeyPageAlone()
    {
        // Arrange
        var session = Services.GetRequiredService<PairedSessionState>();
        await session.SetAsync("token", Guid.NewGuid(), "ReadOnly", passkeySetupOnly: true);
        var nav = Services.GetRequiredService<NavigationManager>();
        nav.NavigateTo("http://localhost/settings/passkeys?setup=true");

        // Act
        Render<AuthGate>();
        await Task.Delay(100); // negative assertion: give the first-render redirect a chance to fire

        // Assert
        Assert.Equal("/settings/passkeys?setup=true", new Uri(nav.Uri).PathAndQuery);
    }

    [Fact]
    public void ResolveBootstrapRedirectTarget_NoOperatorsAndLocal_ReturnsSetup()
    {
        string target = AuthGate.ResolveBootstrapRedirectTarget(isEmpty: true, isLocal: true);
        Assert.Equal("/setup", target);
    }

    [Fact]
    public void ResolveBootstrapRedirectTarget_NoOperatorsAndRemote_ReturnsAwaitingSetup()
    {
        string target = AuthGate.ResolveBootstrapRedirectTarget(isEmpty: true, isLocal: false);
        Assert.Equal("/awaiting-setup", target);
    }

    [Fact]
    public void ResolveBootstrapRedirectTarget_OperatorsExist_ReturnsWelcome()
    {
        string target = AuthGate.ResolveBootstrapRedirectTarget(isEmpty: false, isLocal: true);
        Assert.Equal("/welcome", target);
    }

    [Fact]
    public void ResolveBootstrapRedirectTarget_OperatorsExistRemote_ReturnsWelcome()
    {
        string target = AuthGate.ResolveBootstrapRedirectTarget(isEmpty: false, isLocal: false);
        Assert.Equal("/welcome", target);
    }
}
