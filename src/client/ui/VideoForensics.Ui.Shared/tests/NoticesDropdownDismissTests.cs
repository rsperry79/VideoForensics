
using Bunit;

using VideoForensics.Ui.Shared.Layout;
using VideoForensics.Ui.Shared.Layout.Mobile;

using Xunit;

namespace VideoForensics.Ui.Shared.Tests;
/// <summary>
/// Covers outside-click dismissal of the notices (bell) dropdown in both the desktop (MainLayout)
/// and mobile (MobileLayout) layouts. Clicking anywhere else on the page must close the dropdown.
/// </summary>
public class NoticesDropdownDismissTests : SimpleModeLayoutTestBase
{
    [Fact]
    public void MainLayout_ClickOutsideOpenNoticesDropdown_ClosesDropdown()
    {
        // Arrange
        IRenderedComponent<MainLayout> component = Render<MainLayout>();
        component.Find(".notices-toggle").Click();
        component.WaitForAssertion(() => Assert.NotEmpty(component.FindAll(".notices-dropdown")), TimeSpan.FromSeconds(10));

        // Act - click the page area outside the dropdown (the backdrop covers everything else)
        component.Find(".notices-backdrop").Click();

        // Assert
        component.WaitForAssertion(() => Assert.Empty(component.FindAll(".notices-dropdown")), TimeSpan.FromSeconds(10));
        component.WaitForAssertion(() => Assert.Empty(component.FindAll(".notices-backdrop")), TimeSpan.FromSeconds(10));
    }

    [Fact]
    public void MobileLayout_ClickOutsideOpenNoticesDropdown_ClosesDropdown()
    {
        // Arrange
        IRenderedComponent<MobileLayout> component = Render<MobileLayout>();
        component.Find(".notices-button").Click();
        Assert.NotEmpty(component.FindAll(".mobile-notices-panel"));

        // Act
        component.Find(".notices-backdrop").Click();

        // Assert
        Assert.Empty(component.FindAll(".mobile-notices-panel"));
        Assert.Empty(component.FindAll(".notices-backdrop"));
    }

    [Fact]
    public void MainLayout_DisposedWithAttachedPanelHandle_DetachesPanelResizeListener()
    {
        // Arrange - the right-panel handle attaches a JS listener that holds a DotNetObjectReference to this layout
        IRenderedComponent<MainLayout> component = Render<MainLayout>();
        component.WaitForAssertion(() => JSInterop.VerifyInvoke("vfPanelResize.attach"), TimeSpan.FromSeconds(10));

        // Act
        ((IDisposable)component.Instance).Dispose();

        // Assert - without a detach, the JS listener keeps calling the disposed reference
        _ = JSInterop.VerifyInvoke("vfPanelResize.detach");
    }

    [Fact]
    public void MainLayout_NoticesDropdownClosed_RendersNoBackdrop()
    {
        // Arrange / Act
        IRenderedComponent<MainLayout> component = Render<MainLayout>();

        // Assert - the backdrop must not intercept page clicks while the dropdown is closed
        Assert.Empty(component.FindAll(".notices-backdrop"));
    }
}
