namespace VideoForensics.Ui.Shared.Tests;

using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Moq;
using VideoForensics.Client.Common.Contracts;
using Xunit;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Ui.Shared.Layout;
using VideoForensics.Ui.Shared.Services;

/// <summary>Shared Syncfusion + session setup for components that render SfButton and read PairedSessionState.</summary>
public abstract class NavComponentTestBase : BunitContext
{
    protected NavComponentTestBase()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddLocalization();
        Services.AddSingleton<Syncfusion.Blazor.ISyncfusionStringLocalizer, Syncfusion.Blazor.SyncfusionStringLocalizer>();
        Services.AddSingleton<Syncfusion.Blazor.GlobalOptions>();
        Services.AddScoped<Syncfusion.Blazor.SyncfusionBlazorService>();
        Services.AddScoped(sp => new PairedSessionState(sp.GetRequiredService<IJSRuntime>()));
        // Default mock: app lock unsupported, so App Lock stays hidden.
        Services.AddSingleton(new Mock<IAppLockPreferencesStore>().Object);
    }

    protected PairedSessionState Session => Services.GetRequiredService<PairedSessionState>();
}

public class NavSectionList_Tests : NavComponentTestBase
{
    private static IReadOnlyList<NavSection> TwoSections() =>
    [
        new("General", [new("Settings", "/settings"), new("Notifications", "/settings/notifications")]),
        new("Security", [new("Operators", "/settings/operators"), new("Security Audit Log", "/settings/security-log")])
    ];

    [Fact]
    public void NavSectionList_SectionContainingCurrentPage_IsExpandedOnRender()
    {
        var component = Render<NavSectionList>(p => p
            .Add(c => c.Sections, TwoSections())
            .Add(c => c.CurrentPath, "/settings/operators")
            .Add(c => c.CurrentQuery, ""));

        Assert.Single(component.FindAll("[data-path='/settings/operators']"));
        Assert.Empty(component.FindAll("[data-path='/settings']"));
    }

    [Fact]
    public void NavSectionList_ClickingCollapsedHeader_ExpandsThatSectionAndCollapsesOthers()
    {
        var component = Render<NavSectionList>(p => p
            .Add(c => c.Sections, TwoSections())
            .Add(c => c.CurrentPath, "/settings")
            .Add(c => c.CurrentQuery, ""));

        Assert.NotEmpty(component.FindAll("[data-path='/settings/notifications']"));
        Assert.Empty(component.FindAll("[data-path='/settings/operators']"));

        component.Find(".left-nav-section[data-section='Security'] .left-nav-section-header").Click();

        Assert.NotEmpty(component.FindAll("[data-path='/settings/operators']"));
        Assert.Empty(component.FindAll("[data-path='/settings/notifications']"));
    }

    [Fact]
    public void NavSectionList_ManualCollapse_IsKeptWhenSectionsRerenderUnchanged()
    {
        var component = Render<NavSectionList>(p => p
            .Add(c => c.Sections, TwoSections())
            .Add(c => c.CurrentPath, "/settings/operators")
            .Add(c => c.CurrentQuery, ""));

        // The operator collapses the active section; an unrelated re-render must not reopen it.
        component.Find(".left-nav-section[data-section='Security'] .left-nav-section-header").Click();
        component.Render(p => p
            .Add(c => c.Sections, TwoSections())
            .Add(c => c.CurrentPath, "/settings/operators")
            .Add(c => c.CurrentQuery, ""));

        Assert.Empty(component.FindAll("[data-path='/settings/operators']"));
    }

    [Fact]
    public void NavSectionList_SectionsArriveAfterFirstRender_RevealsActiveSection()
    {
        // Role and tier load after the first render, so the sections start empty and arrive later.
        var component = Render<NavSectionList>(p => p
            .Add(c => c.Sections, new List<NavSection>())
            .Add(c => c.CurrentPath, "/settings/operators")
            .Add(c => c.CurrentQuery, ""));

        component.Render(p => p
            .Add(c => c.Sections, TwoSections())
            .Add(c => c.CurrentPath, "/settings/operators")
            .Add(c => c.CurrentQuery, ""));

        Assert.Single(component.FindAll("[data-path='/settings/operators']"));
    }

