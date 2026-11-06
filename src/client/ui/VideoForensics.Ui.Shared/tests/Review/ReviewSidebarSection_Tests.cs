using Bunit;

using Microsoft.AspNetCore.Components;

using VideoForensics.Client.Ui.Shared.Components.Review;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests;

/// <summary>A right-sidebar section: a heading button that collapses and expands its content.</summary>
public class ReviewSidebarSection_Tests : ReviewTestContext
{
    private IRenderedComponent<ReviewSidebarSection> RenderSection()
    {
        return Render<ReviewSidebarSection>(parameters => parameters
            .Add(p => p.Title, "Filters")
            .Add(p => p.ChildContent, (RenderFragment)(builder => builder.AddMarkupContent(0, "<p data-testid='body'>Body</p>"))));
    }

    [Fact]
    public void ReviewSidebarSection_Rendered_IsOpenWithItsTitleAndContent()
    {
        // Arrange and Act
        IRenderedComponent<ReviewSidebarSection> cut = RenderSection();

        // Assert
        Assert.Equal("Filters", cut.Find("button[aria-expanded] .fw-semibold").TextContent);
        Assert.Equal("true", cut.Find("button[aria-expanded]").GetAttribute("aria-expanded"));
        Assert.Single(cut.FindAll("[data-testid='body']"));
    }

    [Fact]
    public void ReviewSidebarSection_HeadingClicked_CollapsesTheContent()
    {
        // Arrange
        IRenderedComponent<ReviewSidebarSection> cut = RenderSection();

        // Act
        cut.Find("button[aria-expanded]").Click();

        // Assert
        Assert.Equal("false", cut.Find("button[aria-expanded]").GetAttribute("aria-expanded"));
        Assert.Empty(cut.FindAll("[data-testid='body']"));
    }

    [Fact]
    public void ReviewSidebarSection_HeadingClickedTwice_ExpandsTheContentAgain()
    {
        // Arrange
        IRenderedComponent<ReviewSidebarSection> cut = RenderSection();

        // Act
        cut.Find("button[aria-expanded]").Click();
        cut.Find("button[aria-expanded]").Click();

        // Assert
        Assert.Single(cut.FindAll("[data-testid='body']"));
    }
}
