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
}

public class PageAccessRouteView_Tests : NavComponentTestBase
{
    [Route("/settings/network")]
    private sealed class LocalOnlyPage : ComponentBase
    {
        protected override void BuildRenderTree(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder)
        {
            builder.AddMarkupContent(0, "<div id=\"page-body\">network page</div>");
        }
    }

    private static RouteData RouteFor<T>() => new(typeof(T), new Dictionary<string, object?>());

    [Fact]
    public async Task PageAccessRouteView_RemoteSuperAdminOnLocalPage_ShowsNotAuthorizedAndNoPage()
    {
        await Session.SetAsync("token", Guid.NewGuid(), "SuperAdmin");
        Session.SetServerSessionKind(localTier: false, passkeySession: false);

        var component = Render<PageAccessRouteView>(p => p.Add(c => c.RouteData, RouteFor<LocalOnlyPage>()));

        Assert.Empty(component.FindAll("#page-body"));
        Assert.Contains("You do not have access to this page.", component.Markup);
    }

    [Fact]
    public async Task PageAccessRouteView_LocalSuperAdminOnLocalPage_RendersPage()
    {
        await Session.SetAsync("token", Guid.NewGuid(), "SuperAdmin");
        Session.SetServerSessionKind(localTier: true, passkeySession: false);

        var component = Render<PageAccessRouteView>(p => p.Add(c => c.RouteData, RouteFor<LocalOnlyPage>()));

        Assert.Single(component.FindAll("#page-body"));
    }
}
