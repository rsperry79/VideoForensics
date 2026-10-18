namespace VideoForensics.Ui.Shared.Tests.Components;

using Bunit;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.DependencyInjection;
using VideoForensics.Ui.Shared.Components;
using VideoForensics.Ui.Shared.Resources;
using Xunit;

public class Breadcrumb_Tests : BunitContext
{
    private static readonly List<(string Label, string? Url)> Trail =
    [
        ("Sources", "/accounts"),
        ("Query API", "/query"),
        ("API Tester", null)
    ];

    public Breadcrumb_Tests()
    {
        Services.AddLocalization();
        Services.AddSingleton<IStringLocalizer<SharedResources>>(TestLocalizer.Create());
        Services.AddSingleton<Syncfusion.Blazor.ISyncfusionStringLocalizer, Syncfusion.Blazor.SyncfusionStringLocalizer>();
        Services.AddSingleton<Syncfusion.Blazor.GlobalOptions>();
        Services.AddScoped<Syncfusion.Blazor.SyncfusionBlazorService>();
    }

    [Fact]
    public void Breadcrumb_LastItem_HasAriaCurrentPageAndNoLink()
    {
        var cut = Render<Breadcrumb>(p => p.Add(c => c.Items, Trail));

        var last = cut.FindAll("ol > li").Last();
        Assert.Equal("page", last.GetAttribute("aria-current"));
        Assert.Empty(last.QuerySelectorAll("a"));
        Assert.Equal("API Tester", last.TextContent.Trim());

        foreach (var earlier in cut.FindAll("ol > li").SkipLast(1))
        {
            Assert.Null(earlier.GetAttribute("aria-current"));
        }
    }

    [Fact]
    public void Breadcrumb_NonLastItemsWithHref_RenderAsLinks()
    {
        var cut = Render<Breadcrumb>(p => p.Add(c => c.Items, Trail));

        var links = cut.FindAll("ol > li > a");
        Assert.Equal(2, links.Count);
        Assert.Equal("/accounts", links[0].GetAttribute("href"));
        Assert.Equal("/query", links[1].GetAttribute("href"));
    }

    [Fact]
    public void Breadcrumb_NullItems_RendersNoListItems()
    {
        var cut = Render<Breadcrumb>(p => p.Add(c => c.Items, null));

        Assert.Empty(cut.FindAll("li"));
    }

    [Fact]
    public void Breadcrumb_EmptyItems_RendersNoListItems()
    {
        var cut = Render<Breadcrumb>(p => p.Add(c => c.Items, new List<(string Label, string? Url)>()));

        Assert.Empty(cut.FindAll("li"));
    }
}
