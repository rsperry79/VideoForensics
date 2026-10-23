namespace VideoForensics.Ui.Shared.Tests;

using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using VideoForensics.Ui.Shared.Components;
using VideoForensics.Ui.Shared.Layout.Simple;
using VideoForensics.Ui.Shared.Services;
using Xunit;

/// <summary>
/// Covers the Simple layout during a limited passkey-setup session: the chat panel and the plain-language
/// home are hidden, while the routed passkey page still renders.
/// </summary>
public class SimpleLayoutPasskeySetupTests : SimpleModeLayoutTestBase
{
    private static readonly RenderFragment PasskeyPageBody = b => b.AddMarkupContent(0, "<p id=\"passkey-page\">passkey setup</p>");

    private async Task<PairedSessionState> StartSetupOnlySessionAsync()
    {
        RegisterSignedInSession();
        RegisterViewportService();
        RegisterUiModeService("Simple");
        var session = Services.GetRequiredService<PairedSessionState>();
        await session.SetAsync("token", Guid.NewGuid(), "ReadOnly", passkeySetupOnly: true);
        return session;
    }

    [Fact]
    public async Task SimpleLayout_PasskeySetupOnly_HidesChatPanel()
    {
        // Arrange
        await StartSetupOnlySessionAsync();
        Services.GetRequiredService<NavigationManager>().NavigateTo("/settings/passkeys");

        // Act
        var component = Render<SimpleLayout>(p => p.Add(l => l.Body, PasskeyPageBody));

        // Assert
        Assert.Empty(component.FindComponents<ChatPanel>());
        Assert.Empty(component.FindAll(".simple-layout-chat"));
        Assert.Single(component.FindAll("#passkey-page"));
    }

    [Fact]
    public async Task SimpleLayout_PasskeySetupOnly_HidesStandardViewButton()
    {
        // Arrange
        await StartSetupOnlySessionAsync();
        Services.GetRequiredService<NavigationManager>().NavigateTo("/settings/passkeys");

        // Act
        var component = Render<SimpleLayout>(p => p.Add(l => l.Body, PasskeyPageBody));

        // Assert
        Assert.Empty(component.FindAll(".simple-layout-standard-button"));
        Assert.Single(component.FindAll("#passkey-page"));
    }

    [Fact]
    public async Task SimpleLayout_PasskeySetupOnly_HidesSimpleHomeAtHomeRoute()
    {
        // Arrange
        await StartSetupOnlySessionAsync();
        Services.GetRequiredService<NavigationManager>().NavigateTo("/");

        // Act
        var component = Render<SimpleLayout>(p => p.Add(l => l.Body, PasskeyPageBody));

        // Assert
        Assert.Empty(component.FindComponents<SimpleHome>());
        Assert.Single(component.FindAll("#passkey-page"));
    }
}
