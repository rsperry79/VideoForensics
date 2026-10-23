
using AngleSharp.Dom;

using Bunit;

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

using Moq;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Client.Ui.Shared.Layout;
using VideoForensics.Client.Ui.Shared.Services;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests;
/// <summary>Shared Syncfusion + session setup for components that render SfButton and read PairedSessionState.</summary>
public abstract class NavComponentTestBase : BunitContext
{
    protected NavComponentTestBase()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        _ = Services.AddLocalization();
        _ = Services.AddSingleton<Syncfusion.Blazor.ISyncfusionStringLocalizer, Syncfusion.Blazor.SyncfusionStringLocalizer>();
        _ = Services.AddSingleton<Syncfusion.Blazor.GlobalOptions>();
        _ = Services.AddScoped<Syncfusion.Blazor.SyncfusionBlazorService>();
        _ = Services.AddScoped(sp => new PairedSessionState(sp.GetRequiredService<IJSRuntime>()));
        // Default mock: app lock unsupported, so App Lock stays hidden.
        _ = Services.AddSingleton(new Mock<IAppLockPreferencesStore>().Object);
    }

    protected PairedSessionState Session => Services.GetRequiredService<PairedSessionState>();
}

public class NavSectionList_Tests : NavComponentTestBase
{
    private static IReadOnlyList<NavSection> TwoSections()
    {
        return [
        new("General", [new("Settings", "/settings"), new("Notifications", "/settings/notifications")]),
        new("Security", [new("Operators", "/settings/operators"), new("Security Audit Log", "/settings/security-log")])
    ];
    }

    [Fact]
    public void NavSectionList_SectionContainingCurrentPage_IsExpandedOnRender()
    {
        IRenderedComponent<NavSectionList> component = Render<NavSectionList>(p => p
            .Add(c => c.Sections, TwoSections())
            .Add(c => c.CurrentPath, "/settings/operators")
            .Add(c => c.CurrentQuery, string.Empty));

        _ = Assert.Single(component.FindAll("[data-path='/settings/operators']"));
        Assert.Empty(component.FindAll("[data-path='/settings']"));
    }

    [Fact]
    public void NavSectionList_ClickingCollapsedHeader_ExpandsThatSectionAndCollapsesOthers()
    {
        IRenderedComponent<NavSectionList> component = Render<NavSectionList>(p => p
            .Add(c => c.Sections, TwoSections())
            .Add(c => c.CurrentPath, "/settings")
            .Add(c => c.CurrentQuery, string.Empty));

        Assert.NotEmpty(component.FindAll("[data-path='/settings/notifications']"));
        Assert.Empty(component.FindAll("[data-path='/settings/operators']"));

        component.Find(".left-nav-section[data-section='Security'] .left-nav-section-header").Click();

        Assert.NotEmpty(component.FindAll("[data-path='/settings/operators']"));
        Assert.Empty(component.FindAll("[data-path='/settings/notifications']"));
    }

    [Fact]
    public void NavSectionList_ManualCollapse_IsKeptWhenSectionsRerenderUnchanged()
    {
        IRenderedComponent<NavSectionList> component = Render<NavSectionList>(p => p
            .Add(c => c.Sections, TwoSections())
            .Add(c => c.CurrentPath, "/settings/operators")
            .Add(c => c.CurrentQuery, string.Empty));

        // The operator collapses the active section; an unrelated re-render must not reopen it.
        component.Find(".left-nav-section[data-section='Security'] .left-nav-section-header").Click();
        component.Render(p => p
            .Add(c => c.Sections, TwoSections())
            .Add(c => c.CurrentPath, "/settings/operators")
            .Add(c => c.CurrentQuery, string.Empty));

        Assert.Empty(component.FindAll("[data-path='/settings/operators']"));
    }

    [Fact]
    public void NavSectionList_SectionsArriveAfterFirstRender_RevealsActiveSection()
    {
        // Role and tier load after the first render, so the sections start empty and arrive later.
        IRenderedComponent<NavSectionList> component = Render<NavSectionList>(p => p
            .Add(c => c.Sections, [])
            .Add(c => c.CurrentPath, "/settings/operators")
            .Add(c => c.CurrentQuery, string.Empty));

        component.Render(p => p
            .Add(c => c.Sections, TwoSections())
            .Add(c => c.CurrentPath, "/settings/operators")
            .Add(c => c.CurrentQuery, string.Empty));

        _ = Assert.Single(component.FindAll("[data-path='/settings/operators']"));
    }

