using Bunit;

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

using VideoForensics.Client.Ui.Shared.Pages.Redirects;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests;

/// <summary>
/// The API Tester now lives on the Query API page. Both of its old routes redirect there, so bookmarks and
/// links keep working and the tester's access rule is the Query page's rule.
/// </summary>
public class ApiTesterRedirect_Tests : BunitContext
{
    [Theory]
    [InlineData("/query/api-tester")]
    [InlineData("/tools/ring-selftest")]
    public void Render_OldTesterRoute_RedirectsToQueryPage(string oldPath)
    {
        NavigationManager nav = Services.GetRequiredService<NavigationManager>();
        nav.NavigateTo(oldPath, replace: true);

        _ = Render<ApiTesterRedirect>();

        Assert.EndsWith("/query", nav.Uri);
    }
}
