namespace VideoForensics.Ui.Shared.Tests;

using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Moq;
using VideoForensics.Client.Common.Contracts;
using VideoForensics.Core.Providers.Common.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Ui.Shared.Pages;
using VideoForensics.Ui.Shared.Services;
using Xunit;
using ProviderAccountEntity = VideoForensics.Data.Common.Entities.ProviderAccount;

/// <summary>
/// Shared setup for the /accounts page tests: repository and auth mocks, the real
/// <see cref="AccountSelectionState"/> / <see cref="PairedSessionState"/> over the bUnit JS runtime,
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

    protected AccountsPageTestBase()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddLocalization();
        ScopeOwnershipTestDoubles.RegisterEmpty(Services);
        Services.AddSingleton<Syncfusion.Blazor.ISyncfusionStringLocalizer, Syncfusion.Blazor.SyncfusionStringLocalizer>();
        Services.AddSingleton<Syncfusion.Blazor.GlobalOptions>();
        Services.AddScoped<Syncfusion.Blazor.SyncfusionBlazorService>();

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

        AccountRepositoryMock
            .Setup(r => r.ListActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(accounts);
        UserRepositoryMock
            .Setup(r => r.GetAsync(ringUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { ProviderUserKey = "ring-key", DisplayName = "Ring Owner" });
        UserRepositoryMock
            .Setup(r => r.GetAsync(wyzeUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { ProviderUserKey = "wyze-key", DisplayName = "Wyze Owner" });

        AccountRepositoryMock
            .Setup(r => r.ListRemovedAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ProviderAccountEntity>());

        Services.AddScoped(_ => AccountRepositoryMock.Object);
        Services.AddScoped(_ => ExportServiceMock.Object);
        Services.AddScoped(_ => UserRepositoryMock.Object);
        Services.AddScoped(_ => new Mock<IProviderAuthService>().Object);
        Services.AddScoped(_ => new Mock<IMultiProviderAuthService>().Object);
        Services.AddScoped(_ => new Mock<IForensicsConfiguration>().Object);
        Services.AddScoped(_ => new Mock<IForensicsConfigurationService>().Object);
        Services.AddScoped(sp => new AccountSelectionState(sp.GetRequiredService<IJSRuntime>()));
        // Signed-in passkey session so StepUpService can fetch a step-up token for remove/retire/export.
        Services.AddScoped(sp =>
        {
            var session = new PairedSessionState(sp.GetRequiredService<IJSRuntime>());
            session.SetAsync("session-token", Guid.NewGuid(), "SuperAdmin").GetAwaiter().GetResult();
            session.SetServerSessionKind(localTier: true, passkeySession: true, passwordSession: false);
            return session;
        });
        WebAuthnMock.Setup(w => w.StepUpAsync(It.IsAny<string>())).ReturnsAsync("step-token");
        Services.AddScoped(_ => WebAuthnMock.Object);
        Services.AddScoped<StepUpService>();
        Services.AddScoped<RightPanelContentService>();
    }

    protected IRenderedComponent<Accounts> RenderPage() => Render<Accounts>();

    protected static readonly Guid RemovedAccountId = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000001");
    protected static readonly DateTime RemovedUtc = new(2026, 10, 5, 12, 0, 0, DateTimeKind.Utc);

    /// <summary>Makes ListRemoved return one retired Ring account whose owner is "Retired Owner".</summary>
    protected void SetRemovedAccount(Guid id)
    {
        var userId = Guid.NewGuid();
        AccountRepositoryMock
            .Setup(r => r.ListRemovedAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ProviderAccountEntity>
            {
                new() { Id = id, UserId = userId, ProviderName = "Ring", RemovedUtc = RemovedUtc },
            });
        UserRepositoryMock
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
            var buttons = scope is null ? cut.FindAll("button") : cut.FindAll(scope + " button");
            target = buttons.First(b => b.TextContent.Trim() == text);
        });
        target!.Click();
    }

    /// <summary>The fragment the page pushed into the right-panel slot; render it with Render(...) to drive its controls.</summary>
    protected RenderFragment RightPanelFragment()
    {
        var slot = Services.GetRequiredService<RightPanelContentService>();
        Assert.NotNull(slot.Content);
        return slot.Content!;
    }

}

public class AccountsPage_Grid_Tests : AccountsPageTestBase
{
    [Fact]
    public void Accounts_GridRendered_NoBlankLeadingHeaderColumn()
    {
        var cut = RenderPage();

        var labels = cut.FindAll(".e-headercell .e-headertext");
        Assert.NotEmpty(labels);
        Assert.Equal("Display Name", labels[0].TextContent.Trim());
    }

    [Fact]
    public void Accounts_GridRows_EachRowShowsOnlyDetailsButton()
    {
        var cut = RenderPage();

        var buttonTexts = cut.FindAll("#active-accounts .e-gridcontent tbody td button").Select(b => b.TextContent.Trim()).ToList();

        Assert.Equal(2, buttonTexts.Count(t => t == "Details"));
        Assert.Equal(2, buttonTexts.Count);
    }

    [Fact]
    public void Accounts_GridRowWithError_ErrorIconSitsInDisplayNameCell()
    {
        var cut = RenderPage();

        var wyzeRow = cut.FindAll("tbody tr").Single(r => r.TextContent.Contains("Wyze Owner"));
        var firstCell = wyzeRow.QuerySelector("td");

        Assert.NotNull(firstCell);
        Assert.NotNull(firstCell!.QuerySelector("i.material-icons"));
    }
}

