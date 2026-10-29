
using AngleSharp.Dom;

using Bunit;
using Bunit.Rendering;

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;

using Moq;

using Syncfusion.Blazor.DropDowns;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Core.Providers.Common.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Client.Ui.Shared.Contracts;
using VideoForensics.Client.Ui.Shared.Pages;
using VideoForensics.Client.Ui.Shared.Services;

using Xunit;

using ProviderAccountEntity = VideoForensics.Data.Common.Entities.ProviderAccount;

namespace VideoForensics.Client.Ui.Shared.Tests;
/// <summary>
/// Shared setup for the /accounts page tests: repository and auth mocks, the real
/// <see cref="PairedSessionState"/> over the bUnit JS runtime,
/// and two linked accounts on different providers (Ring, Wyze). The grid is the real SfGrid; the
/// provider filter lives in the right-panel slot, so tests render the captured
/// <see cref="RightPanelContentService.Content"/> fragment to reach it.
/// </summary>
public abstract class AccountsPageTestBase : BunitContext
{
    protected Mock<IProviderAccountRepository> AccountRepositoryMock { get; } = new();
    protected Mock<IUserRepository> UserRepositoryMock { get; } = new();
    protected Mock<IAccountExportService> ExportServiceMock { get; } = new();
    protected Mock<WebAuthnClient> WebAuthnMock { get; } = new(Mock.Of<IJSRuntime>(), Mock.Of<ISelfApiHttpClientFactory>());

    protected static readonly Guid RingAccountId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    protected static readonly Guid WyzeAccountId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000002");

    protected ProviderAccountEditAccessDouble EditAccess { get; } = new();

    protected AccountsPageTestBase()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        _ = Services.AddSingleton<IProviderAccountEditAccess>(EditAccess);
        _ = Services.AddLocalization();
        ScopeOwnershipTestDoubles.RegisterEmpty(Services);
        _ = Services.AddSingleton<Syncfusion.Blazor.ISyncfusionStringLocalizer, Syncfusion.Blazor.SyncfusionStringLocalizer>();
        _ = Services.AddSingleton<Syncfusion.Blazor.GlobalOptions>();
        _ = Services.AddScoped<Syncfusion.Blazor.SyncfusionBlazorService>();

