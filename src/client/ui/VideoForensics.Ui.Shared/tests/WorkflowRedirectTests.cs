namespace VideoForensics.Ui.Shared.Tests;

using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using VideoForensics.Ui.Shared.Pages;
using VideoForensics.Ui.Shared.Pages.Redirects;

public class WorkflowRedirect_Tests : BunitContext
{
    [Fact]
    public void Render_NavigatesToEvidence()
    {
        Render<WorkflowRedirect>();

        var nav = Services.GetRequiredService<NavigationManager>();
        Assert.EndsWith("/evidence", nav.Uri);
    }
}

public class Evidence_Route_Tests
{
    [Fact]
    public void Evidence_HasRootRoute()
    {
        var templates = typeof(Evidence)
            .GetCustomAttributes(typeof(RouteAttribute), inherit: false)
            .Cast<RouteAttribute>()
            .Select(a => a.Template)
            .ToList();

        Assert.Contains("/", templates);
        Assert.Contains("/evidence", templates);
    }
}
