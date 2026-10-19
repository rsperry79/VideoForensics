namespace VideoForensics.Ui.Shared.Tests;

using Xunit;
using VideoForensics.Ui.Shared.Services.Scope;

/// <summary>A filter that matches no device must stay "no devices", never widen to "all devices".</summary>
public class ScopeState_NoMatch_Tests
{
    private static readonly DateTime FixedNow = new(2026, 9, 24, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Set_NoMatchFlagWithNoDevices_ExcludesAllDevicesSemantics()
    {
        var state = new ScopeState();

        state.Set(state.Current with { MatchesNoDevices = true });

        Assert.True(state.Current.MatchesNoDevices);
        Assert.False(state.Current.IncludesAllDevices);
    }

    [Fact]
    public void Set_NoMatchFlagWithListedDevice_ClearsFlag()
    {
        var state = new ScopeState();
        var device = Guid.NewGuid();

        state.Set(state.Current with { DeviceIds = new[] { device }, MatchesNoDevices = true });

        Assert.False(state.Current.MatchesNoDevices);
        Assert.Equal(new[] { device }, state.Current.DeviceIds);
    }

    [Fact]
    public void ToQueryString_NoMatch_WritesDevicesNone()
    {
        var state = new ScopeState();
        state.Set(state.Current with { MatchesNoDevices = true });

        Assert.Contains("devices=none", state.ToQueryString());
    }

    [Fact]
    public void ApplyQueryString_DevicesNone_RestoresNoMatchInsteadOfAllDevices()
    {
        var state = new ScopeState();

        state.ApplyQueryString("?devices=none");

        Assert.True(state.Current.MatchesNoDevices);
        Assert.False(state.Current.IncludesAllDevices);
    }

    [Fact]
    public void ApplyQueryString_WithoutDevicesKey_ClearsNoMatch()
    {
        var state = new ScopeState();
        state.ApplyQueryString("devices=none");

        state.ApplyQueryString("from=2026-09-01");

        Assert.False(state.Current.MatchesNoDevices);
        Assert.True(state.Current.IncludesAllDevices);
    }

    [Fact]
    public void Default_IsNotNoMatch_AndIncludesAllDevices()
    {
        var scope = ForensicScope.Default(FixedNow);

        Assert.False(scope.MatchesNoDevices);
        Assert.True(scope.IncludesAllDevices);
    }
}
