
using Bunit;

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;

using Moq;

using System.Net;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text.Json;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Core.Api.Contracts;
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
/// Stub HTTP handler for the account details page. Responds with canned JSON per absolute path,
/// 500 for paths registered as failing, and 404 for anything unexpected. Records every request URI.
/// </summary>
public sealed class StubHttpMessageHandler : HttpMessageHandler
{
    public Dictionary<string, object> Bodies { get; } = [];
    public HashSet<string> FailingPaths { get; } = [];
    public List<Uri> RequestedUris { get; } = [];

    /// <summary>Every request in arrival order, with its method, absolute path and JSON body (null when none).</summary>
    public List<(HttpMethod Method, string Path, string? Body)> Sent { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        string path = request.RequestUri!.AbsolutePath;
        RequestedUris.Add(request.RequestUri);
        string? sentBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        Sent.Add((request.Method, path, sentBody));

        return FailingPaths.Contains(path)
            ? new HttpResponseMessage(HttpStatusCode.InternalServerError)
            : !Bodies.TryGetValue(path, out object? body)
            ? new HttpResponseMessage(HttpStatusCode.NotFound)
            : new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(body), System.Text.Encoding.UTF8, "application/json"),
            };
    }
}

/// <summary>
/// Shared setup for the /accounts/{Id} page tests: repository and session mocks, the real
/// <see cref="PairedSessionState"/> over the bUnit JS runtime, and a stub <see cref="HttpClient"/>
/// bound as the page's injected client for the sync-run and case list endpoints.
/// </summary>
/// <summary>Progress source that never emits, so the embedded download panel stays idle in these tests.</summary>
public sealed class SilentProgressSource : IDownloadProgressSource
{
    public IObservable<DownloadProgressSnapshot> Progress { get; } = Observable.Never<DownloadProgressSnapshot>();
}

public abstract class AccountDetailsPageTestBase : BunitContext
{
    protected static readonly Guid AccountId = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000001");

    protected StubHttpMessageHandler Handler { get; } = new();

    protected ProviderAccountEntity Account { get; }

    protected Mock<IForensicsConfiguration> ForensicsConfigMock { get; } = new();

    protected Mock<IForensicsConfigurationService> ForensicsConfigServiceMock { get; } = new();

    protected Mock<IProviderAuthService> AuthServiceMock { get; } = new();

    protected Mock<IMultiProviderAuthService> MultiProviderAuthServiceMock { get; } = new();

    protected Mock<ILocationRepository> LocationRepositoryMock { get; } = new();

    protected Mock<IDeviceRepository> DeviceRepositoryMock { get; } = new();

    protected Mock<IDeviceHealthRepository> DeviceHealthRepositoryMock { get; } = new();

    protected Mock<ISyncScheduleRepository> SyncScheduleRepositoryMock { get; } = new();

    protected Mock<IMediaItemRepository> MediaItemRepositoryMock { get; } = new();

    protected Mock<IMediaContentUrlProvider> MediaUrlProviderMock { get; } = new();

    /// <summary>Device health changes the test pushes; the account pages see them through IDeviceHealthChangedSource.</summary>
    protected Subject<DeviceHealthChangedDto> DeviceHealthChanges { get; } = new();

    /// <summary>Edit-access double; gating tests set <see cref="ProviderAccountEditAccessDouble.CanEdit"/> before rendering.</summary>
    protected ProviderAccountEditAccessDouble EditAccess { get; } = new();

    protected AccountDetailsPageTestBase()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        _ = Services.AddSingleton<IDeviceHealthChangedSource>(new DeviceHealthChangedSourceDouble(DeviceHealthChanges));
        _ = Services.AddSingleton<IProviderAccountEditAccess>(EditAccess);
        _ = Services.AddLocalization();
        _ = Services.AddSingleton<Syncfusion.Blazor.ISyncfusionStringLocalizer, Syncfusion.Blazor.SyncfusionStringLocalizer>();
        _ = Services.AddSingleton<Syncfusion.Blazor.GlobalOptions>();
        _ = Services.AddScoped<Syncfusion.Blazor.SyncfusionBlazorService>();

