using VideoForensics.Data.Common.Entities;
using VideoForensics.Client.Ui.Shared.Layout;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests;
/// <summary>
/// Tests for <see cref="NavGroups.ActiveGroupFor"/>: picks the top tab whose matching item is the most specific,
/// so "/analyze/chain-of-custody" lands on Cases rather than Evidence's broader "/analyze" item.
/// </summary>
public class NavGroups_ActiveGroupFor_Tests
{
    private static readonly NavContext AdminCtx = new(IsSignedIn: true, Role: OperatorRole.Admin, AppLockSupported: false);

    [Theory]
    [InlineData("/analyze/chain-of-custody", "cases")]
    [InlineData("/analyze", "cases")]
    [InlineData("/cases/123", "cases")]
    [InlineData("/media", "media")]
    public void ActiveGroupFor_MostSpecificItemWins_SelectsExpectedGroup(string currentPath, string expectedKey)
    {
        NavGroup group = NavGroups.ActiveGroupFor(NavGroups.All, AdminCtx, currentPath, null);

        Assert.Equal(expectedKey, group.Key);
    }

    [Fact]
    public void ActiveGroupFor_QueryItemMatches_SelectsCases()
    {
        NavGroup group = NavGroups.ActiveGroupFor(NavGroups.All, AdminCtx, "/analyze", "?analysis=reports");

        Assert.Equal("cases", group.Key);
    }

    [Fact]
    public void ActiveGroupFor_NoItemMatches_FallsBackToFirstVisibleGroup()
    {
        var visible = NavGroups.All.Skip(1).ToList();

        NavGroup group = NavGroups.ActiveGroupFor(visible, AdminCtx, "/nowhere", null);

        Assert.Equal(visible[0].Key, group.Key);
    }

    [Fact]
    public void ActiveGroupFor_NoVisibleGroups_FallsBackToAllFirst()
    {
        NavGroup group = NavGroups.ActiveGroupFor([], AdminCtx, "/nowhere", null);

        Assert.Equal(NavGroups.All[0].Key, group.Key);
    }
}