    [Fact]
    public void NavSectionList_UntitledSection_ItemsAlwaysRendered()
    {
        IReadOnlyList<NavSection> sections =
        [
            new(null, [new("Media", "/media")]),
            new("Security", [new("Operators", "/settings/operators")])
        ];

        IRenderedComponent<NavSectionList> component = Render<NavSectionList>(p => p
            .Add(c => c.Sections, sections)
            .Add(c => c.CurrentPath, "/media")
            .Add(c => c.CurrentQuery, string.Empty));

        _ = Assert.Single(component.FindAll("[data-path='/media']"));
    }

    [Fact]
    public void NavSectionList_ItemClicked_RaisesCallbackWithItem()
    {
        NavItem? selected = null;
        IRenderedComponent<NavSectionList> component = Render<NavSectionList>(p => p
            .Add(c => c.Sections, TwoSections())
            .Add(c => c.CurrentPath, "/settings")
            .Add(c => c.CurrentQuery, string.Empty)
            .Add(c => c.OnItemSelected, EventCallback.Factory.Create<NavItem>(this, item => selected = item)));

        component.Find("[data-path='/settings/notifications'] button").Click();

        Assert.Equal("/settings/notifications", selected?.Path);
    }

    [Fact]
    public void NavSectionList_ClickingCollapsedHeader_NavigatesToFirstItemOfSection()
    {
        NavItem? selected = null;
        IRenderedComponent<NavSectionList> component = Render<NavSectionList>(p => p
            .Add(c => c.Sections, TwoSections())
            .Add(c => c.CurrentPath, "/settings")
            .Add(c => c.CurrentQuery, string.Empty)
            .Add(c => c.OnItemSelected, EventCallback.Factory.Create<NavItem>(this, item => selected = item)));

        component.Find(".left-nav-section[data-section='Security'] .left-nav-section-header").Click();

        Assert.Equal("/settings/operators", selected?.Path);
    }

    [Fact]
    public void NavSectionList_ClickingHeaderOfActiveSection_CollapsesWithoutNavigating()
    {
        NavItem? selected = null;
        IRenderedComponent<NavSectionList> component = Render<NavSectionList>(p => p
            .Add(c => c.Sections, TwoSections())
            .Add(c => c.CurrentPath, "/settings/notifications")
            .Add(c => c.CurrentQuery, string.Empty)
            .Add(c => c.OnItemSelected, EventCallback.Factory.Create<NavItem>(this, item => selected = item)));

        component.Find(".left-nav-section[data-section='General'] .left-nav-section-header").Click();

        Assert.Null(selected);
        Assert.Empty(component.FindAll("[data-path='/settings/notifications']"));
    }

    [Fact]
    public void NavSectionList_Header_IsButtonWithHeaderClassAndAriaExpanded()
    {
        IRenderedComponent<NavSectionList> component = Render<NavSectionList>(p => p
            .Add(c => c.Sections, TwoSections())
            .Add(c => c.CurrentPath, "/settings")
            .Add(c => c.CurrentQuery, string.Empty));

        IElement open = component.Find(".left-nav-section[data-section='General'] button.left-nav-section-header");
        IElement closed = component.Find(".left-nav-section[data-section='Security'] button.left-nav-section-header");

        Assert.Equal("true", open.GetAttribute("aria-expanded"));
        Assert.Equal("false", closed.GetAttribute("aria-expanded"));
    }

    [Fact]
    public void NavSectionList_Items_KeepItemStyleAndNotHeaderStyle()
    {
        IRenderedComponent<NavSectionList> component = Render<NavSectionList>(p => p
            .Add(c => c.Sections, TwoSections())
            .Add(c => c.CurrentPath, "/settings")
            .Add(c => c.CurrentQuery, string.Empty));

        IElement item = component.Find("[data-path='/settings'] button");

        Assert.Contains("left-nav-item", item.ClassName);
        Assert.DoesNotContain("left-nav-section-header", item.ClassName);
    }
}
