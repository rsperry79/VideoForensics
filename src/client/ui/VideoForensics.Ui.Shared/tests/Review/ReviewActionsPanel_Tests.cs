using Bunit;

using Microsoft.AspNetCore.Components.Web;

using VideoForensics.Client.Ui.Shared.Components.Review;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests;

/// <summary>The right-sidebar actions panel: one full-width dropdown that lists the selected-evidence actions.</summary>
public class ReviewActionsPanel_Tests : ReviewTestContext
{
    private IRenderedComponent<ReviewActionsPanel> RenderPanel()
    {
        return Render<ReviewActionsPanel>(parameters => parameters.Add(p => p.State, ReviewState));
    }

    [Fact]
    public void ReviewActionsPanel_Rendered_ShowsOnlyAFullWidthDropdownButton()
    {
        // Act
        IRenderedComponent<ReviewActionsPanel> cut = RenderPanel();

        // Assert
        var button = cut.Find("[data-testid='review-actions-button']");
        Assert.Equal(L["ActionsMenuTitle"].Value, button.TextContent.Trim());
        Assert.Contains("w-100", button.ClassName);
        Assert.Equal("false", button.GetAttribute("aria-expanded"));
        Assert.Empty(cut.FindAll("[data-testid='review-actions-popup']"));
        Assert.Empty(cut.FindAll("[data-testid='review-grab-still']"));
    }

    [Fact]
    public void ReviewActionsPanel_ButtonClicked_OpensAPopupListingEveryAction()
    {
        // Arrange
        IRenderedComponent<ReviewActionsPanel> cut = RenderPanel();

        // Act
        cut.Find("[data-testid='review-actions-button']").Click();

        // Assert
        var dialog = cut.Find("[data-testid='review-actions-dropdown']");
        Assert.Equal("dialog", dialog.GetAttribute("role"));
        Assert.Contains("review-popup", dialog.ClassName);
        foreach (string id in new[] { "review-action-tag", "review-action-new-case", "review-action-hold", "review-action-verify", "review-action-export", "review-grab-still" })
        {
            Assert.Single(cut.FindAll($"[data-testid='{id}']"));
        }

        Assert.Equal("true", cut.Find("[data-testid='review-actions-button']").GetAttribute("aria-expanded"));
    }

    [Fact]
    public void ReviewActionsPanel_ButtonClickedTwice_ClosesThePopup()
    {
        // Arrange
        IRenderedComponent<ReviewActionsPanel> cut = RenderPanel();
        cut.Find("[data-testid='review-actions-button']").Click();

        // Act
        cut.Find("[data-testid='review-actions-button']").Click();

        // Assert
        Assert.Empty(cut.FindAll("[data-testid='review-actions-dropdown']"));
    }

    [Fact]
    public void ReviewActionsPanel_Escape_ClosesThePopup()
    {
        // Arrange
        IRenderedComponent<ReviewActionsPanel> cut = RenderPanel();
        cut.Find("[data-testid='review-actions-button']").Click();

        // Act
        cut.Find("[data-testid='review-actions-panel']").KeyDown(new KeyboardEventArgs { Key = "Escape" });

        // Assert
        Assert.Empty(cut.FindAll("[data-testid='review-actions-dropdown']"));
    }
}
