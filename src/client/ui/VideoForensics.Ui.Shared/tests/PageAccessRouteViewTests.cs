namespace VideoForensics.Ui.Shared.Tests;

using Bunit;
using Microsoft.AspNetCore.Components;
using VideoForensics.Ui.Shared.Layout;
using Xunit;

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

    // Two @page templates: the open alias must not let a Local-only page be reached through a second route.
    [Route("/settings/network")]
    [Route("/open/alias")]
    private sealed class AliasedLocalOnlyPage : ComponentBase
    {
        protected override void BuildRenderTree(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder)
        {
            builder.AddMarkupContent(0, "<div id=\"page-body\">aliased page</div>");
        }
    }

    [Route("/chat")]
    private sealed class ChatPage : ComponentBase
    {
        protected override void BuildRenderTree(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder)
        {
            builder.AddMarkupContent(0, "<div id=\"page-body\">chat page</div>");
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

    [Fact]
    public async Task PageAccessRouteView_AliasRouteOnLocalPage_RemoteSuperAdmin_Denied()
    {
        await Session.SetAsync("token", Guid.NewGuid(), "SuperAdmin");
        Session.SetServerSessionKind(localTier: false, passkeySession: false);

        var component = Render<PageAccessRouteView>(p => p.Add(c => c.RouteData, RouteFor<AliasedLocalOnlyPage>()));

        Assert.Empty(component.FindAll("#page-body"));
    }

    [Fact]
    public void PageAccessRouteView_SignedOut_RendersNothing()
    {
        // No SetAsync: the visitor is signed out. AuthGate redirects them; the guard must show no page body.
        var component = Render<PageAccessRouteView>(p => p.Add(c => c.RouteData, RouteFor<ChatPage>()));

        Assert.Empty(component.FindAll("#page-body"));
        Assert.DoesNotContain("You do not have access to this page.", component.Markup);
    }

    [Route("/signin")]
    private sealed class SignInStubPage : ComponentBase
    {
        protected override void BuildRenderTree(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder)
        {
            builder.AddMarkupContent(0, "<div id=\"page-body\">sign-in page</div>");
        }
    }

    [Fact]
    public void PageAccessRouteView_SignedOutOnPublicRoute_RendersPage()
    {
        // Sign-in, register and setup are reachable while signed out (AuthGate allow-list); the guard must not blank them.
        var component = Render<PageAccessRouteView>(p => p.Add(c => c.RouteData, RouteFor<SignInStubPage>()));

        Assert.Single(component.FindAll("#page-body"));
    }

    [Fact]
    public async Task PageAccessRouteView_SignedInButServerTierUnknown_FailsClosedOnLocalPage()
    {
        // No server client on this host, so the guard marks the tier known with the most restrictive answer.
        await Session.SetAsync("token", Guid.NewGuid(), "SuperAdmin");

        var component = Render<PageAccessRouteView>(p => p.Add(c => c.RouteData, RouteFor<LocalOnlyPage>()));

        component.WaitForAssertion(() => Assert.Contains("You do not have access to this page.", component.Markup));
        Assert.Empty(component.FindAll("#page-body"));
        Assert.True(Session.ServerKindKnown);
    }

    [Fact]
    public async Task PageAccessRouteView_SignInAfterRender_ReevaluatesGuard()
    {
        // A guard rendered while signed out must reveal the page once a session is set, without a navigation.
        var component = Render<PageAccessRouteView>(p => p.Add(c => c.RouteData, RouteFor<ChatPage>()));
        Assert.Empty(component.FindAll("#page-body"));

        await Session.SetAsync("token", Guid.NewGuid(), "ReadOnly");
        Session.SetServerSessionKind(localTier: false, passkeySession: false);

        component.WaitForAssertion(() => Assert.Single(component.FindAll("#page-body")));
    }

    [Fact]
    public async Task PageAccessRouteView_ClearedSession_ResetsTierAndKnownState()
    {
        await Session.SetAsync("token", Guid.NewGuid(), "SuperAdmin");
        Session.SetServerSessionKind(localTier: true, passkeySession: true);

        await Session.ClearAsync();

        Assert.False(Session.IsLocalTier);
        Assert.False(Session.IsPasskeySession);
        Assert.False(Session.ServerKindKnown);
    }
}
