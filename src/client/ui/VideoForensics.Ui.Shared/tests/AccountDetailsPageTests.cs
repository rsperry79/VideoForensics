namespace VideoForensics.Ui.Shared.Tests;

using System.Net;
using System.Text.Json;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using Moq;
using VideoForensics.Client.Common.Contracts;
using VideoForensics.Core.Api.Contracts;
using VideoForensics.Core.Providers.Common.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Ui.Shared.Pages;
using VideoForensics.Ui.Shared.Services;
using Xunit;
using ProviderAccountEntity = VideoForensics.Data.Common.Entities.ProviderAccount;

/// <summary>
/// Stub HTTP handler for the account details page. Responds with canned JSON per absolute path,
/// 500 for paths registered as failing, and 404 for anything unexpected. Records every request URI.
/// </summary>
public sealed class StubHttpMessageHandler : HttpMessageHandler
{
    public Dictionary<string, object> Bodies { get; } = new();
    public HashSet<string> FailingPaths { get; } = new();
    public List<Uri> RequestedUris { get; } = new();

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var path = request.RequestUri!.AbsolutePath;
        RequestedUris.Add(request.RequestUri);

        if (FailingPaths.Contains(path))
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError));
        }

        if (!Bodies.TryGetValue(path, out var body))
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }

        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(body), System.Text.Encoding.UTF8, "application/json"),
        });
    }
}

/// <summary>
/// Shared setup for the /accounts/{Id} page tests: repository and session mocks, the real
/// <see cref="PairedSessionState"/> over the bUnit JS runtime, and a stub <see cref="HttpClient"/>
/// bound as the page's injected client for the sync-run and case list endpoints.
/// </summary>
public abstract class AccountDetailsPageTestBase : BunitContext
{
    protected static readonly Guid AccountId = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000001");

    protected static readonly Guid OtherAccountId = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002");

    protected StubHttpMessageHandler Handler { get; } = new();

    protected ProviderAccountEntity Account { get; }

    protected Mock<IForensicsConfiguration> ForensicsConfigMock { get; } = new();

    protected Mock<IForensicsConfigurationService> ForensicsConfigServiceMock { get; } = new();

    protected Mock<IProviderAuthService> AuthServiceMock { get; } = new();

    protected AccountDetailsPageTestBase()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddLocalization();
        Services.AddSingleton<Syncfusion.Blazor.ISyncfusionStringLocalizer, Syncfusion.Blazor.SyncfusionStringLocalizer>();
        Services.AddSingleton<Syncfusion.Blazor.GlobalOptions>();
        Services.AddScoped<Syncfusion.Blazor.SyncfusionBlazorService>();