    [Fact]
    public void NavSectionList_UntitledSection_ItemsAlwaysRendered()
    {
        IReadOnlyList<NavSection> sections =
        [
            new(null, [new("Evidence", "/evidence")]),
            new("Security", [new("Operators", "/settings/operators")])
        ];

        var component = Render<NavSectionList>(p => p
            .Add(c => c.Sections, sections)
            .Add(c => c.CurrentPath, "/evidence")
            .Add(c => c.CurrentQuery, ""));

        Assert.Single(component.FindAll("[data-path='/evidence']"));
    }

    [Fact]
    public void NavSectionList_ItemClicked_RaisesCallbackWithItem()
    {
        NavItem? selected = null;
        var component = Render<NavSectionList>(p => p
            .Add(c => c.Sections, TwoSections())
            .Add(c => c.CurrentPath, "/settings")
            .Add(c => c.CurrentQuery, "")
            .Add(c => c.OnItemSelected, EventCallback.Factory.Create<NavItem>(this, item => selected = item)));

        component.Find("[data-path='/settings/notifications'] button").Click();

        Assert.Equal("/settings/notifications", selected?.Path);
    }

    [Fact]
    public void NavSectionList_ClickingCollapsedHeader_NavigatesToFirstItemOfSection()
    {
        NavItem? selected = null;
        var component = Render<NavSectionList>(p => p
            .Add(c => c.Sections, TwoSections())
            .Add(c => c.CurrentPath, "/settings")
            .Add(c => c.CurrentQuery, "")
            .Add(c => c.OnItemSelected, EventCallback.Factory.Create<NavItem>(this, item => selected = item)));

        component.Find(".left-nav-section[data-section='Security'] .left-nav-section-header").Click();

        Assert.Equal("/settings/operators", selected?.Path);
    }

    [Fact]
    public void NavSectionList_ClickingHeaderOfActiveSection_CollapsesWithoutNavigating()
    {
        NavItem? selected = null;
        var component = Render<NavSectionList>(p => p
            .Add(c => c.Sections, TwoSections())
            .Add(c => c.CurrentPath, "/settings/notifications")
            .Add(c => c.CurrentQuery, "")
            .Add(c => c.OnItemSelected, EventCallback.Factory.Create<NavItem>(this, item => selected = item)));

        component.Find(".left-nav-section[data-section='General'] .left-nav-section-header").Click();

        Assert.Null(selected);
        Assert.Empty(component.FindAll("[data-path='/settings/notifications']"));
    }

    [Fact]
    public void NavSectionList_Header_IsButtonWithHeaderClassAndAriaExpanded()
    {
        var component = Render<NavSectionList>(p => p
            .Add(c => c.Sections, TwoSections())
            .Add(c => c.CurrentPath, "/settings")
            .Add(c => c.CurrentQuery, ""));

        var open = component.Find(".left-nav-section[data-section='General'] button.left-nav-section-header");
        var closed = component.Find(".left-nav-section[data-section='Security'] button.left-nav-section-header");

        Assert.Equal("true", open.GetAttribute("aria-expanded"));
        Assert.Equal("false", closed.GetAttribute("aria-expanded"));
    }

    [Fact]
    public void NavSectionList_Items_KeepItemStyleAndNotHeaderStyle()
    {
        var component = Render<NavSectionList>(p => p
            .Add(c => c.Sections, TwoSections())
            .Add(c => c.CurrentPath, "/settings")
            .Add(c => c.CurrentQuery, ""));

        var item = component.Find("[data-path='/settings'] button");

        Assert.Contains("left-nav-item", item.ClassName);
        Assert.DoesNotContain("left-nav-section-header", item.ClassName);
    }
}
