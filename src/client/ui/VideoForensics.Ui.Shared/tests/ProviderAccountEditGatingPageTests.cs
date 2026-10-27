using Bunit;

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

using VideoForensics.Client.Ui.Shared.Pages;
using VideoForensics.Core.Api.Contracts;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests;

/// <summary>Button text on a rendered page, trimmed, so assertions read the same way a user sees them.</summary>
internal static class GatingPageHelpers
{
    public static List<string> ButtonLabels<T>(IRenderedComponent<T> cut) where T : IComponent
    {
        return cut.FindAll("button").Select(b => b.TextContent.Trim()).ToList();
    }
}

public class AccountDetailsPage_EditAccess_Tests : AccountDetailsPageTestBase
{
    [Fact]
    public void AccountDetails_EditDenied_HidesReauthenticateAndRemoveAccount()
    {
        EditAccess.CanEdit = false;

        IRenderedComponent<AccountDetails> cut = RenderPage();

        cut.WaitForAssertion(() => Assert.Contains(Strings["ValidateCredential"], GatingPageHelpers.ButtonLabels(cut)));
        Assert.DoesNotContain(Strings["Reauthenticate"], GatingPageHelpers.ButtonLabels(cut));
        Assert.DoesNotContain(Strings["RemoveAccount"], GatingPageHelpers.ButtonLabels(cut));
    }

    [Fact]
    public void AccountDetails_EditAllowed_ShowsReauthenticateAndRemoveAccount()
    {
        EditAccess.CanEdit = true;

        IRenderedComponent<AccountDetails> cut = RenderPage();

        cut.WaitForAssertion(() => Assert.Contains(Strings["Reauthenticate"], GatingPageHelpers.ButtonLabels(cut)));
        Assert.Contains(Strings["RemoveAccount"], GatingPageHelpers.ButtonLabels(cut));
    }
}

public class AccountsPage_EditAccess_Tests : AccountsPageTestBase
{
    [Fact]
    public void Accounts_EditDenied_HidesAddAccount()
    {
        EditAccess.CanEdit = false;

        IRenderedComponent<Accounts> cut = RenderPage();

        // Wait for the grid so the check runs after the page has finished loading.
        cut.WaitForAssertion(() => Assert.Contains(Services.GetRequiredService<IStringLocalizer<Accounts>>()["Details"], GatingPageHelpers.ButtonLabels(cut)));
        Assert.DoesNotContain(Services.GetRequiredService<IStringLocalizer<Accounts>>()["AddAccount"], GatingPageHelpers.ButtonLabels(cut));
    }

    [Fact]
    public void Accounts_EditAllowed_ShowsAddAccount()
    {
        EditAccess.CanEdit = true;

        IRenderedComponent<Accounts> cut = RenderPage();

        cut.WaitForAssertion(() => Assert.Contains(Services.GetRequiredService<IStringLocalizer<Accounts>>()["AddAccount"], GatingPageHelpers.ButtonLabels(cut)));
    }
}

public class AccountSyncSchedulePage_EditAccess_Tests : AccountSyncSchedulePageTestBase
{
    private const string ConfigIntervalInputId = "#config-sync-interval-hours";

    [Fact]
    public void AccountSyncSchedule_EditDenied_ConfigSyncIntervalIsReadOnly()
    {
        EditAccess.CanEdit = false;
        SetJson("/config-sync-interval", new ConfigSyncIntervalDto(24));

        IRenderedComponent<AccountSyncSchedule> cut = RenderPage();

        cut.WaitForAssertion(() => Assert.Equal("24", cut.WaitForElement(ConfigIntervalInputId).GetAttribute("value")));
        Assert.True(cut.Find(ConfigIntervalInputId).HasAttribute("disabled"));
    }

    [Fact]
    public void AccountSyncSchedule_EditAllowed_ConfigSyncIntervalIsEditable()
    {
        EditAccess.CanEdit = true;
        SetJson("/config-sync-interval", new ConfigSyncIntervalDto(24));

        IRenderedComponent<AccountSyncSchedule> cut = RenderPage();

        cut.WaitForAssertion(() => Assert.Equal("24", cut.WaitForElement(ConfigIntervalInputId).GetAttribute("value")));
        Assert.False(cut.Find(ConfigIntervalInputId).HasAttribute("disabled"));
    }
}
