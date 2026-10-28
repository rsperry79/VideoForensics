
using Bunit;

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

using VideoForensics.Client.Ui.Shared.Pages;
using VideoForensics.Client.Ui.Shared.Pages.Redirects;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests;

public class WorkflowRedirect_Tests : BunitContext
{
    [Fact]
    public void Render_NavigatesToEvidence()
    {
        _ = Render<WorkflowRedirect>();

        NavigationManager nav = Services.GetRequiredService<NavigationManager>();
        Assert.EndsWith("/media", nav.Uri);
    }
}

public class Evidence_Route_Tests
{
    [Fact]
    public void Evidence_HasMediaRouteOnly_RootRedirectsThere()
    {
        // "/" has no page of its own: Root redirects it to /media, so Evidence keeps only the /media route.
        var templates = typeof(Evidence)
            .GetCustomAttributes(typeof(RouteAttribute), inherit: false)
            .Cast<RouteAttribute>()
            .Select(a => a.Template)
            .ToList();

        Assert.Equal(["/media"], templates);
    }

    [Fact]
    public void Root_HasRootRoute()
    {
        var templates = typeof(Root)
            .GetCustomAttributes(typeof(RouteAttribute), inherit: false)
            .Cast<RouteAttribute>()
            .Select(a => a.Template)
            .ToList();

        Assert.Equal(["/"], templates);
    }
}
