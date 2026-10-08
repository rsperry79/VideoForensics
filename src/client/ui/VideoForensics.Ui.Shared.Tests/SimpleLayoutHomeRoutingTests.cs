using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using VideoForensics.Ui.Shared.Layout.Simple;

namespace VideoForensics.Ui.Shared.Tests;

public class SimpleLayoutHomeRoutingTests : SimpleModeLayoutTestBase
{
    private static readonly RenderFragment RoutedBody = b => b.AddMarkupContent(0, "<p id=\"routed-body\">routed page</p>");

    private IRenderedComponent<SimpleLayout> RenderAt(string path)
    {
        RegisterSignedInSession();
        RegisterViewportService();
        RegisterUiModeService("Simple");
        Services.GetRequiredService<NavigationManager>().NavigateTo(path);
        return Render<SimpleLayout>(p => p.Add(l => l.Body, RoutedBody));
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/evidence")]
    [InlineData("/Evidence")]
    [InlineData("/evidence?x=1")]
    [InlineData("/#section")]
    public void SimpleLayout_AtHomeRoutes_ShowsSimpleHomeNotBody(string path)
    {
        var component = RenderAt(path);

        Assert.Single(component.FindComponents<SimpleHome>());
        Assert.Empty(component.FindAll("#routed-body"));
    }

    [Theory]
    [InlineData("/change-password")]
    [InlineData("/signin")]
    [InlineData("/chat")]
    [InlineData("/cases/42")]
    [InlineData("/settings/passkeys")]
    [InlineData("/evidence/extra")]
    public void SimpleLayout_AtOtherRoutes_ShowsBodyNotSimpleHome(string path)
    {
        var component = RenderAt(path);

        Assert.Empty(component.FindComponents<SimpleHome>());
        Assert.Single(component.FindAll("#routed-body"));
    }

    [Fact]
    public void SimpleLayout_NavigatingFromChatToHome_SwapsToSimpleHome()
    {
        var component = RenderAt("/chat");
        Assert.Empty(component.FindComponents<SimpleHome>());

        Services.GetRequiredService<NavigationManager>().NavigateTo("/");

        component.WaitForAssertion(() =>
        {
            Assert.Single(component.FindComponents<SimpleHome>());
            Assert.Empty(component.FindAll("#routed-body"));
        });
    }

    [Fact]
    public void SimpleLayout_NavigatingFromHomeToOtherRoute_SwapsToBody()
    {
        var component = RenderAt("/");

        Services.GetRequiredService<NavigationManager>().NavigateTo("/cases/42");

        component.WaitForAssertion(() =>
        {
            Assert.Empty(component.FindComponents<SimpleHome>());
            Assert.Single(component.FindAll("#routed-body"));
        });
    }

    [Fact]
    public void SimpleLayout_AtHome_KeepsHeaderAndChatPanel()
    {
        var component = RenderAt("/");

        Assert.NotNull(component.Find(".simple-layout-header"));
        Assert.NotNull(component.Find("button.simple-layout-signout"));
        Assert.NotNull(component.Find(".simple-layout-chat"));
    }

    [Fact]
    public async Task SimpleLayout_AfterDispose_NavigationDoesNotThrow()
    {
        var component = RenderAt("/chat");
        var nav = Services.GetRequiredService<NavigationManager>();

        await DisposeComponentsAsync();

        var ex = Record.Exception(() => nav.NavigateTo("/"));
        Assert.Null(ex);
    }
}