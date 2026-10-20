namespace VideoForensics.Ui.Shared.Tests;

using Bunit;
using Xunit;

/// <summary>
/// The account page is split into tabs: Details (account card and remove), Sync (sync controls, last-sync times,
/// history) and Cases. Each tab is its own routed page, and the tab bar links between them.
/// </summary>
public class AccountTabsTests : AccountDetailsPageTestBase
{
    [Fact]
    public void SyncTab_ShowsLastSyncCompletedTimestamp()
    {
        Account.LastSyncCompletedUtc = new DateTime(2026, 10, 19, 8, 30, 0, DateTimeKind.Utc);
        SetEmptyLists();

        var cut = RenderSync();

        cut.WaitForAssertion(() => Assert.Contains(Strings["LastSyncCompletedLabel"], cut.Markup));
        Assert.Contains(Account.LastSyncCompletedUtc.Value.ToString(), cut.Markup);
    }

    [Fact]
    public void SyncTab_NeverSynced_ShowsNeverForLastSyncCompleted()
    {
        Account.LastSyncCompletedUtc = null;
        SetEmptyLists();

        var cut = RenderSync();

        cut.WaitForAssertion(() => Assert.Contains(Strings["LastSyncCompletedLabel"], cut.Markup));
        Assert.Contains(Strings["Never"], cut.Markup);
    }

    [Fact]
    public void DetailsTab_DoesNotRenderSyncHistory()
    {
        SetEmptyLists();

        var cut = RenderPage();

        Assert.Empty(cut.FindAll("#sync-history"));
        Assert.Empty(cut.FindAll("#attached-cases"));
    }

    [Theory]
    [InlineData("/accounts/{Id:guid}/sync")]
    [InlineData("/accounts/{Id:guid}/cases")]
    public void AccountTabRoutes_AreSuperAdminLocalLikeDetails(string route)
    {
        Assert.True(VideoForensics.Ui.Shared.Layout.PageAccessRules.TryGetPolicy(route, out var policy));
        Assert.Equal(VideoForensics.Ui.Shared.Layout.AccessPolicy.SuperAdminLocal, policy);
    }

    [Fact]
    public void DetailsTab_RendersLinksToCasesAndSyncTabs()
    {
        SetEmptyLists();

        var cut = RenderPage();

        var hrefs = cut.FindAll("nav a").Select(a => a.GetAttribute("href")).ToList();
        Assert.Contains($"/accounts/{AccountId}/cases", hrefs);
        Assert.Contains($"/accounts/{AccountId}/sync", hrefs);
    }
}
