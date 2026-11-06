
using AngleSharp.Dom;

using Bunit;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

using VideoForensics.Client.Ui.Shared.Components;
using VideoForensics.Client.Ui.Shared.Resources;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests.Components;

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
        _ = Services.AddLocalization();
        _ = Services.AddSingleton<IStringLocalizer<SharedResources>>(TestLocalizer.Create());
        _ = Services.AddSingleton<Syncfusion.Blazor.ISyncfusionStringLocalizer, Syncfusion.Blazor.SyncfusionStringLocalizer>();
        _ = Services.AddSingleton<Syncfusion.Blazor.GlobalOptions>();
        _ = Services.AddScoped<Syncfusion.Blazor.SyncfusionBlazorService>();
    }

    [Fact]
    public void Breadcrumb_LastItem_HasAriaCurrentPageAndNoLink()
    {
        IRenderedComponent<Breadcrumb> cut = Render<Breadcrumb>(p => p.Add(c => c.Items, Trail));

        IElement last = cut.FindAll("ol > li").Last();
        Assert.Equal("page", last.GetAttribute("aria-current"));
        Assert.Empty(last.QuerySelectorAll("a"));
        Assert.Equal("API Tester", last.TextContent.Trim());

        foreach (IElement? earlier in cut.FindAll("ol > li").SkipLast(1))
        {
            Assert.Null(earlier.GetAttribute("aria-current"));
        }
    }

    [Fact]
    public void Breadcrumb_NonLastItemsWithHref_RenderAsLinks()
    {
        IRenderedComponent<Breadcrumb> cut = Render<Breadcrumb>(p => p.Add(c => c.Items, Trail));

        IReadOnlyList<IElement> links = cut.FindAll("ol > li > a");
        Assert.Equal(2, links.Count);
        Assert.Equal("/accounts", links[0].GetAttribute("href"));
        Assert.Equal("/query", links[1].GetAttribute("href"));
    }

    [Fact]
    public void Breadcrumb_Colors_UseThemeTokensNotHardCodedHex()
    {
        IRenderedComponent<Breadcrumb> cut = Render<Breadcrumb>(p => p.Add(c => c.Items, Trail));

        // Hard-coded hex text colors vanish on the dark theme; Bootstrap text tokens follow data-bs-theme.
        string current = cut.FindAll("ol > li").Last().GetAttribute("style") ?? string.Empty;
        string link = cut.FindAll("ol > li > a").First().GetAttribute("style") ?? string.Empty;

        Assert.Contains("var(--bs-emphasis-color)", current);
        Assert.DoesNotContain("#333", current);
        Assert.Contains("var(--bs-link-color)", link);
        Assert.DoesNotContain("#0066cc", link);
    }

    [Fact]
    public void Breadcrumb_NullItems_RendersNoListItems()
    {
        IRenderedComponent<Breadcrumb> cut = Render<Breadcrumb>(p => p.Add(c => c.Items, null));

        Assert.Empty(cut.FindAll("li"));
    }

    [Fact]
    public void Breadcrumb_EmptyItems_RendersNoListItems()
    {
        IRenderedComponent<Breadcrumb> cut = Render<Breadcrumb>(p => p.Add(c => c.Items, []));

        Assert.Empty(cut.FindAll("li"));
    }
}
