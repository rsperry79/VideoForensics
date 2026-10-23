
using Bunit;

using Microsoft.Extensions.DependencyInjection;

using VideoForensics.Ui.Shared.Layout;
using VideoForensics.Ui.Shared.Services;

using Xunit;

namespace VideoForensics.Ui.Shared.Tests;
/// <summary>
/// Covers the desktop layout during a limited passkey-setup session: no navigation groups, no
/// sidebar links, no settings panel - only the sign-out control in the top bar.
/// </summary>
public class MainLayoutPasskeySetupTests : SimpleModeLayoutTestBase
{
    [Fact]
    public async Task MainLayout_PasskeySetupOnly_HidesNavigation()
    {
        // Arrange
        PairedSessionState session = Services.GetRequiredService<PairedSessionState>();
        await session.SetAsync("token", Guid.NewGuid(), "ReadOnly", passkeySetupOnly: true);

        // Act
        IRenderedComponent<MainLayout> component = Render<MainLayout>();

        // Assert
        Assert.Empty(component.FindAll(".top-tab-button"));
        Assert.Empty(component.FindAll(".left-nav-item"));
        Assert.Empty(component.FindAll(".left-nav-toggle"));
        Assert.Empty(component.FindAll(".right-panel-toggle"));
        Assert.Empty(component.FindAll(".user-menu"));
        Assert.NotEmpty(component.FindAll(".passkey-setup-signout"));
    }
}
