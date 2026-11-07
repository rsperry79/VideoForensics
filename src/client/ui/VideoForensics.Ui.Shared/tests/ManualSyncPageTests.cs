using AngleSharp.Dom;

using Bunit;
using Bunit.Rendering;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

using Syncfusion.Blazor.DropDowns;

using VideoForensics.Client.Ui.Shared.Pages;
using VideoForensics.Client.Ui.Shared.Services;
using VideoForensics.Client.Ui.Shared.Services.Filters;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests;

/// <summary>Tests for the Manual Sync page (/accounts/manual-sync): the sync controls and the Providers and Account right-panel filters.</summary>
public class ManualSyncPage_Tests : AccountDetailsPageTestBase
{
    private IStringLocalizer<ManualSync> Manual => Services.GetRequiredService<IStringLocalizer<ManualSync>>();

    private IRenderedComponent<ManualSync> RenderManualSync()
    {
        return Render<ManualSync>();
    }

    [Fact]
    public void ManualSync_AccountsOfSelectedProviders_HidesPickAccountHint()
    {
        IRenderedComponent<ManualSync> cut = RenderManualSync();

        cut.WaitForAssertion(() => Assert.DoesNotContain(Manual["PickAccountHint"], cut.Markup));
    }

    [Fact]
    public void ManualSync_SyncTypeConfig_HidesRangeSelector()
    {
        IRenderedComponent<ManualSync> cut = RenderManualSync();
        cut.WaitForElement("#sync-type").Change("Config");

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("#sync-range")));
    }

    [Fact]
    public void ManualSync_RangeNotCustom_HidesCustomDatePickers()
    {
        IRenderedComponent<ManualSync> cut = RenderManualSync();

        _ = cut.WaitForElement("#sync-range");
        Assert.Empty(cut.FindAll("#sync-custom-start, #sync-custom-end"));
    }

    [Fact]
    public void ManualSync_RangeCustom_ShowsDatePickersDefaultingToLast30Days()
    {
        IRenderedComponent<ManualSync> cut = RenderManualSync();
        cut.WaitForElement("#sync-range").Change("Custom");

        cut.WaitForAssertion(() => Assert.Equal(DateTime.Today.AddDays(-30).ToString("yyyy-MM-dd"), cut.Find("#sync-custom-start").GetAttribute("value")));
        Assert.Equal(DateTime.Today.ToString("yyyy-MM-dd"), cut.Find("#sync-custom-end").GetAttribute("value"));
        Assert.Equal(Manual["CustomEndHint"], cut.Find("#sync-custom-end-hint").TextContent);
    }

    [Fact]
    public void ManualSync_AllAccounts_SyncNowIsEnabledAndSyncsEveryAccountInProvidersFilter()
    {
        EditAccess.CanEdit = true;
        string path = $"/api/v1/accounts/provider-accounts/{AccountId}/sync-now";
        Handler.Bodies[path] = new { };
        IRenderedComponent<ManualSync> cut = RenderManualSync();

        IElement syncNow = null!;
        cut.WaitForAssertion(() =>
        {
            syncNow = cut.FindAll("button").First(b => b.TextContent.Contains(Manual["SyncNow"]));
            Assert.False(syncNow.HasAttribute("disabled"));
        });
        syncNow.Click();

        cut.WaitForAssertion(() => Assert.Contains(Handler.Sent, s => s.Method == HttpMethod.Post && s.Path == path));
    }

    [Fact]
    public void ManualSync_StoredSharedProviderChoiceWithNoAccounts_ShowsPickAccountHintOnLoad()
    {
        // Wyze has no account in this fixture, so a stored Wyze choice leaves no sync target.
        JSInterop.Setup<string?>("vfLayoutPrefs.get", "vf.filters.provider-account").SetResult("{\"providers\":[\"Wyze\"],\"account\":null}");
        IRenderedComponent<ManualSync> cut = RenderManualSync();

        cut.WaitForAssertion(() => Assert.Contains(Manual["PickAccountHint"], cut.Markup));
    }

    [Fact]
    public async Task ManualSync_ProviderChoiceMadeInSharedState_ChangesTheSyncTargets()
    {
        IRenderedComponent<ManualSync> cut = RenderManualSync();
        cut.WaitForAssertion(() => Assert.DoesNotContain(Manual["PickAccountHint"], cut.Markup));
        ProviderAccountFilterState shared = Services.GetRequiredService<ProviderAccountFilterState>();

        await cut.InvokeAsync(() => shared.SetAsync(new[] { "Wyze" }, null));

        cut.WaitForAssertion(() => Assert.Contains(Manual["PickAccountHint"], cut.Markup));
    }

    [Fact]
    public void ManualSync_RightPanel_HasProviderAndAccountFilters_NoProviderSelectedMeansAll()
    {
        _ = RenderManualSync();
        RightPanelContentService slot = Services.GetRequiredService<RightPanelContentService>();
        Assert.NotNull(slot.Content);

        IRenderedComponent<ContainerFragment> panel = Render(slot.Content!);

        IRenderedComponent<SfMultiSelect<string[], string>> providers = panel.FindComponent<Syncfusion.Blazor.DropDowns.SfMultiSelect<string[], string>>();
        Assert.Empty(providers.Instance.Value!);
        _ = panel.FindComponent<Syncfusion.Blazor.DropDowns.SfDropDownList<string, KeyValuePair<string, string>>>();
    }
}
