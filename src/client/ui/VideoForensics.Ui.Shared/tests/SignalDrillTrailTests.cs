using VideoForensics.Client.Ui.Shared.Services.Scope;
using VideoForensics.Client.Ui.Shared.Services.Signal;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests;

/// <summary>The drill-down breadcrumb: each scope a drill came from, valid only while the scope is still the drilled one.</summary>
public class SignalDrillTrailTests
{
    private static readonly DateTime Day = new(2026, 10, 24, 0, 0, 0, DateTimeKind.Utc);
    private static readonly ForensicScope Root = new([], Day.AddDays(-7), Day.AddDays(1), null);
    private static readonly ForensicScope OneCamera = new([Guid.NewGuid()], Day.AddDays(-7), Day.AddDays(1), null);
    private static readonly ForensicScope Period = new([Guid.NewGuid()], Day.AddHours(12), Day.AddHours(13), null);

    [Fact]
    public void AncestorsOf_IsEmptyBeforeAnyDrill()
    {
        var trail = new SignalDrillTrail();

        Assert.Empty(trail.AncestorsOf(Root));
    }

    [Fact]
    public void DrillInto_RecordsTheScopeWeCameFrom()
    {
        var trail = new SignalDrillTrail();

        trail.DrillInto(Root, "All cameras", OneCamera);

        IReadOnlyList<SignalDrillTrail.Crumb> ancestors = trail.AncestorsOf(OneCamera);
        SignalDrillTrail.Crumb crumb = Assert.Single(ancestors);
        Assert.Equal("All cameras", crumb.Label);
        Assert.True(crumb.Scope.Equals(Root));
    }

    [Fact]
    public void DrillInto_AccumulatesAcrossLevels()
    {
        var trail = new SignalDrillTrail();
        trail.DrillInto(Root, "All cameras", OneCamera);

        trail.DrillInto(OneCamera, "Front door", Period);

        Assert.Equal(["All cameras", "Front door"], trail.AncestorsOf(Period).Select(c => c.Label));
    }

    [Fact]
    public void AncestorsOf_IsEmptyOnceTheScopeHasChangedSinceTheDrill()
    {
        var trail = new SignalDrillTrail();
        trail.DrillInto(Root, "All cameras", OneCamera);

        Assert.Empty(trail.AncestorsOf(Period));
    }

    [Fact]
    public void RestoreTo_ReturnsTheAncestorScopeAndDropsTheLevelsBelowIt()
    {
        var trail = new SignalDrillTrail();
        trail.DrillInto(Root, "All cameras", OneCamera);
        trail.DrillInto(OneCamera, "Front door", Period);

        ForensicScope restored = trail.RestoreTo(1);

        Assert.True(restored.Equals(OneCamera));
        Assert.Equal(["All cameras"], trail.AncestorsOf(OneCamera).Select(c => c.Label));
    }

    [Fact]
    public void RestoreTo_TheRootClearsTheTrail()
    {
        var trail = new SignalDrillTrail();
        trail.DrillInto(Root, "All cameras", OneCamera);

        ForensicScope restored = trail.RestoreTo(0);

        Assert.True(restored.Equals(Root));
        Assert.Empty(trail.AncestorsOf(Root));
    }
}
