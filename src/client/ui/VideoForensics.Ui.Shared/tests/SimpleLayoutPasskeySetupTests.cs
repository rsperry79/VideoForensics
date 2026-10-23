
using Bunit;

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

using VideoForensics.Client.Ui.Shared.Components;
using VideoForensics.Client.Ui.Shared.Layout.Simple;
using VideoForensics.Client.Ui.Shared.Services;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests;
/// <summary>
/// Covers the Simple layout during a limited passkey-setup session: the chat panel and the plain-language
/// home are hidden, while the routed passkey page still renders.
/// </summary>
public class SimpleLayoutPasskeySetupTests : SimpleModeLayoutTestBase
{
    private static readonly RenderFragment PasskeyPageBody = b => b.AddMarkupContent(0, "<p id=\"passkey-page\">passkey setup</p>");

    private async Task<PairedSessionState> StartSetupOnlySessionAsync()
    {
        _ = RegisterSignedInSession();
        RegisterViewportService();
        RegisterUiModeService("Simple");
        PairedSessionState session = Services.GetRequiredService<PairedSessionState>();
        await session.SetAsync("token", Guid.NewGuid(), "ReadOnly", passkeySetupOnly: true);
        return session;
    }

    [Fact]
    public async Task SimpleLayout_PasskeySetupOnly_HidesChatPanel()
    {
        // Arrange
        _ = await StartSetupOnlySessionAsync();
        Services.GetRequiredService<NavigationManager>().NavigateTo("/settings/passkeys");

        // Act
        IRenderedComponent<SimpleLayout> component = Render<SimpleLayout>(p => p.Add(l => l.Body, PasskeyPageBody));

        // Assert
        Assert.Empty(component.FindComponents<ChatPanel>());
        Assert.Empty(component.FindAll(".simple-layout-chat"));
        _ = Assert.Single(component.FindAll("#passkey-page"));
    }

    [Fact]
    public async Task SimpleLayout_PasskeySetupOnly_HidesStandardViewButton()
    {
        // Arrange
        _ = await StartSetupOnlySessionAsync();
        Services.GetRequiredService<NavigationManager>().NavigateTo("/settings/passkeys");

        // Act
        IRenderedComponent<SimpleLayout> component = Render<SimpleLayout>(p => p.Add(l => l.Body, PasskeyPageBody));

        // Assert
        Assert.Empty(component.FindAll(".simple-layout-standard-button"));
        _ = Assert.Single(component.FindAll("#passkey-page"));
    }

    [Fact]
    public async Task SimpleLayout_PasskeySetupOnly_HidesSimpleHomeAtHomeRoute()
    {
        // Arrange
        _ = await StartSetupOnlySessionAsync();
        Services.GetRequiredService<NavigationManager>().NavigateTo("/");

        // Act
        IRenderedComponent<SimpleLayout> component = Render<SimpleLayout>(p => p.Add(l => l.Body, PasskeyPageBody));

        // Assert
        Assert.Empty(component.FindComponents<SimpleHome>());
        _ = Assert.Single(component.FindAll("#passkey-page"));
    }
}
