using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Moq;
using VideoForensics.Client.Ui.Shared.Layout.RightPanel;
using VideoForensics.Client.Ui.Shared.Pages;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Client.Ui.Shared.Services;
using Xunit;
using ProviderAccountEntity = VideoForensics.Data.Common.Entities.ProviderAccount;

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
    public void ManualSync_NoAccountChosen_ShowsPickAccountHint()
    {
        IRenderedComponent<ManualSync> cut = RenderManualSync();

        cut.WaitForAssertion(() => Assert.Contains(Manual["PickAccountHint"], cut.Markup));
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
    public void ManualSync_RightPanel_HasProviderAndAccountFilters_AllProvidersSelectedByDefault()
    {
        _ = RenderManualSync();
        RightPanelContentService slot = Services.GetRequiredService<RightPanelContentService>();
        Assert.NotNull(slot.Content);

        var panel = Render(slot.Content!);

        var providers = panel.FindComponent<Syncfusion.Blazor.DropDowns.SfMultiSelect<string[], string>>();
        Assert.NotNull(providers.Instance.Value);
        Assert.Contains("Ring", providers.Instance.Value!);
        _ = panel.FindComponent<Syncfusion.Blazor.DropDowns.SfDropDownList<string, KeyValuePair<string, string>>>();
    }

    [Fact]
    public void ManualSync_EditAccessArrivesAfterFirstRender_ShowsSyncNow()
    {
        EditAccess.Answers.Enqueue(false);
        EditAccess.Answers.Enqueue(true);
        IRenderedComponent<ManualSync> cut = RenderManualSync();

        cut.WaitForAssertion(() => Assert.Contains(Manual["SyncNow"], cut.Markup));
    }
}