        var userId = Guid.NewGuid();
        var account = new ProviderAccountEntity
        {
            Id = AccountId,
            UserId = userId,
            ProviderName = "Ring",
            LinkedUtc = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc),
        };
        Account = account;

        // A second linked account so the Select/Selected choice on this page is meaningful.
        var otherAccount = new ProviderAccountEntity
        {
            Id = OtherAccountId,
            UserId = Guid.NewGuid(),
            ProviderName = "Wyze",
            LinkedUtc = new DateTime(2026, 9, 2, 0, 0, 0, DateTimeKind.Utc),
        };

        var accountRepository = new Mock<IProviderAccountRepository>();
        accountRepository
            .Setup(r => r.GetAsync(AccountId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);
        accountRepository
            .Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ProviderAccountEntity> { account, otherAccount });

        var userRepository = new Mock<IUserRepository>();
        userRepository
            .Setup(r => r.GetAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { ProviderUserKey = "ring-key", DisplayName = "Ring Owner" });

        AuthServiceMock
            .Setup(a => a.RestoreFromSavedCredentialsAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        Services.AddScoped(_ => accountRepository.Object);
        Services.AddScoped(_ => userRepository.Object);
        Services.AddScoped(_ => AuthServiceMock.Object);
        Services.AddScoped(_ => ForensicsConfigMock.Object);
        Services.AddScoped(_ => ForensicsConfigServiceMock.Object);
        Services.AddScoped(sp => new AccountSelectionState(sp.GetRequiredService<IJSRuntime>()));
        Services.AddScoped(sp => new PairedSessionState(sp.GetRequiredService<IJSRuntime>()));
        Services.AddScoped(sp => new WebAuthnClient(sp.GetRequiredService<IJSRuntime>(), new Mock<ISelfApiHttpClientFactory>().Object));
        Services.AddScoped(_ => Handler);
        Services.AddScoped(_ => new HttpClient(Handler));

        SetRendererInfo(new RendererInfo("Server", true));
    }

    protected IStringLocalizer<AccountDetails> Strings => Services.GetRequiredService<IStringLocalizer<AccountDetails>>();

    protected NavigationManager Nav => Services.GetRequiredService<NavigationManager>();

    protected IRenderedComponent<AccountDetails> RenderPage() =>
        Render<AccountDetails>(p => p.Add(c => c.Id, AccountId));

    /// <summary>Absolute path (as the page requests it) for a sub-route of this account.</summary>
    protected static string ApiPath(string suffix) => $"/api/v1/accounts/provider-accounts/{AccountId}{suffix}";

    protected void SetJson(string suffix, object body) => Handler.Bodies[ApiPath(suffix)] = body;

    protected void FailJson(string suffix) => Handler.FailingPaths.Add(ApiPath(suffix));

    /// <summary>Default: both lists are empty, so tests only set up the lists they care about.</summary>
    protected void SetEmptyLists()
    {
        SetJson("/sync-runs", Array.Empty<ProviderSyncRunSummaryDto>());
        SetJson("/cases", Array.Empty<AccountCaseDto>());
    }
}

public class AccountDetailsPage_SyncHistory_Tests : AccountDetailsPageTestBase
{
    [Fact]
    public void AccountDetails_SyncRunsRendered_OneRowPerRunWithSucceededAndFailedText()
    {
        var started = new DateTime(2026, 9, 20, 9, 0, 0, DateTimeKind.Utc);
        SetJson("/sync-runs", new[]
        {
            new ProviderSyncRunSummaryDto(Guid.NewGuid(), started, started.AddMinutes(1), true),
            new ProviderSyncRunSummaryDto(Guid.NewGuid(), started.AddDays(-1), started.AddDays(-1).AddMinutes(1), false),
        });
        SetJson("/cases", Array.Empty<AccountCaseDto>());

        var cut = RenderPage();

        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll("#sync-history .sync-run-row").Count));
        var rows = cut.FindAll("#sync-history .sync-run-row").Select(r => r.TextContent).ToList();
        Assert.Contains(rows, text => text.Contains(Strings["SyncSucceeded"]));
        Assert.Contains(rows, text => text.Contains(Strings["SyncRunFailed"]));
    }

    [Fact]
    public void AccountDetails_SyncRunsEmpty_ShowsNoRunsMessage()
    {
        SetEmptyLists();

        var cut = RenderPage();

        cut.WaitForAssertion(() => Assert.Contains(Strings["SyncNoRuns"], cut.Find("#sync-history").TextContent));
        Assert.Empty(cut.FindAll("#sync-history .sync-run-row"));
    }

    [Fact]
    public void AccountDetails_SyncDetailsClicked_ShowsErrorMessageFromDetailEndpoint()
    {
        var runId = Guid.NewGuid();
        var started = new DateTime(2026, 9, 20, 9, 0, 0, DateTimeKind.Utc);
        SetJson("/sync-runs", new[] { new ProviderSyncRunSummaryDto(runId, started, started.AddMinutes(1), false) });
        SetJson($"/sync-runs/{runId}", new ProviderSyncRunDetailDto(runId, AccountId, started, started.AddMinutes(1), false, "Token expired"));
        SetJson("/cases", Array.Empty<AccountCaseDto>());

        var cut = RenderPage();
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("#sync-history .sync-run-row")));

        cut.Find("#sync-history .sync-run-row button").Click();

        cut.WaitForAssertion(() => Assert.Contains("Token expired", cut.Find("#sync-history").TextContent));
        Assert.Contains(Handler.RequestedUris, uri => uri.AbsolutePath == ApiPath($"/sync-runs/{runId}"));
    }

    [Fact]
    public void AccountDetails_SyncListFails_ShowsLoadErrorInSyncSectionOnly()
    {
        FailJson("/sync-runs");
        var caseId = Guid.NewGuid();
        SetJson("/cases", new[]
        {
            new AccountCaseDto(caseId, "CASE-1", "Porch", "Open", "Detected", null, new DateTime(2026, 9, 12, 12, 0, 0, DateTimeKind.Utc)),
        });

        var cut = RenderPage();

        cut.WaitForAssertion(() => Assert.Contains(Strings["LoadListError"], cut.Find("#sync-history").TextContent));
        Assert.Contains("Ring", cut.Find(".card-title").TextContent);
        Assert.Single(cut.FindAll("#attached-cases .case-row"));
    }
}

