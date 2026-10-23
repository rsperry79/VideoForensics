
using AngleSharp.Html.Dom;

using Bunit;
using Bunit.Rendering;

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

using Moq;

using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Ui.Shared.Components.Inspector;
using VideoForensics.Ui.Shared.Components.Scope;
using VideoForensics.Ui.Shared.Pages;
using VideoForensics.Ui.Shared.Services;
using VideoForensics.Ui.Shared.Services.Cases;
using VideoForensics.Ui.Shared.Services.Inspector;
using VideoForensics.Ui.Shared.Services.Scope;

using Xunit;

namespace VideoForensics.Ui.Shared.Tests;
/// <summary>
/// Shared setup for Cases/CaseNew/CaseDetails page tests: repository mocks, a real
/// <see cref="ScopeState"/> and <see cref="CaseState"/> wired to the same
/// <see cref="ICaseRepository"/> mock, a real <see cref="InspectorState"/>, and a default
/// signed-out <see cref="PairedSessionState"/> (tests override with <see cref="RegisterRoleAsync"/>
/// - later registrations win, per the ScopeRailTests/InspectorPanelTests convention). Repository
/// mocks complete synchronously (ReturnsAsync from an already-completed Task), which is what lets
/// each page's async OnInitializedAsync load finish before Render&lt;T&gt;() returns.
/// </summary>
public abstract class CasesPagesTestBase : BunitContext
{
    protected static readonly DateTime FixedNowUtc = new(2026, 9, 24, 12, 0, 0, DateTimeKind.Utc);
    protected static readonly Guid TestOperatorId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    protected Mock<ICaseRepository> CaseRepositoryMock { get; } = new();
    protected Mock<IDeviceRepository> DeviceRepositoryMock { get; } = new();
    protected Mock<IEventRepository> EventRepositoryMock { get; } = new();
    protected Mock<IMediaItemRepository> MediaItemRepositoryMock { get; } = new();

    protected ScopeState ScopeState { get; }
    protected CaseState CaseState { get; }
    protected InspectorState InspectorState { get; } = new();

    /// <summary>
    /// A real, minimal subclass of <see cref="Syncfusion.Blazor.Grids.SfGrid{TValue}"/> used as a
    /// bUnit component double, the same trick <c>ForensicGridTests</c> and <c>EvidencePageTests</c>
    /// use: bUnit's generic stub isn't assignable to the typed <c>@ref</c> capture inside
    /// <c>ForensicGrid&lt;TItem&gt;</c>, but a real subclass is.
    /// </summary>
    private sealed class TestSfGrid<T> : Syncfusion.Blazor.Grids.SfGrid<T>
    {
        protected override void BuildRenderTree(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder)
        {
        }

        protected override Task OnInitializedAsync()
        {
            return Task.CompletedTask;
        }

        protected override Task OnParametersSetAsync()
        {
            return Task.CompletedTask;
        }

        protected override Task OnAfterRenderAsync(bool firstRender)
        {
            return Task.CompletedTask;
        }

        // Cases/CaseDetails page tests re-render after the initial load (status filter change,
        // remove-pin reload, close/reopen), unlike ForensicGridTests/EvidencePageTests which only
        // render once. The real SfGrid.ShouldRender() dereferences internal engine state that only
        // OnInitializedAsync/OnParametersSetAsync (skipped above) would have set up, so it null-refs
        // on that second SetParametersAsync. Skip it entirely - this double only exists to be a
        // valid @ref target and to carry DataSource/parameters for the test to inspect.
        protected override bool ShouldRender()
        {
            return false;
        }
    }

    protected CasesPagesTestBase()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        _ = Services.AddLocalization();
        ScopeOwnershipTestDoubles.RegisterEmpty(Services);

        _ = Services.AddSingleton<Syncfusion.Blazor.ISyncfusionStringLocalizer, Syncfusion.Blazor.SyncfusionStringLocalizer>();
        _ = Services.AddSingleton<Syncfusion.Blazor.GlobalOptions>();
        _ = Services.AddScoped<Syncfusion.Blazor.SyncfusionBlazorService>();