        var ringUserId = Guid.NewGuid();
        var wyzeUserId = Guid.NewGuid();
        var accounts = new List<ProviderAccountEntity>
        {
            new() { Id = RingAccountId, UserId = ringUserId, ProviderName = "Ring", LinkedUtc = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc) },
            new()
            {
                Id = WyzeAccountId,
                UserId = wyzeUserId,
                ProviderName = "Wyze",
                LinkedUtc = new DateTime(2026, 9, 2, 0, 0, 0, DateTimeKind.Utc),
                LastErrorMessage = "Token expired",
                LastErrorUtc = new DateTime(2026, 9, 3, 0, 0, 0, DateTimeKind.Utc),
            },
        };

        _ = AccountRepositoryMock
            .Setup(r => r.ListActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(accounts);
        _ = UserRepositoryMock
            .Setup(r => r.GetAsync(ringUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { ProviderUserKey = "ring-key", DisplayName = "Ring Owner" });
        _ = UserRepositoryMock
            .Setup(r => r.GetAsync(wyzeUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { ProviderUserKey = "wyze-key", DisplayName = "Wyze Owner" });

        _ = AccountRepositoryMock
            .Setup(r => r.ListRemovedAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        _ = Services.AddScoped(_ => AccountRepositoryMock.Object);
        _ = Services.AddScoped(_ => ExportServiceMock.Object);
        _ = Services.AddScoped(_ => UserRepositoryMock.Object);
        _ = Services.AddScoped(_ => new Mock<IProviderAuthService>().Object);
        _ = Services.AddScoped(_ => new Mock<IMultiProviderAuthService>().Object);
        _ = Services.AddScoped(_ => new Mock<IForensicsConfiguration>().Object);
        _ = Services.AddScoped(_ => new Mock<IForensicsConfigurationService>().Object);
        // Signed-in passkey session so StepUpService can fetch a step-up token for remove/retire/export.
        _ = Services.AddScoped(sp =>
        {
            var session = new PairedSessionState(sp.GetRequiredService<IJSRuntime>());
            session.SetAsync("session-token", Guid.NewGuid(), "SuperAdmin").GetAwaiter().GetResult();
            session.SetServerSessionKind(localTier: true, passkeySession: true, passwordSession: false);
            return session;
        });
        _ = WebAuthnMock.Setup(w => w.StepUpAsync(It.IsAny<string>())).ReturnsAsync("step-token");
        _ = Services.AddScoped(_ => WebAuthnMock.Object);
        _ = Services.AddScoped<StepUpService>();
        _ = Services.AddScoped<RightPanelContentService>();
        _ = Services.AddScoped<AccountLinkedBus>();
        _ = Services.AddScoped<IAccountLinkedSource>(sp => sp.GetRequiredService<AccountLinkedBus>());
    }

    protected IRenderedComponent<Accounts> RenderPage()
    {
        return Render<Accounts>();
    }

    protected static readonly Guid RemovedAccountId = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000001");
    protected static readonly DateTime RemovedUtc = new(2026, 10, 5, 12, 0, 0, DateTimeKind.Utc);

    /// <summary>Makes ListRemoved return one retired Ring account whose owner is "Retired Owner".</summary>
    protected void SetRemovedAccount(Guid id)
    {
        var userId = Guid.NewGuid();
        _ = AccountRepositoryMock
            .Setup(r => r.ListRemovedAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new() { Id = id, UserId = userId, ProviderName = "Ring", RemovedUtc = RemovedUtc },
            ]);
        _ = UserRepositoryMock
            .Setup(r => r.GetAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { ProviderUserKey = "retired-key", DisplayName = "Retired Owner" });
    }

    /// <summary>
    /// Clicks the first button with exactly this text inside the CSS scope (whole page when null). Waits for the
    /// button to render first: dialogs such as the retire confirmation render after the click that opens them, and
    /// on slower runners the button can appear after the first lookup.
    /// </summary>
    protected static void ClickButton(IRenderedComponent<Accounts> cut, string text, string? scope = null)
    {
        IElement? target = null;
        cut.WaitForAssertion(() =>
        {
            IReadOnlyList<IElement> buttons = scope is null ? cut.FindAll("button") : cut.FindAll(scope + " button");
            target = buttons.First(b => b.TextContent.Trim() == text);
        });
        target!.Click();
    }

    /// <summary>The fragment the page pushed into the right-panel slot; render it with Render(...) to drive its controls.</summary>
    protected RenderFragment RightPanelFragment()
    {
        RightPanelContentService slot = Services.GetRequiredService<RightPanelContentService>();
        Assert.NotNull(slot.Content);
        return slot.Content!;
    }
}

public class AccountsPage_Grid_Tests : AccountsPageTestBase
{
    [Fact]
    public void Accounts_GridRendered_NoBlankLeadingHeaderColumn()
    {
        IRenderedComponent<Accounts> cut = RenderPage();

        IReadOnlyList<IElement> labels = cut.FindAll(".e-headercell .e-headertext");
        Assert.NotEmpty(labels);
        Assert.Equal("Display Name", labels[0].TextContent.Trim());
    }

    [Fact]
    public void Accounts_GridRows_HaveNoButtons()
    {
        // A row click opens the account, so the grid carries no per-row Details button.
        IRenderedComponent<Accounts> cut = RenderPage();

        var buttonTexts = cut.FindAll("#active-accounts .e-gridcontent tbody td button").Select(b => b.TextContent.Trim()).ToList();

        Assert.Empty(buttonTexts);
    }

    [Fact]
    public void Accounts_GridRowWithError_ErrorIconSitsInDisplayNameCell()
    {
        IRenderedComponent<Accounts> cut = RenderPage();

        IElement wyzeRow = cut.FindAll("tbody tr").Single(r => r.TextContent.Contains("Wyze Owner"));
        IElement? firstCell = wyzeRow.QuerySelector("td");

        Assert.NotNull(firstCell);
        Assert.NotNull(firstCell!.QuerySelector("i.material-icons"));
    }
}

public class AccountsPage_ProviderFilter_Tests : AccountsPageTestBase
{
    [Fact]
    public void Accounts_ProviderFilterDefault_AllProvidersSelected()
    {
        _ = RenderPage();
        IRenderedComponent<ContainerFragment> panel = Render(RightPanelFragment());

        IRenderedComponent<SfMultiSelect<string[], string>> multiSelect = panel.FindComponent<Syncfusion.Blazor.DropDowns.SfMultiSelect<string[], string>>();

        Assert.NotNull(multiSelect.Instance.Value);
        Assert.Equal(new[] { "Ring", "Wyze" }.OrderBy(x => x), multiSelect.Instance.Value!.OrderBy(x => x));
    }

    [Fact]
    public async Task Accounts_ProviderSelectionExcludesProvider_HidesThatProvidersRows()
    {
        IRenderedComponent<Accounts> cut = RenderPage();
        Assert.Contains("Wyze Owner", cut.Markup);

        IRenderedComponent<ContainerFragment> panel = Render(RightPanelFragment());
        IRenderedComponent<SfMultiSelect<string[], string>> multiSelect = panel.FindComponent<Syncfusion.Blazor.DropDowns.SfMultiSelect<string[], string>>();

        await panel.InvokeAsync(() => multiSelect.Instance.ValueChanged.InvokeAsync(new[] { "Ring" }));

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Ring Owner", cut.Markup);
            Assert.DoesNotContain("Wyze Owner", cut.Markup);
        });
    }
}

