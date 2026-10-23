using Bunit;

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

using VideoForensics.Client.Ui.Shared.Layout.Simple;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests;

public class SimpleLayoutHomeRoutingTests : SimpleModeLayoutTestBase
{
    private static readonly RenderFragment RoutedBody = b => b.AddMarkupContent(0, "<p id=\"routed-body\">routed page</p>");

    private IRenderedComponent<SimpleLayout> RenderAt(string path)
    {
        _ = RegisterSignedInSession();
        RegisterViewportService();
        RegisterUiModeService("Simple");
        Services.GetRequiredService<NavigationManager>().NavigateTo(path);
        return Render<SimpleLayout>(p => p.Add(l => l.Body, RoutedBody));
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/media")]
    [InlineData("/Media")]
    [InlineData("/media?x=1")]
    [InlineData("/#section")]
    public void SimpleLayout_AtHomeRoutes_ShowsSimpleHomeNotBody(string path)
    {
        IRenderedComponent<SimpleLayout> component = RenderAt(path);

        _ = Assert.Single(component.FindComponents<SimpleHome>());
        Assert.Empty(component.FindAll("#routed-body"));
    }

    [Theory]
    [InlineData("/change-password")]
    [InlineData("/signin")]
    [InlineData("/chat")]
    [InlineData("/cases/42")]
    [InlineData("/settings/passkeys")]
    [InlineData("/media/extra")]
    public void SimpleLayout_AtOtherRoutes_ShowsBodyNotSimpleHome(string path)
    {
        IRenderedComponent<SimpleLayout> component = RenderAt(path);

        Assert.Empty(component.FindComponents<SimpleHome>());
        _ = Assert.Single(component.FindAll("#routed-body"));
    }

    [Fact]
    public void SimpleLayout_NavigatingFromChatToHome_SwapsToSimpleHome()
    {
        IRenderedComponent<SimpleLayout> component = RenderAt("/chat");
        Assert.Empty(component.FindComponents<SimpleHome>());

        Services.GetRequiredService<NavigationManager>().NavigateTo("/");

        component.WaitForAssertion(() =>
        {
            _ = Assert.Single(component.FindComponents<SimpleHome>());
            Assert.Empty(component.FindAll("#routed-body"));
        });
    }

    [Fact]
    public void SimpleLayout_NavigatingFromHomeToOtherRoute_SwapsToBody()
    {
        IRenderedComponent<SimpleLayout> component = RenderAt("/");

        Services.GetRequiredService<NavigationManager>().NavigateTo("/cases/42");

        component.WaitForAssertion(() =>
        {
            Assert.Empty(component.FindComponents<SimpleHome>());
            _ = Assert.Single(component.FindAll("#routed-body"));
        });
    }

    [Fact]
    public void SimpleLayout_AtHome_KeepsHeaderAndChatPanel()
    {
        IRenderedComponent<SimpleLayout> component = RenderAt("/");

        Assert.NotNull(component.Find(".simple-layout-header"));
        Assert.NotNull(component.Find("button.simple-layout-signout"));
        Assert.NotNull(component.Find(".simple-layout-chat"));
    }

    [Fact]
    public async Task SimpleLayout_AfterDispose_NavigationDoesNotThrow()
    {
        IRenderedComponent<SimpleLayout> component = RenderAt("/chat");
        NavigationManager nav = Services.GetRequiredService<NavigationManager>();

        await DisposeComponentsAsync();

        Exception? ex = Record.Exception(() => nav.NavigateTo("/"));
        Assert.Null(ex);
    }
}