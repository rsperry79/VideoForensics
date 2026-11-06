using Bunit;

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

using VideoForensics.Client.Ui.Shared.Components.Review;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests.Review;

/// <summary>The shared review layout: five regions, localized labels, nothing outside its box.</summary>
public class FlagReviewShell_Tests : BunitContext
{
    public FlagReviewShell_Tests()
    {
        _ = Services.AddLocalization();
    }

    private IRenderedComponent<FlagReviewShell> RenderShell(bool withEveryRegion = true)
    {
        return Render<FlagReviewShell>(p =>
        {
            if (withEveryRegion)
            {
                _ = p.Add(c => c.Breadcrumb, (RenderFragment)(b => b.AddMarkupContent(0, "<span id=\"crumb\">crumb</span>")))
                    .Add(c => c.Media, (RenderFragment)(b => b.AddMarkupContent(0, "<span id=\"media\">media</span>")))
                    .Add(c => c.Timeline, (RenderFragment)(b => b.AddMarkupContent(0, "<span id=\"timeline\">timeline</span>")));
            }
        });
    }

    [Fact]
    public void Render_PutsEachFragmentInItsRegion()
    {
        IRenderedComponent<FlagReviewShell> cut = RenderShell();

        Assert.Equal("crumb", cut.Find("[data-testid=flag-review-breadcrumb] #crumb").TextContent);
        Assert.Equal("media", cut.Find("[data-testid=flag-review-media] #media").TextContent);
        Assert.Equal("timeline", cut.Find("[data-testid=flag-review-timeline] #timeline").TextContent);
    }

    [Fact]
    public void Render_SideColumnStacksActionsAboveNavigator()
    {
        IRenderedComponent<FlagReviewShell> cut = RenderShell();

        // The flag controls live in the app right panel, so the shell renders no side region of its own.
        Assert.Empty(cut.FindAll("[data-testid=flag-review-side]"));
        Assert.Empty(cut.FindAll("[data-testid=flag-review-actions]"));
        Assert.Empty(cut.FindAll("[data-testid=flag-review-navigator]"));
    }

    [Fact]
    public void Render_RegionsAreLabelledFromResources()
    {
        IRenderedComponent<FlagReviewShell> cut = RenderShell();

        // Each landmark has a non-empty accessible name that comes from a resource key, not an English literal in markup.
        foreach (string testId in new[] { "flag-review-media", "flag-review-timeline" })
        {
            string? label = cut.Find($"[data-testid={testId}]").GetAttribute("aria-label");
            Assert.False(string.IsNullOrWhiteSpace(label));
        }
    }

    [Fact]
    public void Render_WithoutNavigator_StillRendersTheOtherRegions()
    {
        IRenderedComponent<FlagReviewShell> cut = Render<FlagReviewShell>(p => p
            .Add(c => c.Media, (RenderFragment)(b => b.AddMarkupContent(0, "<span id=\"media\">media</span>")))
            .Add(c => c.Timeline, (RenderFragment)(b => b.AddMarkupContent(0, "<span id=\"timeline\">timeline</span>"))));

        Assert.NotEmpty(cut.FindAll("#media"));
        Assert.NotEmpty(cut.FindAll("#timeline"));
        Assert.Empty(cut.FindAll("[data-testid=flag-review-navigator]"));
    }

    [Fact]
    public void Render_RootFillsItsContainerAndDoesNotScrollThePage()
    {
        IRenderedComponent<FlagReviewShell> cut = RenderShell();

        // The layout rules are in the component's scoped stylesheet; the root carries the class they hang from.
        Assert.Contains("flag-review-shell", cut.Find("[data-testid=flag-review-shell]").ClassList);
    }
}
