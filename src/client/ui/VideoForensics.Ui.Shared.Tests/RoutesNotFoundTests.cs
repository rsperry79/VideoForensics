namespace VideoForensics.Ui.Shared.Tests;

using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

/// <summary>
/// Verifies the Router's NotFound fragment in <see cref="Routes"/> renders its alert text through the localizer.
/// Uses the layout test base so the ResponsiveLayout/MainLayout service graph the NotFound fragment renders into is registered.
/// </summary>
public class Routes_NotFound_Tests : SimpleModeLayoutTestBase
{
    [Fact]
    public void Routes_UnknownPath_ShowsLocalizedNotFoundMessage()
    {
        RegisterViewportService();
        RegisterUiModeService("Standard");
        var expected = TestLocalizer.Create()["NotFoundMessage"].Value;

        // Navigate before rendering so the Router's initial match is the unknown path (NotFound),
        // not the default "/" route, which would instantiate AuthGate and its service graph.
        Services.GetRequiredService<NavigationManager>().NavigateTo("/this-route-does-not-exist");

        var cut = Render<Routes>();

        IElement? alert = null;
        cut.WaitForAssertion(() => alert = cut.Find("p[role='alert']"));

        Assert.Equal(expected, alert!.TextContent.Trim());
    }
}