public class AccountDetailsPage_AttachedCases_Tests : AccountDetailsPageTestBase
{
    [Fact]
    public void AccountDetails_CasesRendered_ShowsCaseTypeEventDateAndCreatedDate()
    {
        var scope = new DateTime(2026, 9, 10, 12, 0, 0, DateTimeKind.Utc);
        var created = new DateTime(2026, 9, 12, 12, 0, 0, DateTimeKind.Utc);
        SetJson("/sync-runs", Array.Empty<ProviderSyncRunSummaryDto>());
        SetJson("/cases", new[]
        {
            new AccountCaseDto(Guid.NewGuid(), "CASE-1", "Porch", "Open", "Detected", scope, created),
        });

        var cut = RenderPage();

        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("#attached-cases .case-row")));
        var row = cut.Find("#attached-cases .case-row").TextContent;
        Assert.Contains("Detected", row);
        Assert.Contains(scope.ToLocalTime().ToString("d"), row);
        Assert.Contains(created.ToLocalTime().ToString("g"), row);
    }

    [Fact]
    public void AccountDetails_CaseWithoutScope_LeavesEventDateBlank()
    {
        var created = new DateTime(2026, 9, 12, 12, 0, 0, DateTimeKind.Utc);
        SetJson("/sync-runs", Array.Empty<ProviderSyncRunSummaryDto>());
        SetJson("/cases", new[]
        {
            new AccountCaseDto(Guid.NewGuid(), "CASE-2", "Garage", "Open", "Manual", null, created),
        });

        var cut = RenderPage();

        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("#attached-cases .case-row")));
        var eventCell = cut.Find("#attached-cases .case-row td[data-col='event-date']");
        Assert.True(string.IsNullOrWhiteSpace(eventCell.TextContent));
    }

    [Fact]
    public void AccountDetails_OpenCaseClicked_NavigatesToCaseDetailsRoute()
    {
        var caseId = Guid.NewGuid();
        SetJson("/sync-runs", Array.Empty<ProviderSyncRunSummaryDto>());
        SetJson("/cases", new[]
        {
            new AccountCaseDto(caseId, "CASE-1", "Porch", "Open", "Detected", null, new DateTime(2026, 9, 12, 12, 0, 0, DateTimeKind.Utc)),
        });

        var cut = RenderPage();
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("#attached-cases .case-row")));

        cut.Find("#attached-cases .case-row button").Click();

        Assert.EndsWith($"/cases/{caseId}", Nav.Uri);
    }

    [Fact]
    public void AccountDetails_CasesEmpty_ShowsCasesNoneMessage()
    {
        SetEmptyLists();

        var cut = RenderPage();

        cut.WaitForAssertion(() => Assert.Contains(Strings["CasesNone"], cut.Find("#attached-cases").TextContent));
        Assert.Empty(cut.FindAll("#attached-cases .case-row"));
    }
}

public class AccountDetailsPage_Selection_Tests : AccountDetailsPageTestBase
{
    [Fact]
    public void AccountDetails_AccountNotSelected_ShowsEnabledSelectButton()
    {
        SetEmptyLists();
        // The browser remembers the other account as its choice, so this account is not selected.
        JSInterop.Setup<string?>("vfLayoutPrefs.get", "vf.account.selected").SetResult(OtherAccountId.ToString());

        var cut = RenderPage();

        cut.WaitForAssertion(() => Assert.NotEmpty(FindButtons(cut, Strings["Select"])));
        var button = FindButtons(cut, Strings["Select"]).Single();
        Assert.False(button.HasAttribute("disabled"));
    }