public class AccountsPage_ProviderFilter_Tests : AccountsPageTestBase
{
    [Fact]
    public void Accounts_ProviderFilterDefault_AllProvidersSelected()
    {
        RenderPage();
        var panel = Render(RightPanelFragment());

        var multiSelect = panel.FindComponent<Syncfusion.Blazor.DropDowns.SfMultiSelect<string[], string>>();

        Assert.NotNull(multiSelect.Instance.Value);
        Assert.Equal(new[] { "Ring", "Wyze" }.OrderBy(x => x), multiSelect.Instance.Value!.OrderBy(x => x));
    }

    [Fact]
    public async Task Accounts_ProviderSelectionExcludesProvider_HidesThatProvidersRows()
    {
        var cut = RenderPage();
        Assert.Contains("Wyze Owner", cut.Markup);

        var panel = Render(RightPanelFragment());
        var multiSelect = panel.FindComponent<Syncfusion.Blazor.DropDowns.SfMultiSelect<string[], string>>();

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
        var strings = Services.GetRequiredService<Microsoft.Extensions.Localization.IStringLocalizer<VideoForensics.Ui.Shared.Pages.Accounts>>();
        var userId = Guid.NewGuid();
        AccountRepositoryMock
            .Setup(r => r.ListActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ProviderAccountEntity>
            {
                new() { Id = RingAccountId, UserId = userId, ProviderName = "Ring", LastErrorMessage = "Token expired" },
            });
        UserRepositoryMock
            .Setup(r => r.GetAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { ProviderUserKey = "ring-key", DisplayName = "Ring Owner" });

        var cut = RenderPage();

        var title = cut.Find("i.material-icons").GetAttribute("title");
        Assert.Contains($"(as of {strings["UnknownTime"]})", title);
    }
}

public class AccountsPage_RemovedAccounts_Tests : AccountsPageTestBase
{
    [Fact]
    public void AccountsPage_ShowsRemovedAccountsTable_FromListRemoved()
    {
        SetRemovedAccount(RemovedAccountId);

        var cut = RenderPage();

        Assert.Contains("Removed accounts", cut.Markup);
        var removed = cut.Find("#removed-accounts");
        Assert.Contains("Retired Owner", removed.TextContent);
        Assert.Contains(RemovedUtc.ToString(), removed.TextContent);
        AccountRepositoryMock.Verify(r => r.ListRemovedAsync(It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public void AccountsPage_ActiveTable_UsesListActive()
    {
        var cut = RenderPage();

        Assert.Contains("Ring Owner", cut.Find("#active-accounts").TextContent);
        AccountRepositoryMock.Verify(r => r.ListActiveAsync(It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        AccountRepositoryMock.Verify(r => r.ListAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void AccountsPage_ExportFailed_KeepsAccountInRemovedTable()
    {
        var jobId = Guid.NewGuid();
        SetRemovedAccount(RemovedAccountId);
        ExportServiceMock
            .Setup(e => e.QueueExportAsync(RemovedAccountId, false, It.IsAny<CancellationToken>(), It.IsAny<string?>()))
            .ReturnsAsync(jobId);
        ExportServiceMock
            .Setup(e => e.GetStatusAsync(jobId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExportJobStatus(jobId, RemovedAccountId, ExportJobState.Failed, null, "disk full", true, DateTime.UtcNow, DateTime.UtcNow));

        var cut = RenderPage();
        ClickButton(cut, "Delete permanently", "#removed-accounts");
        ClickButton(cut, "Export first");

        cut.WaitForAssertion(() =>
        {
            var removed = cut.Find("#removed-accounts").TextContent;
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
        ExportServiceMock
            .Setup(e => e.GetLatestStatusForAccountAsync(RemovedAccountId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExportJobStatus(jobId, RemovedAccountId, ExportJobState.Running, null, null, true, DateTime.UtcNow, null));
        ExportServiceMock
            .Setup(e => e.GetStatusAsync(jobId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExportJobStatus(jobId, RemovedAccountId, ExportJobState.Running, null, null, true, DateTime.UtcNow, null));

        var cut = RenderPage();

        cut.WaitForAssertion(() => Assert.Contains("Running", cut.Find("#removed-accounts").TextContent));
    }

    [Fact]
    public void AccountsPage_ExportJobGone_StopsPollingAndReportsLost()
    {
        // The server no longer knows the job (e.g. after a restart). The page must report it and stop, not poll forever.
        var jobId = Guid.NewGuid();
        SetRemovedAccount(RemovedAccountId);
        ExportServiceMock
            .Setup(e => e.GetLatestStatusForAccountAsync(RemovedAccountId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExportJobStatus(jobId, RemovedAccountId, ExportJobState.Running, null, null, true, DateTime.UtcNow, null));
        ExportServiceMock
            .Setup(e => e.GetStatusAsync(jobId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ExportJobStatus?)null);

        var cut = RenderPage();

        cut.WaitForAssertion(() => Assert.Contains("no longer tracked by the server", cut.Find("#removed-accounts").TextContent),
            timeout: TimeSpan.FromSeconds(15));
    }
}
