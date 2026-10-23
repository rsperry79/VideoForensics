namespace VideoForensics.Ui.Shared.Tests;

using Bunit;
using Xunit;
using VideoForensics.Ui.Shared.Components.Scope;

/// <summary>The shared ScopeRail bUnit base must supply the defaults and helpers without subclass setup.</summary>
public class ScopeRailTestContext_Defaults_Tests : ScopeRailTestContext
{
    [Fact]
    public void Render_WithRegisteredDevice_ShowsDeviceName()
    {
        RegisterDeviceRepository(MakeDevice("Front Door"));

        var component = Render<ScopeRail>();

        Assert.Contains("Front Door", component.Markup);
    }

    [Fact]
    public void MakeDevice_CalledTwice_ReturnsDistinctIds()
    {
        var first = MakeDevice("Front Door");
        var second = MakeDevice("Front Door");

        Assert.NotEqual(first.Id, second.Id);
    }
}