        var userId = Guid.NewGuid();
        var account = new ProviderAccountEntity
        {
            Id = AccountId,
            UserId = userId,
            ProviderName = "Ring",
            LinkedUtc = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc),
        };
        Account = account;

        var accountRepository = new Mock<IProviderAccountRepository>();
        _ = accountRepository
            .Setup(r => r.GetAsync(AccountId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);
        _ = accountRepository
            .Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([account]);

        var userRepository = new Mock<IUserRepository>();
        _ = userRepository
            .Setup(r => r.GetAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { ProviderUserKey = "ring-key", DisplayName = "Ring Owner" });

        _ = AuthServiceMock
            .Setup(a => a.RestoreFromSavedCredentialsAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _ = Services.AddScoped(_ => accountRepository.Object);
        _ = Services.AddScoped(_ => userRepository.Object);
        _ = Services.AddScoped(_ => AuthServiceMock.Object);
        _ = Services.AddScoped(_ => MultiProviderAuthServiceMock.Object);
        _ = Services.AddScoped(_ => ForensicsConfigMock.Object);
        _ = Services.AddScoped(_ => ForensicsConfigServiceMock.Object);
        // The Sync tab embeds the validate panel, which resolves these when it renders.
        _ = Services.AddScoped(_ => LocationRepositoryMock.Object);
        _ = Services.AddScoped(_ => DeviceRepositoryMock.Object);
        _ = Services.AddScoped(_ => DeviceHealthRepositoryMock.Object);
        // The Sync tab embeds the schedule settings, which resolve the sync-schedule repository.
        _ = Services.AddScoped(_ => SyncScheduleRepositoryMock.Object);
        _ = Services.AddScoped(_ => MediaItemRepositoryMock.Object);
        _ = Services.AddScoped(_ => MediaUrlProviderMock.Object);
        _ = Services.AddScoped(_ => Mock.Of<IEvidenceValidationJobs>());
        _ = Services.AddScoped(sp => new AccountSelectionState(sp.GetRequiredService<IJSRuntime>()));
        _ = Services.AddScoped(sp => new PairedSessionState(sp.GetRequiredService<IJSRuntime>()));
        _ = Services.AddScoped(sp => new WebAuthnClient(sp.GetRequiredService<IJSRuntime>(), new Mock<ISelfApiHttpClientFactory>().Object));
        _ = Services.AddScoped<StepUpService>();
        _ = Services.AddScoped(_ => Handler);
        _ = Services.AddScoped(_ => new HttpClient(Handler));
        // The sync output panel embeds the download progress panel, which subscribes to this source.
        _ = Services.AddSingleton<IDownloadProgressSource>(new SilentProgressSource());

        SetRendererInfo(new RendererInfo("Server", true));
    }

    protected IStringLocalizer<AccountDetails> Strings => Services.GetRequiredService<IStringLocalizer<AccountDetails>>();

    protected NavigationManager Nav => Services.GetRequiredService<NavigationManager>();

    protected IRenderedComponent<AccountDetails> RenderPage()
    {
        return Render<AccountDetails>(p => p.Add(c => c.Id, AccountId));
    }

    /// <summary>The Sync tab: sync controls, output, history, and the last-sync timestamps.</summary>
    protected IRenderedComponent<AccountSyncTab> RenderSync()
    {
        return Render<AccountSyncTab>(p => p.Add(c => c.Id, AccountId));
    }

    /// <summary>The Cases tab: the account's attached cases.</summary>
    protected IRenderedComponent<AccountCasesTab> RenderCases()
    {
        return Render<AccountCasesTab>(p => p.Add(c => c.Id, AccountId));
    }

    /// <summary>Absolute path (as the page requests it) for a sub-route of this account.</summary>
    protected static string ApiPath(string suffix)
    {
        return $"/api/v1/accounts/provider-accounts/{AccountId}{suffix}";
    }

    protected void SetJson(string suffix, object body)
    {
        Handler.Bodies[ApiPath(suffix)] = body;
    }

    protected void FailJson(string suffix)
    {
        _ = Handler.FailingPaths.Add(ApiPath(suffix));
    }

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

        IRenderedComponent<AccountSyncTab> cut = RenderSync();

        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll("#sync-history .sync-run-row").Count));
        var rows = cut.FindAll("#sync-history .sync-run-row").Select(r => r.TextContent).ToList();
        Assert.Contains(rows, text => text.Contains(Strings["SyncSucceeded"]));
        Assert.Contains(rows, text => text.Contains(Strings["SyncRunFailed"]));
    }

    [Fact]
    public void AccountDetails_SyncRunRendersTypeAndRecordsFromLocalizer()
    {
        var started = new DateTime(2026, 10, 1, 9, 0, 0, DateTimeKind.Utc);
        SetJson("/sync-runs", new[]
        {
            new ProviderSyncRunSummaryDto(Guid.NewGuid(), started, started.AddMinutes(1), true,
                SyncType: "Full", DevicesSynced: 3, LocationsSynced: 1, EventsSynced: 42, ConfigAreasSynced: 6, FeedsFailed: 0),
        });
        SetJson("/cases", Array.Empty<AccountCaseDto>());

        IRenderedComponent<AccountSyncTab> cut = RenderSync();

        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("#sync-history .sync-run-row")));
        Assert.Contains(Strings["ColSyncType"], cut.Find("#sync-history thead").TextContent);
        Assert.Contains(Strings["ColSyncRecords"], cut.Find("#sync-history thead").TextContent);
        string row = cut.Find("#sync-history .sync-run-row").TextContent;
        Assert.Contains(Strings["SyncTypeFull"], row);
        Assert.Contains(string.Format(Strings["SyncRecordsSummary"], 3, 1, 42, 6, 0), row);
    }

    [Fact]
    public void AccountDetails_SnapshotsOnlyRun_ShowsSnapshotCount()
    {
        var started = new DateTime(2026, 10, 2, 9, 0, 0, DateTimeKind.Utc);
        SetJson("/sync-runs", new[]
        {
            new ProviderSyncRunSummaryDto(Guid.NewGuid(), started, started.AddMinutes(1), true, SnapshotsSynced: 12),
        });
        SetJson("/cases", Array.Empty<AccountCaseDto>());

        IRenderedComponent<AccountSyncTab> cut = RenderSync();

        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("#sync-history .sync-run-row")));
        string row = cut.Find("#sync-history .sync-run-row").TextContent;
        Assert.Contains(string.Format(Strings["SyncRecordsSnapshots"], 12), row);
        // The type column also reads "Unknown" for this run, so check the records cell only.
        Assert.DoesNotContain(Strings["SyncRecordsUnknown"], cut.Find("#sync-history .sync-run-row td:nth-child(4)").TextContent);
    }

    [Fact]
    public void AccountDetails_OldSyncRunWithoutCounts_ShowsUnknownTypeAndRecords()
    {
        var started = new DateTime(2026, 9, 1, 9, 0, 0, DateTimeKind.Utc);
        SetJson("/sync-runs", new[] { new ProviderSyncRunSummaryDto(Guid.NewGuid(), started, started.AddMinutes(1), true) });
        SetJson("/cases", Array.Empty<AccountCaseDto>());

        IRenderedComponent<AccountSyncTab> cut = RenderSync();

        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("#sync-history .sync-run-row")));
        string row = cut.Find("#sync-history .sync-run-row").TextContent;
        Assert.Contains(Strings["SyncTypeUnknown"], row);
        Assert.Contains(Strings["SyncRecordsUnknown"], row);
    }

    [Fact]
    public void AccountDetails_SyncRunsEmpty_ShowsNoRunsMessage()
    {
        SetEmptyLists();

        IRenderedComponent<AccountSyncTab> cut = RenderSync();

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

        IRenderedComponent<AccountSyncTab> cut = RenderSync();
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("#sync-history .sync-run-row")));

        cut.Find("#sync-history .sync-run-row button").Click();

        cut.WaitForAssertion(() => Assert.Contains("Token expired", cut.Find("#sync-history").TextContent));
        Assert.Contains(Handler.RequestedUris, uri => uri.AbsolutePath == ApiPath($"/sync-runs/{runId}"));
    }

    [Fact]
    public void AccountDetails_SyncListFails_ShowsLoadErrorInSyncSectionOnly()
    {
        FailJson("/sync-runs");

        IRenderedComponent<AccountSyncTab> cut = RenderSync();

        cut.WaitForAssertion(() => Assert.Contains(Strings["LoadListError"], cut.Find("#sync-history").TextContent));
        Assert.Contains("Ring", cut.Find(".card-title").TextContent);
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

        IRenderedComponent<AccountCasesTab> cut = RenderCases();

        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("#attached-cases .case-row")));
        string row = cut.Find("#attached-cases .case-row").TextContent;
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

        IRenderedComponent<AccountCasesTab> cut = RenderCases();

        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("#attached-cases .case-row")));
        AngleSharp.Dom.IElement eventCell = cut.Find("#attached-cases .case-row td[data-col='event-date']");
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

        IRenderedComponent<AccountCasesTab> cut = RenderCases();
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("#attached-cases .case-row")));

        cut.Find("#attached-cases .case-row button").Click();

        Assert.EndsWith($"/cases/{caseId}", Nav.Uri);
    }

    [Fact]
    public void AccountDetails_CasesEmpty_ShowsCasesNoneMessage()
    {
        SetEmptyLists();

        IRenderedComponent<AccountCasesTab> cut = RenderCases();

        cut.WaitForAssertion(() => Assert.Contains(Strings["CasesNone"], cut.Find("#attached-cases").TextContent));
        Assert.Empty(cut.FindAll("#attached-cases .case-row"));
    }
}