public class AccountsPage_ErrorTooltip_Tests : AccountsPageTestBase
{
    [Fact]
    public void Accounts_ErrorWithoutTimestamp_TooltipUsesLocalizedUnknownTime()
    {
        IStringLocalizer<Accounts> strings = Services.GetRequiredService<Microsoft.Extensions.Localization.IStringLocalizer<VideoForensics.Client.Ui.Shared.Pages.Accounts>>();
        var userId = Guid.NewGuid();
        _ = AccountRepositoryMock
            .Setup(r => r.ListActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new() { Id = RingAccountId, UserId = userId, ProviderName = "Ring", LastErrorMessage = "Token expired" },
            ]);
        _ = UserRepositoryMock
            .Setup(r => r.GetAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { ProviderUserKey = "ring-key", DisplayName = "Ring Owner" });

        IRenderedComponent<Accounts> cut = RenderPage();

        string? title = cut.Find("i.material-icons").GetAttribute("title");
        Assert.Contains($"(as of {strings["UnknownTime"]})", title);
    }
}

public class AccountsPage_RemovedAccounts_Tests : AccountsPageTestBase
{
    [Fact]
    public void AccountsPage_ShowsRemovedAccountsTable_FromListRemoved()
    {
        SetRemovedAccount(RemovedAccountId);

        IRenderedComponent<Accounts> cut = RenderPage();

        Assert.Contains("Removed accounts", cut.Markup);
        IElement removed = cut.Find("#removed-accounts");
        Assert.Contains("Retired Owner", removed.TextContent);
        // Rendered in the machine zone here: no user zone is cascaded in this test.
        Assert.Contains(RemovedUtc.ToLocalTime().ToString(), removed.TextContent);
        AccountRepositoryMock.Verify(r => r.ListRemovedAsync(It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public void AccountsPage_ActiveTable_UsesListActive()
    {
        IRenderedComponent<Accounts> cut = RenderPage();

        Assert.Contains("Ring Owner", cut.Find("#active-accounts").TextContent);
        AccountRepositoryMock.Verify(r => r.ListActiveAsync(It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        AccountRepositoryMock.Verify(r => r.ListAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void AccountsPage_ExportFailed_KeepsAccountInRemovedTable()
    {
        var jobId = Guid.NewGuid();
        SetRemovedAccount(RemovedAccountId);
        _ = ExportServiceMock
            .Setup(e => e.QueueExportAsync(RemovedAccountId, false, It.IsAny<CancellationToken>(), It.IsAny<string?>()))
            .ReturnsAsync(jobId);
        _ = ExportServiceMock
            .Setup(e => e.GetStatusAsync(jobId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExportJobStatus(jobId, RemovedAccountId, ExportJobState.Failed, null, "disk full", true, DateTime.UtcNow, DateTime.UtcNow));

        IRenderedComponent<Accounts> cut = RenderPage();
        ClickButton(cut, "Delete permanently", "#removed-accounts");
        ClickButton(cut, "Export first");

        cut.WaitForAssertion(() =>
        {
            string removed = cut.Find("#removed-accounts").TextContent;
            Assert.Contains("Retired Owner", removed);
            Assert.Contains("disk full", removed);
        });
        AccountRepositoryMock.Verify(r => r.DeleteAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>(), It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public void AccountsPage_OnLoad_ShowsServerExportStatusForRemovedAccount()
    {
        var jobId = Guid.NewGuid();
        SetRemovedAccount(RemovedAccountId);
        _ = ExportServiceMock
            .Setup(e => e.GetLatestStatusForAccountAsync(RemovedAccountId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExportJobStatus(jobId, RemovedAccountId, ExportJobState.Running, null, null, true, DateTime.UtcNow, null));
        _ = ExportServiceMock
            .Setup(e => e.GetStatusAsync(jobId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExportJobStatus(jobId, RemovedAccountId, ExportJobState.Running, null, null, true, DateTime.UtcNow, null));

        IRenderedComponent<Accounts> cut = RenderPage();

        cut.WaitForAssertion(() => Assert.Contains("Running", cut.Find("#removed-accounts").TextContent));
    }

    [Fact]
    public void AccountsPage_ExportJobGone_StopsPollingAndReportsLost()
    {
        // The server no longer knows the job (e.g. after a restart). The page must report it and stop, not poll forever.
        var jobId = Guid.NewGuid();
        SetRemovedAccount(RemovedAccountId);
        _ = ExportServiceMock
            .Setup(e => e.GetLatestStatusForAccountAsync(RemovedAccountId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExportJobStatus(jobId, RemovedAccountId, ExportJobState.Running, null, null, true, DateTime.UtcNow, null));
        _ = ExportServiceMock
            .Setup(e => e.GetStatusAsync(jobId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ExportJobStatus?)null);

        IRenderedComponent<Accounts> cut = RenderPage();

        cut.WaitForAssertion(() => Assert.Contains("no longer tracked by the server", cut.Find("#removed-accounts").TextContent),
            timeout: TimeSpan.FromSeconds(15));
    }
}
