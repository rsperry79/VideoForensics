
using Bunit;

using VideoForensics.Data.Common.Entities;
using VideoForensics.Ui.Shared.Components.Scope;

using Xunit;

namespace VideoForensics.Ui.Shared.Tests;
/// <summary>The shared ScopeRail bUnit base must supply the defaults and helpers without subclass setup.</summary>
public class ScopeRailTestContext_Defaults_Tests : ScopeRailTestContext
{
    [Fact]
    public void Render_WithRegisteredDevice_ShowsDeviceName()
    {
        _ = RegisterDeviceRepository(MakeDevice("Front Door"));

        IRenderedComponent<ScopeRail> component = Render<ScopeRail>();

        Assert.Contains("Front Door", component.Markup);
    }

    [Fact]
    public void MakeDevice_CalledTwice_ReturnsDistinctIds()
    {
        Device first = MakeDevice("Front Door");
        Device second = MakeDevice("Front Door");

        Assert.NotEqual(first.Id, second.Id);
    }
}