        _ = DeviceRepositoryMock
            .Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _ = CaseRepositoryMock
            .Setup(r => r.ListItemsAsync(It.IsAny<Guid>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _ = CaseRepositoryMock
            .Setup(r => r.GetDeviceIdsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        _ = Services.AddScoped(_ => CaseRepositoryMock.Object);
        _ = Services.AddScoped(_ => DeviceRepositoryMock.Object);
        _ = Services.AddScoped(_ => EventRepositoryMock.Object);
        _ = Services.AddScoped(_ => MediaItemRepositoryMock.Object);

        var timeProvider = new FakeTimeProvider(FixedNowUtc);
        ScopeState = new ScopeState(timeProvider);
        _ = Services.AddSingleton<TimeProvider>(timeProvider);
        _ = Services.AddScoped(_ => ScopeState);
        _ = Services.AddScoped(_ => InspectorState);

        CaseState = new CaseState(CaseRepositoryMock.Object, ScopeState);
        _ = Services.AddScoped(_ => CaseState);

        // Default: signed out. RegisterRoleAsync overrides this (later registration wins).
        _ = Services.AddScoped(sp => new PairedSessionState(sp.GetRequiredService<IJSRuntime>()));
    }

    /// <summary>Registers the Syncfusion grid stub for <c>ForensicGrid&lt;T&gt;</c>, mirroring ForensicGridTests/EvidencePageTests.</summary>
    protected void RegisterGridStub<T>()
    {
        _ = ComponentFactories.Add<Syncfusion.Blazor.Grids.SfGrid<T>, TestSfGrid<T>>();
        _ = ComponentFactories.AddStub<Syncfusion.Blazor.Grids.GridColumn>();
        _ = ComponentFactories.AddStub<Syncfusion.Blazor.Grids.GridPageSettings>();
        _ = ComponentFactories.AddStub<Syncfusion.Blazor.Grids.GridEvents<T>>();
    }

    protected async Task<PairedSessionState> RegisterRoleAsync(OperatorRole role, Guid? operatorId = null)
    {
        var session = new PairedSessionState(JSInterop.JSRuntime);
        await session.SetAsync("test-token", operatorId ?? TestOperatorId, role.ToString());
        _ = Services.AddScoped(_ => session);
        return session;
    }

    protected static ForensicCase CreateTestCase(
        Guid? id = null,
        string caseNumber = "CASE-001",
        string title = "Test Case",
        CaseStatus status = CaseStatus.Open,
        DateTime? scopeFromUtc = null,
        DateTime? scopeToUtc = null)
    {
        return new ForensicCase
        {
            Id = id ?? Guid.NewGuid(),
            CaseNumber = caseNumber,
            Title = title,
            Status = status,
            ScopeFromUtc = scopeFromUtc,
            ScopeToUtc = scopeToUtc,
            CreatedBy = TestOperatorId.ToString(),
            CreatedAtUtc = FixedNowUtc,
            UpdatedAtUtc = FixedNowUtc
        };
    }

    protected static Device CreateTestDevice(Guid? id = null, string name = "Test Device")
    {
        return new Device
        {
            Id = id ?? Guid.NewGuid(),
            LocationId = Guid.NewGuid(),
            ProviderDeviceId = "provider-123",
            Name = name,
            Type = "CameraDoorbell"
        };
    }

    protected static Event CreateTestEvent(Guid? id = null, Guid? deviceId = null, DateTime? occurredAtUtc = null)
    {
        DateTime occurred = occurredAtUtc ?? FixedNowUtc.AddDays(-1);
        return new Event
        {
            Id = id ?? Guid.NewGuid(),
            DeviceId = deviceId ?? Guid.NewGuid(),
            EventType = "Motion",
            ProviderEventId = Guid.NewGuid().ToString(),
            OccurredAtUtc = occurred,
            DiscoveredAtUtc = occurred.AddSeconds(2)
        };
    }

    protected static MediaItem CreateTestMediaItem(Guid? id = null, string fileName = "test.mp4", string sha256Hash = "ABC123")
    {
        return new MediaItem
        {
            Id = id ?? Guid.NewGuid(),
            DeviceId = Guid.NewGuid(),
            FileName = fileName,
            FilePath = "/path/to/" + fileName,
            MediaFormat = "mp4",
            Sha256Hash = sha256Hash,
            RecordedAtUtc = FixedNowUtc,
            DownloadedAtUtc = FixedNowUtc
        };
    }

    protected static CaseItem CreateEventPin(Guid caseId, Guid eventId, string reason = "Suspected incident")
    {
        return new CaseItem
        {
            Id = Guid.NewGuid(),
            CaseId = caseId,
            Kind = CaseItemKind.Event,
            EventId = eventId,
            Reason = reason,
            AddedBy = TestOperatorId.ToString(),
            AddedAtUtc = FixedNowUtc
        };
    }

    protected static CaseItem CreateMediaPin(Guid caseId, Guid mediaId, string shaAtAdd, string reason = "Evidence video")
    {
        return new CaseItem
        {
            Id = Guid.NewGuid(),
            CaseId = caseId,
            Kind = CaseItemKind.Media,
            MediaItemId = mediaId,
            MediaSha256AtAdd = shaAtAdd,
            Reason = reason,
            AddedBy = TestOperatorId.ToString(),
            AddedAtUtc = FixedNowUtc
        };
    }
}

/// <summary>Tests for Cases.razor (list view with status filter).</summary>
public class CasesPage_Tests : CasesPagesTestBase
{
    public CasesPage_Tests()
    {
        RegisterGridStub<ForensicCase>();
        _ = Services.AddScoped<RightPanelContentService>();
        _ = Services.AddScoped<LeftPanelContentService>();
    }

    private IRenderedComponent<Cases> RenderPage()
    {
        return Render<Cases>();
    }

    /// <summary>The fragment the page pushed into the right-panel slot; render it with Render(...) to drive its controls.</summary>
    private RenderFragment RightPanelFragment()
    {
        RightPanelContentService slot = Services.GetRequiredService<RightPanelContentService>();
        Assert.NotNull(slot.Content);
        return slot.Content!;
    }

    private static IEnumerable<ForensicCase> GridData(IRenderedComponent<Cases> component)
    {
        return Assert.IsAssignableFrom<IEnumerable<ForensicCase>>(
            component.FindComponent<ForensicGrid<ForensicCase>>().Instance.DataSource);
    }

    [Fact]
    public void DefaultLoad_CallsListAsyncOpen_RendersBothCaseNumbers()
    {
        ForensicCase openCase1 = CreateTestCase(caseNumber: "CASE-001", status: CaseStatus.Open);
        ForensicCase openCase2 = CreateTestCase(caseNumber: "CASE-002", status: CaseStatus.Open);

        _ = CaseRepositoryMock
            .Setup(r => r.ListAsync(CaseStatus.Open, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { openCase1, openCase2 });

        IRenderedComponent<Cases> component = RenderPage();

        // ScopeRail now lives in the right-panel slot, which the page render does not draw, so only the page loads.
        CaseRepositoryMock.Verify(r => r.ListAsync(CaseStatus.Open, It.IsAny<CancellationToken>()), Times.Once);

        var cases = GridData(component).ToList();
        Assert.Equal(2, cases.Count);
        Assert.Contains(cases, c => c.CaseNumber == "CASE-001");
        Assert.Contains(cases, c => c.CaseNumber == "CASE-002");
    }

    [Fact]
    public void Cases_RendersScopeFilter_InRightPanelSlot_NotInLeftPanel()
    {
        _ = CaseRepositoryMock
            .Setup(r => r.ListAsync(CaseStatus.Open, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { CreateTestCase(caseNumber: "CASE-001", status: CaseStatus.Open) });

        IRenderedComponent<Cases> component = RenderPage();

        // Page body has no ScopeRail; it was handed to the right-panel slot.
        Assert.Empty(component.FindComponents<ScopeRail>());
        LeftPanelContentService leftSlot = Services.GetRequiredService<LeftPanelContentService>();
        Assert.Null(leftSlot.Content);

        // Rendering the right-panel fragment (as MainLayout's right pane would) produces the scope rail.
        IRenderedComponent<ContainerFragment> panel = Render(RightPanelFragment());
        Assert.NotEmpty(panel.FindComponents<ScopeRail>());
    }

    [Fact]
    public void StatusFilter_RendersInRightPanel_NotInPageBody()
    {
        _ = CaseRepositoryMock
            .Setup(r => r.ListAsync(CaseStatus.Open, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        IRenderedComponent<Cases> component = RenderPage();

        Assert.Empty(component.FindAll("[data-testid='case-status-filter']"));

        IRenderedComponent<ContainerFragment> panel = Render(RightPanelFragment());
        _ = Assert.Single(panel.FindAll("[data-testid='case-status-filter']"));
    }

    [Fact]
    public void StatusFilter_InRightPanel_HasLocalizedLabel()
    {
        _ = CaseRepositoryMock
            .Setup(r => r.ListAsync(CaseStatus.Open, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        _ = RenderPage();
        IRenderedComponent<ContainerFragment> panel = Render(RightPanelFragment());

        Assert.Contains("Status", panel.Markup);
    }

    [Fact]
    public void StatusFilterChangedToAll_InRightPanel_CallsListAsyncWithNull_AndReRendersList()
    {
        ForensicCase openCase = CreateTestCase(caseNumber: "CASE-OPEN", status: CaseStatus.Open);
        ForensicCase closedCase = CreateTestCase(caseNumber: "CASE-CLOSED", status: CaseStatus.Closed);

        _ = CaseRepositoryMock
            .Setup(r => r.ListAsync(CaseStatus.Open, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { openCase });
        _ = CaseRepositoryMock
            .Setup(r => r.ListAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { openCase, closedCase });

        IRenderedComponent<Cases> component = RenderPage();

        IRenderedComponent<ContainerFragment> panel = Render(RightPanelFragment());
        var filter = (IHtmlSelectElement)panel.Find("[data-testid='case-status-filter']");
        filter.Change(string.Empty);

        CaseRepositoryMock.Verify(r => r.ListAsync(null, It.IsAny<CancellationToken>()), Times.Once);

        var cases = GridData(component).ToList();
        Assert.Equal(2, cases.Count);
        Assert.Contains(cases, c => c.CaseNumber == "CASE-CLOSED");
    }

    [Fact]
    public async Task ReadOnlyRole_HidesNewCaseButton()
    {
        _ = CaseRepositoryMock
            .Setup(r => r.ListAsync(CaseStatus.Open, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _ = await RegisterRoleAsync(OperatorRole.ReadOnly);

        IRenderedComponent<Cases> component = RenderPage();

        Assert.Empty(component.FindAll("[data-testid='new-case']"));
    }

    [Fact]
    public async Task ReviewRole_ShowsNewCaseButton_AndNavigatesOnClick()
    {
        _ = CaseRepositoryMock
            .Setup(r => r.ListAsync(CaseStatus.Open, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _ = await RegisterRoleAsync(OperatorRole.Review);

        IRenderedComponent<Cases> component = RenderPage();

        AngleSharp.Dom.IElement button = component.Find("[data-testid='new-case']");
        button.Click();

        NavigationManager nav = Services.GetRequiredService<NavigationManager>();
        Assert.EndsWith("/cases/new", nav.Uri);
    }

    [Fact]
    public async Task RowSelected_NavigatesToCaseDetails()
    {
        ForensicCase testCase = CreateTestCase();
        _ = CaseRepositoryMock
            .Setup(r => r.ListAsync(CaseStatus.Open, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { testCase });

        IRenderedComponent<Cases> component = RenderPage();
        IRenderedComponent<ForensicGrid<ForensicCase>> grid = component.FindComponent<ForensicGrid<ForensicCase>>();

        await component.InvokeAsync(() => grid.Instance.HandleRowSelectedAsync(testCase));

        NavigationManager nav = Services.GetRequiredService<NavigationManager>();
        Assert.Contains($"/cases/{testCase.Id}", nav.Uri);
    }

    [Fact]
    public void LoadFailure_ShowsError()
    {
        _ = CaseRepositoryMock
            .Setup(r => r.ListAsync(CaseStatus.Open, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("case store unreachable"));

        IRenderedComponent<Cases> component = RenderPage();

        Assert.Contains("case store unreachable", component.Markup);
    }
}

/// <summary>Tests for CaseNew.razor (create new case form).</summary>
public class CaseNewPage_Tests : CasesPagesTestBase
{
    private IRenderedComponent<CaseNew> RenderPage()
    {
        return Render<CaseNew>();
    }

    private static void Fill(IRenderedComponent<CaseNew> component, string caseNumber, string title)
    {
        // For @bind on text inputs, use Change() to trigger binding update
        ((IHtmlInputElement)component.Find("#title")).Change(title);
    }

    private static void Submit(IRenderedComponent<CaseNew> component)
    {
        component.Find("form").Submit();
    }

    [Fact]
    public async Task BlankTitle_DoesNotCallCreateAsync_ShowsInlineError()
    {
        _ = await RegisterRoleAsync(OperatorRole.Review);
        IRenderedComponent<CaseNew> component = RenderPage();

        // caseNumber is auto-generated, so we do not need to set it
        Submit(component);

        CaseRepositoryMock.Verify(
            r => r.CreateAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<Guid?>(),
                It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<IReadOnlyCollection<Guid>>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        Assert.Contains("Title is required", component.Markup);
    }

    [Fact]
    public async Task ValidSubmit_UseCurrentScopeChecked_CallsCreateAsyncWithScopeDevicesWindowAndActor()
    {
        _ = await RegisterRoleAsync(OperatorRole.Review, TestOperatorId);

        var device1 = Guid.NewGuid();
        var device2 = Guid.NewGuid();
        ScopeState.Set(ScopeState.Current with { DeviceIds = [device1, device2] });
        DateTime expectedFrom = ScopeState.Current.FromUtc;
        DateTime expectedTo = ScopeState.Current.ToUtc;

        ForensicCase createdCase = CreateTestCase(caseNumber: "NEW-001", title: "New Investigation");

        _ = CaseRepositoryMock
            .Setup(r => r.CreateAsync(
                null,
                "New Investigation",
                null,
                TestOperatorId,
                expectedFrom,
                expectedTo,
                It.Is<IReadOnlyCollection<Guid>>(d => d.SequenceEqual(new[] { device1, device2 })),
                TestOperatorId.ToString(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(createdCase);
        _ = CaseRepositoryMock
            .Setup(r => r.GetAsync(createdCase.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(createdCase);

        IRenderedComponent<CaseNew> component = RenderPage();
        Fill(component, "NEW-001", "New Investigation");
        Submit(component);

        CaseRepositoryMock.Verify(
            r => r.CreateAsync(
                null,
                "New Investigation",
                null,
                TestOperatorId,
                expectedFrom,
                expectedTo,
                It.Is<IReadOnlyCollection<Guid>>(d => d.SequenceEqual(new[] { device1, device2 })),
                TestOperatorId.ToString(),
                It.IsAny<CancellationToken>()),
            Times.Once);

        Assert.NotNull(CaseState.ActiveCase);
        Assert.Equal(createdCase.Id, CaseState.ActiveCase!.Id);

        NavigationManager nav = Services.GetRequiredService<NavigationManager>();
        Assert.Contains($"/cases/{createdCase.Id}", nav.Uri);
    }

    [Fact]
    public async Task ValidSubmit_UseCurrentScopeUnchecked_CallsCreateAsyncWithNullWindowAndNoDevices()
    {
        _ = await RegisterRoleAsync(OperatorRole.Review, TestOperatorId);
        ScopeState.Set(ScopeState.Current with { DeviceIds = [Guid.NewGuid()] });

        ForensicCase createdCase = CreateTestCase(caseNumber: "NEW-002", title: "No Scope");
        _ = CaseRepositoryMock
            .Setup(r => r.CreateAsync(
                "NEW-002", "No Scope", null, TestOperatorId,
                null, null,
                It.Is<IReadOnlyCollection<Guid>>(d => d.Count == 0),
                TestOperatorId.ToString(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(createdCase);
        _ = CaseRepositoryMock
            .Setup(r => r.GetAsync(createdCase.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(createdCase);

        IRenderedComponent<CaseNew> component = RenderPage();
        ((IHtmlInputElement)component.Find("#useCurrentScope")).Change(false);
        Fill(component, "NEW-002", "No Scope");
        Submit(component);

        CaseRepositoryMock.Verify(
            r => r.CreateAsync(
                null, "No Scope", null, TestOperatorId,
                null, null,
                It.Is<IReadOnlyCollection<Guid>>(d => d.Count == 0),
                TestOperatorId.ToString(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task DuplicateCaseNumber_ShowsMessage_DoesNotNavigate()
    {
        _ = await RegisterRoleAsync(OperatorRole.Review, TestOperatorId);
        _ = CaseRepositoryMock
            .Setup(r => r.CreateAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<Guid?>(),
                It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<IReadOnlyCollection<Guid>>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("CaseNumber already exists"));

        IRenderedComponent<CaseNew> component = RenderPage();
        string startingUri = Services.GetRequiredService<NavigationManager>().Uri;
        Fill(component, "DUP-001", "Duplicate");
        Submit(component);

        Assert.Contains("CaseNumber already exists", component.Markup);
        NavigationManager nav = Services.GetRequiredService<NavigationManager>();
        Assert.Equal(startingUri, nav.Uri);
        Assert.Null(CaseState.ActiveCase);
    }

    [Fact]
    public async Task ReadOnlyRole_ShowsReadOnlyNotice_NoForm()
    {
        _ = await RegisterRoleAsync(OperatorRole.ReadOnly);

        IRenderedComponent<CaseNew> component = RenderPage();

        Assert.Contains("You do not have permission", component.Markup);
        Assert.Empty(component.FindAll("form"));
    }
}

/// <summary>Tests for CaseDetails.razor (pinned items, close/reopen, activation).</summary>
public class CaseDetailsPage_Tests : CasesPagesTestBase
{
    public CaseDetailsPage_Tests()
    {
        RegisterGridStub<CaseDetails.CaseItemRow>();
    }

    private IRenderedComponent<CaseDetails> RenderPage(Guid caseId)
    {
        return Render<CaseDetails>(parameters => parameters.Add(p => p.CaseId, caseId));
    }

    private static List<CaseDetails.CaseItemRow> GridRows(IRenderedComponent<CaseDetails> component)
    {
        return Assert.IsAssignableFrom<IEnumerable<CaseDetails.CaseItemRow>>(
            component.FindComponent<ForensicGrid<CaseDetails.CaseItemRow>>().Instance.DataSource).ToList();
    }

    [Fact]
    public void PinnedItems_DisplayWithIntegrity_EventNotApplicable_MediaMatch()
    {
        var caseId = Guid.NewGuid();
        ForensicCase testCase = CreateTestCase(id: caseId);
        Event @event = CreateTestEvent();
        MediaItem media = CreateTestMediaItem(sha256Hash: "ABC123");
        CaseItem eventPin = CreateEventPin(caseId, @event.Id);
        CaseItem mediaPin = CreateMediaPin(caseId, media.Id, media.Sha256Hash!);

        _ = CaseRepositoryMock.Setup(r => r.GetAsync(caseId, It.IsAny<CancellationToken>())).ReturnsAsync(testCase);
        _ = CaseRepositoryMock
            .Setup(r => r.ListItemsAsync(caseId, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { eventPin, mediaPin });
        _ = EventRepositoryMock.Setup(r => r.GetAsync(@event.Id, It.IsAny<CancellationToken>())).ReturnsAsync(@event);
        _ = MediaItemRepositoryMock.Setup(r => r.GetAsync(media.Id, It.IsAny<CancellationToken>())).ReturnsAsync(media);

        IRenderedComponent<CaseDetails> component = RenderPage(caseId);
        List<CaseDetails.CaseItemRow> rows = GridRows(component);

        Assert.Equal(2, rows.Count);
        CaseDetails.CaseItemRow eventRow = Assert.Single(rows, r => r.Kind == CaseItemKind.Event);
        Assert.Equal(PinIntegrity.NotApplicable, eventRow.Integrity);
        Assert.Equal($"{@event.EventType} · {@event.OccurredAtUtc:u}", eventRow.Label);

        CaseDetails.CaseItemRow mediaRow = Assert.Single(rows, r => r.Kind == CaseItemKind.Media);
        Assert.Equal(PinIntegrity.Match, mediaRow.Integrity);
        Assert.Equal(media.FileName, mediaRow.Label);
    }

    [Fact]
    public void MediaLookupThrows_ShowsUnavailableLabel_PageStillRenders()
    {
        var caseId = Guid.NewGuid();
        ForensicCase testCase = CreateTestCase(id: caseId, caseNumber: "CASE-XYZ");
        CaseItem mediaPin = CreateMediaPin(caseId, Guid.NewGuid(), "ABC123");

        _ = CaseRepositoryMock.Setup(r => r.GetAsync(caseId, It.IsAny<CancellationToken>())).ReturnsAsync(testCase);
        _ = CaseRepositoryMock
            .Setup(r => r.ListItemsAsync(caseId, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { mediaPin });
        _ = MediaItemRepositoryMock
            .Setup(r => r.GetAsync(mediaPin.MediaItemId!.Value, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("media store unreachable"));

        IRenderedComponent<CaseDetails> component = RenderPage(caseId);

        Assert.Contains("CASE-XYZ", component.Markup);
        List<CaseDetails.CaseItemRow> rows = GridRows(component);
        CaseDetails.CaseItemRow row = Assert.Single(rows);
        Assert.Equal("Unavailable", row.Label);
    }

    [Fact]
    public void ShowRemoved_CallsListItemsAsyncWithIncludeRemovedTrue()
    {
        var caseId = Guid.NewGuid();
        ForensicCase testCase = CreateTestCase(id: caseId);
        CaseItem removedPin = CreateEventPin(caseId, Guid.NewGuid());
        Event @event = CreateTestEvent(id: removedPin.EventId);

        _ = CaseRepositoryMock.Setup(r => r.GetAsync(caseId, It.IsAny<CancellationToken>())).ReturnsAsync(testCase);
        _ = CaseRepositoryMock
            .Setup(r => r.ListItemsAsync(caseId, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _ = CaseRepositoryMock
            .Setup(r => r.ListItemsAsync(caseId, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { removedPin });
        _ = EventRepositoryMock.Setup(r => r.GetAsync(@event.Id, It.IsAny<CancellationToken>())).ReturnsAsync(@event);

        IRenderedComponent<CaseDetails> component = RenderPage(caseId);
        var checkbox = (IHtmlInputElement)component.Find("[data-testid='show-removed']");
        checkbox.Change(true);

        CaseRepositoryMock.Verify(r => r.ListItemsAsync(caseId, true, It.IsAny<CancellationToken>()), Times.Once);
        _ = Assert.Single(GridRows(component));
    }

    [Fact]
    public async Task RemoveWithReason_CallsRemoveItemAsync_ThenReloads()
    {
        var caseId = Guid.NewGuid();
        ForensicCase testCase = CreateTestCase(id: caseId);
        CaseItem pin = CreateEventPin(caseId, Guid.NewGuid());
        Event @event = CreateTestEvent(id: pin.EventId);

        _ = CaseRepositoryMock.Setup(r => r.GetAsync(caseId, It.IsAny<CancellationToken>())).ReturnsAsync(testCase);
        _ = CaseRepositoryMock
            .Setup(r => r.ListItemsAsync(caseId, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { pin });
        _ = EventRepositoryMock.Setup(r => r.GetAsync(@event.Id, It.IsAny<CancellationToken>())).ReturnsAsync(@event);
        _ = CaseRepositoryMock
            .Setup(r => r.RemoveItemAsync(pin.Id, TestOperatorId.ToString(), "No longer relevant", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _ = await RegisterRoleAsync(OperatorRole.Review, TestOperatorId);
        IRenderedComponent<CaseDetails> component = RenderPage(caseId);
        IRenderedComponent<ForensicGrid<CaseDetails.CaseItemRow>> grid = component.FindComponent<ForensicGrid<CaseDetails.CaseItemRow>>();
        CaseDetails.CaseItemRow row = GridRows(component).Single();

        await component.InvokeAsync(() => grid.Instance.HandleRowSelectedAsync(row));

        var reasonInput = (IHtmlInputElement)component.Find("[data-testid='remove-reason']");
        reasonInput.Change("No longer relevant");
        component.Find("[data-testid='remove-pin']").Click();

        CaseRepositoryMock.Verify(
            r => r.RemoveItemAsync(pin.Id, TestOperatorId.ToString(), "No longer relevant", It.IsAny<CancellationToken>()),
            Times.Once);
        CaseRepositoryMock.Verify(r => r.ListItemsAsync(caseId, false, It.IsAny<CancellationToken>()), Times.AtLeast(2));
    }

    [Fact]
    public async Task RemoveWithBlankReason_DoesNotCallRemoveItemAsync()
    {
        var caseId = Guid.NewGuid();
        ForensicCase testCase = CreateTestCase(id: caseId);
        CaseItem pin = CreateEventPin(caseId, Guid.NewGuid());
        Event @event = CreateTestEvent(id: pin.EventId);

        _ = CaseRepositoryMock.Setup(r => r.GetAsync(caseId, It.IsAny<CancellationToken>())).ReturnsAsync(testCase);
        _ = CaseRepositoryMock
            .Setup(r => r.ListItemsAsync(caseId, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { pin });
        _ = EventRepositoryMock.Setup(r => r.GetAsync(@event.Id, It.IsAny<CancellationToken>())).ReturnsAsync(@event);

        _ = await RegisterRoleAsync(OperatorRole.Review, TestOperatorId);
        IRenderedComponent<CaseDetails> component = RenderPage(caseId);
        IRenderedComponent<ForensicGrid<CaseDetails.CaseItemRow>> grid = component.FindComponent<ForensicGrid<CaseDetails.CaseItemRow>>();
        CaseDetails.CaseItemRow row = GridRows(component).Single();
        await component.InvokeAsync(() => grid.Instance.HandleRowSelectedAsync(row));

        component.Find("[data-testid='remove-pin']").Click();

        CaseRepositoryMock.Verify(
            r => r.RemoveItemAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task AdminRole_SeesCloseCaseButton()
    {
        var caseId = Guid.NewGuid();
        ForensicCase testCase = CreateTestCase(id: caseId, status: CaseStatus.Open);
        _ = CaseRepositoryMock.Setup(r => r.GetAsync(caseId, It.IsAny<CancellationToken>())).ReturnsAsync(testCase);

        _ = await RegisterRoleAsync(OperatorRole.Admin);
        IRenderedComponent<CaseDetails> component = RenderPage(caseId);

        Assert.NotEmpty(component.FindAll("[data-testid='close-case']"));
    }

    [Fact]
    public async Task ReviewRole_DoesNotSeeCloseCaseButton()
    {
        var caseId = Guid.NewGuid();
        ForensicCase testCase = CreateTestCase(id: caseId, status: CaseStatus.Open);
        _ = CaseRepositoryMock.Setup(r => r.GetAsync(caseId, It.IsAny<CancellationToken>())).ReturnsAsync(testCase);

        _ = await RegisterRoleAsync(OperatorRole.Review);
        IRenderedComponent<CaseDetails> component = RenderPage(caseId);

        Assert.Empty(component.FindAll("[data-testid='close-case']"));
    }

    [Fact]
    public async Task CloseCase_CallsCloseAsync_BadgeShowsClosed_EditRemoveDisabled()
    {
        var caseId = Guid.NewGuid();
        ForensicCase openCase = CreateTestCase(id: caseId, status: CaseStatus.Open);
        ForensicCase closedCase = CreateTestCase(id: caseId, status: CaseStatus.Closed);
        CaseItem pin = CreateEventPin(caseId, Guid.NewGuid());
        Event @event = CreateTestEvent(id: pin.EventId);

        _ = CaseRepositoryMock.SetupSequence(r => r.GetAsync(caseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(openCase)
            .ReturnsAsync(closedCase);
        _ = CaseRepositoryMock
            .Setup(r => r.ListItemsAsync(caseId, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { pin });
        _ = EventRepositoryMock.Setup(r => r.GetAsync(@event.Id, It.IsAny<CancellationToken>())).ReturnsAsync(@event);
        _ = CaseRepositoryMock
            .Setup(r => r.CloseAsync(caseId, TestOperatorId.ToString(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _ = await RegisterRoleAsync(OperatorRole.Admin, TestOperatorId);
        IRenderedComponent<CaseDetails> component = RenderPage(caseId);

        component.Find("[data-testid='close-case']").Click();

        CaseRepositoryMock.Verify(r => r.CloseAsync(caseId, TestOperatorId.ToString(), It.IsAny<CancellationToken>()), Times.Once);
        Assert.Contains("Closed", component.Markup);
        Assert.Empty(component.FindAll("[data-testid='close-case']"));
        Assert.Empty(component.FindAll("[data-testid='activate-case']"));
        Assert.Empty(component.FindAll("[data-testid='remove-pin']"));
    }

    [Fact]
    public async Task ReopenCase_Admin_CallsReopenAsync()
    {
        var caseId = Guid.NewGuid();
        ForensicCase closedCase = CreateTestCase(id: caseId, status: CaseStatus.Closed);
        ForensicCase reopenedCase = CreateTestCase(id: caseId, status: CaseStatus.Open);

        _ = CaseRepositoryMock.SetupSequence(r => r.GetAsync(caseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(closedCase)
            .ReturnsAsync(reopenedCase);
        _ = CaseRepositoryMock
            .Setup(r => r.ReopenAsync(caseId, TestOperatorId.ToString(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _ = await RegisterRoleAsync(OperatorRole.Admin, TestOperatorId);
        IRenderedComponent<CaseDetails> component = RenderPage(caseId);

        Assert.NotEmpty(component.FindAll("[data-testid='reopen-case']"));
        component.Find("[data-testid='reopen-case']").Click();

        CaseRepositoryMock.Verify(r => r.ReopenAsync(caseId, TestOperatorId.ToString(), It.IsAny<CancellationToken>()), Times.Once);
        Assert.Contains("Open", component.Markup);
        Assert.Empty(component.FindAll("[data-testid='reopen-case']"));
    }

    [Fact]
    public async Task WorkOnThisCase_ActivatesCase_NavigatesToEvidenceWithCaseParam()
    {
        var caseId = Guid.NewGuid();
        ForensicCase testCase = CreateTestCase(id: caseId, status: CaseStatus.Open);
        var deviceId = Guid.NewGuid();

        _ = CaseRepositoryMock.Setup(r => r.GetAsync(caseId, It.IsAny<CancellationToken>())).ReturnsAsync(testCase);
        _ = CaseRepositoryMock
            .Setup(r => r.GetDeviceIdsAsync(caseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { deviceId });

        _ = await RegisterRoleAsync(OperatorRole.Review, TestOperatorId);
        IRenderedComponent<CaseDetails> component = RenderPage(caseId);

        component.Find("[data-testid='activate-case']").Click();

        NavigationManager nav = Services.GetRequiredService<NavigationManager>();
        string path = new Uri(nav.Uri).PathAndQuery;
        Assert.StartsWith("/media", path);
        Assert.Contains($"case={caseId:D}", path);
        Assert.NotNull(CaseState.ActiveCase);
        Assert.Equal(caseId, CaseState.ActiveCase!.Id);
    }

    [Fact]
    public void UnknownId_ShowsCaseNotFound()
    {
        var caseId = Guid.NewGuid();
        _ = CaseRepositoryMock.Setup(r => r.GetAsync(caseId, It.IsAny<CancellationToken>())).ReturnsAsync((ForensicCase?)null);

        IRenderedComponent<CaseDetails> component = RenderPage(caseId);

        Assert.Contains("Case not found", component.Markup);
    }

    [Fact]
    public async Task SelectingPinnedItemRow_ShowsInInspectorState_WithIntegrityInProvenance()
    {
        var caseId = Guid.NewGuid();
        ForensicCase testCase = CreateTestCase(id: caseId);
        MediaItem media = CreateTestMediaItem(sha256Hash: "ABC123");
        CaseItem pin = CreateMediaPin(caseId, media.Id, media.Sha256Hash!);

        _ = CaseRepositoryMock.Setup(r => r.GetAsync(caseId, It.IsAny<CancellationToken>())).ReturnsAsync(testCase);
        _ = CaseRepositoryMock
            .Setup(r => r.ListItemsAsync(caseId, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { pin });
        _ = MediaItemRepositoryMock.Setup(r => r.GetAsync(media.Id, It.IsAny<CancellationToken>())).ReturnsAsync(media);

        IRenderedComponent<CaseDetails> component = RenderPage(caseId);
        IRenderedComponent<ForensicGrid<CaseDetails.CaseItemRow>> grid = component.FindComponent<ForensicGrid<CaseDetails.CaseItemRow>>();
        CaseDetails.CaseItemRow row = GridRows(component).Single();

        await component.InvokeAsync(() => grid.Instance.HandleRowSelectedAsync(row));

        Assert.NotNull(InspectorState.Current);
        Assert.Equal(row.Label, InspectorState.Current!.Title);
        Assert.NotNull(InspectorState.Current.Provenance);
        Assert.Contains(
            InspectorState.Current.Provenance!,
            kv => kv.Key == "Integrity" && kv.Value == PinIntegrity.Match.ToString());
    }
}
