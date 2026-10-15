namespace VideoForensics.Ui.Shared.Tests;

using Bunit;
using Xunit;
using VideoForensics.Ui.Shared.Layout;
using VideoForensics.Ui.Shared.Layout.Mobile;

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
        var component = Render<MainLayout>();
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
        var component = Render<MobileLayout>();
        component.Find(".notices-button").Click();
        Assert.NotEmpty(component.FindAll(".mobile-notices-panel"));

        // Act
        component.Find(".notices-backdrop").Click();

        // Assert
        Assert.Empty(component.FindAll(".mobile-notices-panel"));
        Assert.Empty(component.FindAll(".notices-backdrop"));
    }

    [Fact]
    public void MainLayout_NoticesDropdownClosed_RendersNoBackdrop()
    {
        // Arrange / Act
        var component = Render<MainLayout>();

        // Assert - the backdrop must not intercept page clicks while the dropdown is closed
        Assert.Empty(component.FindAll(".notices-backdrop"));
    }
}
