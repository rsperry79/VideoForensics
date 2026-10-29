
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

public class Media_Route_Tests
{
    private static List<string> RouteTemplates(Type type)
    {
        return type
            .GetCustomAttributes(typeof(RouteAttribute), inherit: false)
            .Cast<RouteAttribute>()
            .Select(a => a.Template)
            .ToList();
    }

    [Fact]
    public void ReviewPage_HasMediaAndMediaReviewRoutes()
    {
        // /media is the review page; /media/review stays as a second route for existing links.
        List<string> templates = RouteTemplates(typeof(ReviewPage));

        Assert.Contains("/media", templates);
        Assert.Contains("/media/review", templates);
    }

    [Fact]
    public void Root_HasRootRoute()
    {
        // "/" belongs to Root, which redirects to /media.
        Assert.Contains("/", RouteTemplates(typeof(Root)));
    }

    [Fact]
    public void NoOtherPage_DeclaresMediaRoute()
    {
        List<Type> owners = typeof(ReviewPage).Assembly
            .GetTypes()
            .Where(t => RouteTemplates(t).Contains("/media"))
            .ToList();

        Assert.Equal(new[] { typeof(ReviewPage) }, owners);
    }
}
