
using Bunit;

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

using VideoForensics.Client.Ui.Shared.Pages.Redirects;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests;
/// <summary>
/// The retired Events page's old route now redirects to the review page at /media, preserving the
/// global forensic scope's query keys (devices/from/to/q - see ScopeState) so a bookmarked or
/// shared /events link with a scope still lands on the same data.
/// </summary>
public class EventsRedirect_Tests : BunitContext
{
    [Fact]
    public void Render_NoQueryString_RedirectsToMediaWithoutQuery()
    {
        NavigationManager nav = Services.GetRequiredService<NavigationManager>();
        nav.NavigateTo("/events", replace: true);

        _ = Render<EventsRedirect>();

        Assert.Equal("http://localhost/media", nav.Uri);
    }

    [Fact]
    public void Render_WithScopeQueryKeys_PreservesThemAndDropsView()
    {
        NavigationManager nav = Services.GetRequiredService<NavigationManager>();
        nav.NavigateTo("/events?devices=11111111-1111-1111-1111-111111111111&from=2026-01-01&to=2026-01-08&q=motion", replace: true);

        _ = Render<EventsRedirect>();

        string uri = nav.Uri;
        Assert.Contains("devices=11111111-1111-1111-1111-111111111111", uri);
        Assert.Contains("from=2026-01-01", uri);
        Assert.Contains("to=2026-01-08", uri);
        Assert.Contains("q=motion", uri);
        Assert.DoesNotContain("view=", uri);
        Assert.StartsWith("http://localhost/media?", uri);
    }

    [Fact]
    public void Render_WithUnrelatedQueryKeys_DropsThem()
    {
        NavigationManager nav = Services.GetRequiredService<NavigationManager>();
        nav.NavigateTo("/events?somethingElse=1&from=2026-01-01", replace: true);

        _ = Render<EventsRedirect>();

        Assert.DoesNotContain("somethingElse", nav.Uri);
        Assert.Contains("from=2026-01-01", nav.Uri);
    }

    [Fact]
    public void Render_WithExistingViewParam_IsDropped()
    {
        NavigationManager nav = Services.GetRequiredService<NavigationManager>();
        nav.NavigateTo("/events?view=timeline", replace: true);

        _ = Render<EventsRedirect>();

        Assert.Equal("http://localhost/media", nav.Uri);
    }
}

public class Events_Route_Tests
{
    [Fact]
    public void EventsRedirect_HasEventsRoute()
    {
        var templates = typeof(EventsRedirect)
            .GetCustomAttributes(typeof(RouteAttribute), inherit: false)
            .Cast<RouteAttribute>()
            .Select(a => a.Template)
            .ToList();

        Assert.Contains("/events", templates);
    }
}