public class AccountDetailsPage_SyncPanel_Tests : AccountDetailsPageTestBase
{
    [Fact]
    public void AccountDetails_Rendered_HasNoSelectOrSelectedButton()
    {
        SetEmptyLists();

        IRenderedComponent<AccountSyncTab> cut = RenderSync();

        cut.WaitForAssertion(() => Assert.Contains(Strings["SyncNow"], ButtonLabels(cut)));
        Assert.DoesNotContain("Select", ButtonLabels(cut));
        Assert.DoesNotContain("Selected", ButtonLabels(cut));
    }

    [Fact]
    public void AccountDetails_Rendered_DoesNotRenderConfigSyncInterval()
    {
        SetEmptyLists();

        IRenderedComponent<AccountSyncTab> cut = RenderSync();

        cut.WaitForAssertion(() => Assert.Contains(Strings["SyncNow"], ButtonLabels(cut)));
        // The config sync interval lives in the schedule section, never in the sync output or controls.
        Assert.Empty(cut.FindAll("#sync-output [id^='config-sync']"));
    }

    [Fact]
    public void AccountDetails_RangeNotCustom_HidesCustomDatePickers()
    {
        SetEmptyLists();

        IRenderedComponent<AccountSyncTab> cut = RenderSync();

        _ = cut.WaitForElement("#sync-range");
        Assert.Empty(cut.FindAll("#sync-custom-start, #sync-custom-end"));
    }

