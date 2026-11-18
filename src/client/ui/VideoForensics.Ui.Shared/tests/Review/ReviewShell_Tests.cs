using Bunit;

using Microsoft.AspNetCore.Components;

using VideoForensics.Client.Ui.Shared.Components.Review;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests.Review;

/// <summary>The shared review shell: header, stage, optional side column and bottom strip, used by /media and the flag pages.</summary>
public class ReviewShell_Tests : BunitContext
{
    private static RenderFragment Text(string text) => builder => builder.AddContent(0, text);

    [Fact]
    public void ReviewShell_Render_HasHeaderStageAndBottomRegions()
    {
        // Act
        IRenderedComponent<ReviewShell> cut = Render<ReviewShell>(p => p
            .Add(x => x.Header, Text("header"))
            .Add(x => x.Stage, Text("stage"))
            .Add(x => x.Bottom, Text("bottom")));

        // Assert
        Assert.Equal("header", cut.Find("[data-testid=review-shell-header]").TextContent);
        Assert.Equal("stage", cut.Find("[data-testid=review-shell-stage]").TextContent);
        Assert.Equal("bottom", cut.Find("[data-testid=review-shell-bottom]").TextContent);
    }

    [Fact]
    public void ReviewShell_NoSide_OmitsSideColumn()
    {
        // Act
        IRenderedComponent<ReviewShell> cut = Render<ReviewShell>(p => p
            .Add(x => x.Stage, Text("stage")));

        // Assert
        Assert.Empty(cut.FindAll("[data-testid=review-shell-side]"));
        Assert.Contains("review-shell--no-side", cut.Find("[data-testid=review-shell-stage]").ParentElement!.ClassList);
    }

    [Fact]
    public void ReviewShell_WithSide_RendersSideBesideTheStage()
    {
        // Act
        IRenderedComponent<ReviewShell> cut = Render<ReviewShell>(p => p
            .Add(x => x.Stage, Text("stage"))
            .Add(x => x.Side, Text("timeline")));

        // Assert
        Assert.Equal("timeline", cut.Find("[data-testid=review-shell-side]").TextContent);
        Assert.Empty(cut.FindAll(".review-shell--no-side"));
    }

    [Fact]
    public void ReviewShell_FillParameter_AddsTheFillClass()
    {
        // Act
        IRenderedComponent<ReviewShell> cut = Render<ReviewShell>(p => p
            .Add(x => x.Stage, Text("stage"))
            .Add(x => x.Fill, true));

        // Assert
        Assert.Contains("review-shell--fill", cut.Find("[data-testid=review-shell-stage]").ParentElement!.ClassList);
    }
}