    [Fact]
    public void AccountDetails_SelectClicked_RemembersSelectionSavesConfigAndResumesSession()
    {
        SetEmptyLists();
        JSInterop.Setup<string?>("vfLayoutPrefs.get", "vf.account.selected").SetResult(OtherAccountId.ToString());

        var cut = RenderPage();
        cut.WaitForAssertion(() => Assert.Single(FindButtons(cut, Strings["Select"])));

        FindButtons(cut, Strings["Select"]).Single().Click();

        cut.WaitForAssertion(() => Assert.Contains(Strings["SwitchedAndResumed"], cut.Markup));
        Assert.Contains(JSInterop.Invocations, i => i.Identifier == "vfLayoutPrefs.set" && i.Arguments.Contains(AccountId.ToString()));
        ForensicsConfigMock.VerifySet(c => c.ActiveProviderAccountId = AccountId, Times.Once);
        ForensicsConfigServiceMock.Verify(s => s.SaveConfigurationAsync(It.IsAny<IForensicsConfiguration>(), It.IsAny<CancellationToken>()), Times.Once);
        AuthServiceMock.Verify(a => a.RestoreFromSavedCredentialsAsync(AccountId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void AccountDetails_AccountSelected_ShowsDisabledSelectedButton()
    {
        SetEmptyLists();
        // No stored choice: the first linked account (this one) is the selection.

        var cut = RenderPage();

        cut.WaitForAssertion(() => Assert.Single(FindButtons(cut, Strings["Selected"])));
        var button = FindButtons(cut, Strings["Selected"]).Single();
        Assert.True(button.HasAttribute("disabled"));
        Assert.Empty(FindButtons(cut, Strings["Select"]));
    }

    private static List<AngleSharp.Dom.IElement> FindButtons(IRenderedComponent<AccountDetails> cut, string text) =>
        cut.FindAll("button").Where(b => b.TextContent.Trim() == text).ToList();
}

public class AccountDetailsPage_Localization_Tests : AccountDetailsPageTestBase
{
    [Fact]
    public void AccountDetails_CaseTypeDetected_ShowsLocalizedDetectedLabel()
    {
        SetJson("/sync-runs", Array.Empty<ProviderSyncRunSummaryDto>());
        SetJson("/cases", new[]
        {
            new AccountCaseDto(Guid.NewGuid(), "CASE-1", "Porch", "Open", "Detected", null, new DateTime(2026, 9, 12, 12, 0, 0, DateTimeKind.Utc)),
        });

        var cut = RenderPage();

        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("#attached-cases .case-row")));
        Assert.Contains(Strings["CaseTypeDetected"], cut.Find("#attached-cases .case-row").TextContent);
    }

    [Fact]
    public void AccountDetails_UnknownCaseType_ShowsLocalizedOtherLabel()
    {
        SetJson("/sync-runs", Array.Empty<ProviderSyncRunSummaryDto>());
        SetJson("/cases", new[]
        {
            new AccountCaseDto(Guid.NewGuid(), "CASE-3", "Shed", "Open", "SomethingNew", null, new DateTime(2026, 9, 12, 12, 0, 0, DateTimeKind.Utc)),
        });

        var cut = RenderPage();

        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("#attached-cases .case-row")));
        var row = cut.Find("#attached-cases .case-row").TextContent;
        Assert.Contains(Strings["CaseTypeOther"], row);
        Assert.DoesNotContain("SomethingNew", row);
    }

    [Fact]
    public void AccountDetails_ErrorWithoutTimestamp_ShowsLocalizedNoValuePlaceholder()
    {
        Account.LastErrorMessage = "Token expired";
        SetEmptyLists();

        var cut = RenderPage();

        cut.WaitForAssertion(() => Assert.Contains("Token expired", cut.Markup));
        Assert.Contains(Strings["NoValue"], cut.Find(".border-danger").TextContent);
    }
}