    [Fact]
    public void AccountDetails_SyncTypeEvents_ShowsRangeSelector()
    {
        SetEmptyLists();

        IRenderedComponent<AccountSyncTab> cut = RenderSync();

        _ = cut.WaitForElement("#sync-range");
    }

    [Fact]
    public void AccountDetails_SyncTypeConfig_HidesRangeSelector()
    {
        SetEmptyLists();

        IRenderedComponent<AccountSyncTab> cut = RenderSync();
        cut.WaitForElement("#sync-type").Change("Config");

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("#sync-range")));
    }

    [Fact]
    public void AccountDetails_RangeCustom_ShowsCustomDatePickers()
    {
        SetEmptyLists();

        IRenderedComponent<AccountSyncTab> cut = RenderSync();
        cut.WaitForElement("#sync-range").Change("Custom");

        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("#sync-custom-start")));
        _ = Assert.Single(cut.FindAll("#sync-custom-end"));
    }

    [Fact]
    public void AccountDetails_CustomRange_DefaultsToLast30Days()
    {
        SetEmptyLists();

        IRenderedComponent<AccountSyncTab> cut = RenderSync();
        cut.WaitForElement("#sync-range").Change("Custom");

        cut.WaitForAssertion(() => Assert.Equal(DateTime.Today.AddDays(-30).ToString("yyyy-MM-dd"), cut.Find("#sync-custom-start").GetAttribute("value")));
        Assert.Equal(DateTime.Today.ToString("yyyy-MM-dd"), cut.Find("#sync-custom-end").GetAttribute("value"));
    }

    [Fact]
    public void AccountDetails_CustomRange_ShowsEndDateMediaOnlyHint()
    {
        SetEmptyLists();

        IRenderedComponent<AccountSyncTab> cut = RenderSync();
        cut.WaitForElement("#sync-range").Change("Custom");

        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("#sync-custom-end-hint")));
        Assert.Equal(Strings["CustomEndHint"], cut.Find("#sync-custom-end-hint").TextContent);
    }

    [Fact]
    public void AccountDetails_SyncNowClicked_PostsChosenTypeAndRange()
    {
        SetEmptyLists();
        SetJson("/sync-now", new { });

        IRenderedComponent<AccountSyncTab> cut = RenderSync();
        cut.WaitForElement("#sync-type").Change("Events");
        cut.Find("#sync-range").Change("60");
        FindButtons(cut, Strings["SyncNow"]).Single().Click();

        cut.WaitForAssertion(() => Assert.Contains(Handler.Sent, s => s.Method == HttpMethod.Post && s.Path == ApiPath("/sync-now")));
        JsonElement root = JsonDocument.Parse(Handler.Sent.Single(s => s.Method == HttpMethod.Post).Body!).RootElement;
        Assert.Equal("Events", root.GetProperty("syncType").GetString());
        Assert.Equal("Days", root.GetProperty("rangeKind").GetString());
        Assert.Equal(60, root.GetProperty("days").GetInt32());
    }

    [Fact]
    public void AccountDetails_SyncTypeConfig_SendsSinceLastRange()
    {
        SetEmptyLists();
        SetJson("/sync-now", new { });

        IRenderedComponent<AccountSyncTab> cut = RenderSync();
        cut.WaitForElement("#sync-range").Change("60");
        cut.WaitForElement("#sync-type").Change("Config");
        FindButtons(cut, Strings["SyncNow"]).Single().Click();

        cut.WaitForAssertion(() => Assert.Contains(Handler.Sent, s => s.Method == HttpMethod.Post && s.Path == ApiPath("/sync-now")));
        JsonElement root = JsonDocument.Parse(Handler.Sent.Single(s => s.Method == HttpMethod.Post).Body!).RootElement;
        Assert.Equal("Config", root.GetProperty("syncType").GetString());
        Assert.Equal("SinceLast", root.GetProperty("rangeKind").GetString());
        Assert.True(!root.TryGetProperty("days", out JsonElement days) || days.ValueKind == JsonValueKind.Null);
    }

    [Fact]
    public void AccountDetails_SyncTypeAll_ShowsRangeSelectorWithAllHistoryChoice()
    {
        SetEmptyLists();

        IRenderedComponent<AccountSyncTab> cut = RenderSync();
        cut.WaitForElement("#sync-type").Change("All");

        cut.WaitForAssertion(() => Assert.Contains(cut.FindAll("#sync-range option"), o => o.GetAttribute("value") == "All"));
    }

    [Fact]
    public void AccountDetails_SyncTypeAllWithAllHistory_PostsAllTypeAndAllRange()
    {
        SetEmptyLists();
        SetJson("/sync-now", new { });

        IRenderedComponent<AccountSyncTab> cut = RenderSync();
        cut.WaitForElement("#sync-type").Change("All");
        cut.WaitForElement("#sync-range").Change("All");
        FindButtons(cut, Strings["SyncNow"]).Single().Click();

        cut.WaitForAssertion(() => Assert.Contains(Handler.Sent, s => s.Method == HttpMethod.Post && s.Path == ApiPath("/sync-now")));
        JsonElement root = JsonDocument.Parse(Handler.Sent.Single(s => s.Method == HttpMethod.Post).Body!).RootElement;
        Assert.Equal("All", root.GetProperty("syncType").GetString());
        Assert.Equal("All", root.GetProperty("rangeKind").GetString());
    }

    [Fact]
    public void AccountDetails_SyncRunWithSnapshotCount_ShowsSnapshotsTypeAndCount()
    {
        var started = new DateTime(2026, 10, 1, 9, 0, 0, DateTimeKind.Utc);
        SetJson("/sync-runs", new[]
        {
            new ProviderSyncRunSummaryDto(Guid.NewGuid(), started, started.AddMinutes(1), true, SyncType: "Snapshots", SnapshotsSynced: 5),
        });
        SetJson("/cases", Array.Empty<AccountCaseDto>());

        IRenderedComponent<AccountSyncTab> cut = RenderSync();

        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("#sync-history .sync-run-row")));
        string row = cut.Find("#sync-history .sync-run-row").TextContent;
        Assert.Contains(Strings["SyncTypeSnapshots"], row);
        Assert.Contains(string.Format(Strings["SyncRecordsSnapshots"], 5), row);
        Assert.DoesNotContain(Strings["SyncRecordsUnknown"], row);
    }

    [Fact]
    public void AccountDetails_SyncRunWithCountsAndSnapshots_AppendsSnapshotCount()
    {
        var started = new DateTime(2026, 10, 1, 9, 0, 0, DateTimeKind.Utc);
        SetJson("/sync-runs", new[]
        {
            new ProviderSyncRunSummaryDto(Guid.NewGuid(), started, started.AddMinutes(1), true,
                SyncType: "Events", DevicesSynced: 3, LocationsSynced: 1, EventsSynced: 42, ConfigAreasSynced: 0, FeedsFailed: 0, SnapshotsSynced: 3),
        });
        SetJson("/cases", Array.Empty<AccountCaseDto>());

        IRenderedComponent<AccountSyncTab> cut = RenderSync();

        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("#sync-history .sync-run-row")));
        string row = cut.Find("#sync-history .sync-run-row").TextContent;
        Assert.Contains(string.Format(Strings["SyncRecordsSummary"], 3, 1, 42, 0, 0), row);
        Assert.Contains(string.Format(Strings["SyncRecordsSnapshots"], 3), row);
    }

    [Fact]
    public void AccountDetails_SyncRunsLoaded_ShowsLatestRunInSyncOutput()
    {
        var started = new DateTime(2026, 10, 1, 9, 0, 0, DateTimeKind.Utc);
        SetJson("/sync-runs", new[]
        {
            new ProviderSyncRunSummaryDto(Guid.NewGuid(), started.AddDays(-1), started.AddDays(-1).AddMinutes(1), false),
            new ProviderSyncRunSummaryDto(Guid.NewGuid(), started, started.AddMinutes(1), true),
        });
        SetJson("/cases", Array.Empty<AccountCaseDto>());

        IRenderedComponent<AccountSyncTab> cut = RenderSync();

        cut.WaitForAssertion(() => Assert.Contains(Strings["LatestSyncRunLabel"], cut.Find("#sync-output").TextContent));
        Assert.Contains(Strings["SyncSucceeded"], cut.Find("#sync-output").TextContent);
    }

    private static List<string> ButtonLabels<T>(IRenderedComponent<T> cut) where T : Microsoft.AspNetCore.Components.IComponent
    {
        return cut.FindAll("button").Select(b => b.TextContent.Trim()).ToList();
    }

    private static List<AngleSharp.Dom.IElement> FindButtons<T>(IRenderedComponent<T> cut, string text) where T : Microsoft.AspNetCore.Components.IComponent
    {
        return cut.FindAll("button").Where(b => b.TextContent.Trim() == text).ToList();
    }
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

        IRenderedComponent<AccountCasesTab> cut = RenderCases();

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

        IRenderedComponent<AccountCasesTab> cut = RenderCases();

        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("#attached-cases .case-row")));
        string row = cut.Find("#attached-cases .case-row").TextContent;
        Assert.Contains(Strings["CaseTypeOther"], row);
        Assert.DoesNotContain("SomethingNew", row);
    }

    [Fact]
    public void AccountDetails_ErrorWithoutTimestamp_ShowsLocalizedNoValuePlaceholder()
    {
        Account.LastErrorMessage = "Token expired";
        SetEmptyLists();

        IRenderedComponent<AccountDetails> cut = RenderPage();

        cut.WaitForAssertion(() => Assert.Contains(Strings["CredentialRecoveryMessage"], cut.Markup));
        Assert.Contains(Strings["NoValue"], cut.Find(".border-danger").TextContent);
        Assert.DoesNotContain("Token expired", cut.Markup);
    }

    [Fact]
    public void AccountDetails_CredentialError_ShowsCleanRecoveryAlertWithoutRawException()
    {
        Account.LastErrorMessage = "Failed to decrypt stored credential: Error occurred during a cryptographic operation.";
        SetEmptyLists();

        IRenderedComponent<AccountDetails> cut = RenderPage();

        cut.WaitForAssertion(() => Assert.Contains(Strings["CredentialRecoveryMessage"], cut.Markup));
        Assert.Contains("role=\"alert\"", cut.Markup);
        Assert.Contains(Strings["Reauthenticate"], cut.Markup);
        Assert.DoesNotContain("Error occurred during a cryptographic operation", cut.Markup);
    }
}
